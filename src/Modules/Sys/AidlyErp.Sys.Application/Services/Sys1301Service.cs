using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Numbering;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// SYS_1301 — ID Generator setup
//
// Configures the document-number series that DocSequenceGenerator hands out.
// The generator reads what is saved here: pattern, prefix, suffix, padding,
// starting number and yearly reset. Nothing in this service issues numbers.
// ═══════════════════════════════════════════════════════════════════════════

public interface ISys1301Service
{
    Task<List<Sys1301IdGeneratorDto>> GetListAsync(long? menuNo, CancellationToken ct = default);
    Task<List<Sys1301MenuOptionDto>> GetMenuOptionsAsync(CancellationToken ct = default);
    Task<List<SysLookupOptionDto>> GetFinYearOptionsAsync(CancellationToken ct = default);
    IReadOnlyList<string> GetSupportedTokens();
    Sys1301PreviewResponseDto Preview(Sys1301PreviewRequestDto request);
    Task<Sys1301IdGeneratorDto> SaveAsync(Sys1301IdGeneratorDto dto, CancellationToken ct = default);
    Task DeleteAsync(long docSequenceNo, CancellationToken ct = default);
}

public class Sys1301Service : ISys1301Service
{
    private const short Live = 0;

    private readonly ISysDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Sys1301Service(ISysDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");

    public async Task<List<Sys1301IdGeneratorDto>> GetListAsync(long? menuNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var query = _db.DocSequences.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == Live);

        if (menuNo.HasValue && menuNo.Value > 0)
            query = query.Where(s => s.MenuNo == menuNo.Value);

        var rows = await query.OrderBy(s => s.DocSeqNo).ToListAsync(ct);
        if (rows.Count == 0) return new List<Sys1301IdGeneratorDto>();

        // Menu names in one query rather than one per row.
        var menuNos = rows.Where(r => r.MenuNo.HasValue).Select(r => r.MenuNo!.Value).Distinct().ToList();
        var menus = await _db.Menus.AsNoTracking()
            .Where(m => menuNos.Contains(m.MenuNo) && m.IsDeleted == Live)
            .Select(m => new { m.MenuNo, m.FormId, m.FormName })
            .ToDictionaryAsync(m => m.MenuNo, ct);

        return rows.Select(s =>
        {
            var dto = ToDto(s);
            if (s.MenuNo.HasValue && menus.TryGetValue(s.MenuNo.Value, out var m))
            {
                dto.FormId = m.FormId;
                dto.FormName = m.FormName;
            }
            return dto;
        }).ToList();
    }

    public async Task<List<Sys1301MenuOptionDto>> GetMenuOptionsAsync(CancellationToken ct = default) =>
        await _db.Menus.AsNoTracking()
            .Where(m => m.IsDeleted == Live && m.FormId != null)
            .OrderBy(m => m.FormId)
            .Select(m => new Sys1301MenuOptionDto
            {
                MenuNo = m.MenuNo,
                FormId = m.FormId!,
                FormName = m.FormName ?? m.FormId!,
                SubmoduleNo = m.SubmoduleNo
            })
            .ToListAsync(ct);

    public async Task<List<SysLookupOptionDto>> GetFinYearOptionsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();
        return await _db.FinYears.AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.IsDeleted == Live)
            .OrderByDescending(y => y.FinYearNo)
            .Select(y => new SysLookupOptionDto { Value = y.FinYearNo, Label = y.FinYearId })
            .ToListAsync(ct);
    }

    public IReadOnlyList<string> GetSupportedTokens() => IdPatternResolver.SupportedTokens;

    /// <summary>
    /// Renders a pattern against sample context so the operator sees the shape before saving.
    /// Validation runs first, so an unusable pattern is reported as an error rather than
    /// previewed into something misleading.
    /// </summary>
    public Sys1301PreviewResponseDto Preview(Sys1301PreviewRequestDto request)
    {
        IdPatternResolver.Validate(request.Pattern);

        var date = DateOnly.FromDateTime(request.DocDate ?? DateTime.UtcNow.Date);

        // Sample values, clearly recognisable as samples in the rendered preview.
        var ctx = new IdPatternResolver.Ctx(
            DocDate: date,
            BranchCode: "BR",
            CompanyCode: "CO",
            DocSubType: string.IsNullOrWhiteSpace(request.DocSubType) ? "DOC" : request.DocSubType,
            FiscalYearStart: date.Year,
            FiscalYearEnd: date.Year + 1,
            SequenceValue: request.SampleSequence is > 0 ? request.SampleSequence : 1);

        return new Sys1301PreviewResponseDto
        {
            Preview = IdPatternResolver.Resolve(request.Pattern, ctx),
            TokensUsed = string.Join(", ", IdPatternResolver.TokensUsed(request.Pattern))
        };
    }

    public async Task<Sys1301IdGeneratorDto> SaveAsync(Sys1301IdGeneratorDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();

        // A pattern that cannot resolve would break every document of this type at save time —
        // far better to refuse it here, where one person sees the message.
        if (!string.IsNullOrWhiteSpace(dto.Pattern))
            IdPatternResolver.Validate(dto.Pattern);

        if (string.IsNullOrWhiteSpace(dto.Pattern) && string.IsNullOrWhiteSpace(dto.Prefix))
            throw new ValidationException("Give the series either a pattern or a prefix");

        if (dto.Padding is < 1 or > 12)
            throw new ValidationException("Padding must be between 1 and 12");

        if (dto.StartingNo < 1)
            throw new ValidationException("Starting number must be 1 or more");

        DocSequence seq;
        if (dto.DocSequenceNo is > 0)
        {
            seq = await _db.DocSequences
                .FirstOrDefaultAsync(s => s.DocSeqNo == dto.DocSequenceNo && s.IsDeleted == Live, ct)
                ?? throw new NotFoundException($"ID generator config not found: {dto.DocSequenceNo}");

            if (seq.CompanyNo != companyNo)
                throw new ValidationException("This configuration belongs to another company");
        }
        else
        {
            seq = new DocSequence
            {
                CompanyNo = companyNo,
                DocType = (dto.DocType ?? dto.FormId ?? string.Empty).Trim(),
                // A brand-new series starts where the operator says, not at 1.
                NextVal = dto.StartingNo > 0 ? dto.StartingNo : 1,
                IsDeleted = Live,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.DocSequences.Add(seq);
        }

        if (string.IsNullOrWhiteSpace(seq.DocType))
            throw new ValidationException("Document type is required — pick the form this series belongs to");

        seq.MenuNo = dto.MenuNo;
        // sys_doc_sequence.branch_no is NOT NULL, and a series is per branch anyway — two tills in
        // different branches must not share one counter. Falls back to the acting user's branch
        // when the form leaves it blank rather than passing a null the column will reject.
        seq.BranchNo = dto.BranchNo ?? _ctx.CurrentBranchNo()
            ?? throw new ValidationException("Branch is required for a document series");
        seq.FinYearNo = dto.FinYearNo;
        seq.DocSubType = dto.DocSubType?.Trim() ?? string.Empty;
        seq.Pattern = string.IsNullOrWhiteSpace(dto.Pattern) ? null : dto.Pattern.Trim();
        seq.Prefix = dto.Prefix?.Trim();
        seq.Suffix = dto.Suffix?.Trim() ?? string.Empty;
        seq.StartingNo = dto.StartingNo;
        seq.Padding = dto.Padding;
        seq.ResetPolicy = dto.ResetPolicy;
        seq.IsActive = dto.IsActive;
        seq.UpdatedBy = _ctx.CurrentUserNo();
        seq.UpdatedAt = DateTime.UtcNow;

        // next_no is deliberately NOT taken from the DTO. It is the engine's running counter;
        // letting the form write it would hand out a number that has already been used.

        await _db.SaveChangesAsync(ct);
        return ToDto(seq);
    }

    public async Task DeleteAsync(long docSequenceNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var seq = await _db.DocSequences
            .FirstOrDefaultAsync(s => s.DocSeqNo == docSequenceNo && s.IsDeleted == Live, ct)
            ?? throw new NotFoundException($"ID generator config not found: {docSequenceNo}");

        if (seq.CompanyNo != companyNo)
            throw new ValidationException("This configuration belongs to another company");

        // A series that has issued numbers is history: deleting it would let the next document
        // reuse a number already printed on a customer's invoice.
        if (seq.NextVal > seq.StartingNo)
            throw new ValidationException(
                $"This series has already issued {seq.NextVal - seq.StartingNo} number(s) — set it inactive instead of deleting it");

        seq.IsDeleted = 1;
        seq.IsActive = 0;
        seq.DeletedBy = _ctx.CurrentUserNo();
        seq.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Sys1301IdGeneratorDto ToDto(DocSequence s) => new()
    {
        DocSequenceNo = s.DocSeqNo,
        MenuNo = s.MenuNo,
        DocType = s.DocType,
        DocSubType = s.DocSubType,
        BranchNo = s.BranchNo,
        FinYearNo = s.FinYearNo,
        Pattern = s.Pattern,
        Prefix = s.Prefix,
        Suffix = s.Suffix,
        StartingNo = s.StartingNo,
        NextNo = s.NextVal,
        Padding = s.Padding,
        ResetPolicy = s.ResetPolicy,
        IsActive = s.IsActive ?? 1,
        RowVersion = s.RowVersion
    };
}
