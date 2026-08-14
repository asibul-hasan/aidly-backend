using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Numbering;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Sys1301Service — Document ID Generator setup
//
// Ported from the Java Sys1301Service, the last form the .NET migration did not
// cover. Configures how each form numbers its documents: either a full pattern
// (INV-{FY_YY_YY}-{SEQ:6}) or the older prefix + zero-padded counter.
// ═══════════════════════════════════════════════════════════════════════════

public interface ISys1301Service
{
    Task<List<Sys1301IdGeneratorDto>> GetListAsync(long? menuNo, CancellationToken ct = default);
    Task<List<Sys1301MenuOptionDto>> GetMenuOptionsAsync(CancellationToken ct = default);
    Task<List<SysLookupDto>> GetFinYearOptionsAsync(CancellationToken ct = default);
    IReadOnlyList<string> GetTokens();
    Task<Sys1301PreviewResponseDto> PreviewAsync(Sys1301PreviewRequestDto request, CancellationToken ct = default);
    Task<Sys1301IdGeneratorDto> SaveAsync(Sys1301IdGeneratorDto dto, CancellationToken ct = default);
    Task DeleteAsync(long docSequenceNo, CancellationToken ct = default);
}

public class Sys1301Service : ISys1301Service
{
    private const short Live = 0;
    private const short ResetNever = 2;

    private readonly ISysDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Sys1301Service(ISysDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Sys1301IdGeneratorDto>> GetListAsync(long? menuNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var rows = await _db.DocSequences.AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == Live
                        && (menuNo == null || s.MenuNo == menuNo))
            .OrderBy(s => s.DocSeqNo)
            .ToListAsync(ct);

        if (rows.Count == 0) return new List<Sys1301IdGeneratorDto>();

        // Three lookups for the whole page rather than a walk up the menu tree per row.
        var menuNos = rows.Where(r => r.MenuNo.HasValue).Select(r => r.MenuNo!.Value).Distinct().ToList();
        var menus = await _db.Menus.AsNoTracking()
            .Where(m => menuNos.Contains(m.MenuNo))
            .ToDictionaryAsync(m => m.MenuNo, ct);

        var subNos = menus.Values.Where(m => m.SubmoduleNo.HasValue).Select(m => m.SubmoduleNo!.Value).Distinct().ToList();
        var subs = await _db.SysSubmodules.AsNoTracking()
            .Where(s => subNos.Contains(s.SubmoduleNo))
            .ToDictionaryAsync(s => s.SubmoduleNo, ct);

        var modNos = subs.Values.Select(s => s.ModuleNo).Distinct().ToList();
        var mods = await _db.SysModules.AsNoTracking()
            .Where(m => modNos.Contains(m.ModuleNo))
            .ToDictionaryAsync(m => m.ModuleNo, ct);

        return rows.Select(r => ToDto(r, menus, subs, mods)).ToList();
    }

    public async Task<List<Sys1301MenuOptionDto>> GetMenuOptionsAsync(CancellationToken ct = default)
    {
        var menus = await _db.Menus.AsNoTracking().Where(m => m.IsDeleted == Live).ToListAsync(ct);
        if (menus.Count == 0) return new List<Sys1301MenuOptionDto>();

        var subs = await _db.SysSubmodules.AsNoTracking()
            .Where(s => menus.Where(m => m.SubmoduleNo.HasValue).Select(m => m.SubmoduleNo!.Value).Contains(s.SubmoduleNo))
            .ToDictionaryAsync(s => s.SubmoduleNo, ct);

        var mods = await _db.SysModules.AsNoTracking()
            .Where(m => subs.Values.Select(s => s.ModuleNo).Contains(m.ModuleNo))
            .ToDictionaryAsync(m => m.ModuleNo, ct);

        return menus.Select(m =>
            {
                SysSubmodule? sub = null;
                if (m.SubmoduleNo.HasValue) subs.TryGetValue(m.SubmoduleNo.Value, out sub);
                SysModule? mod = null;
                if (sub is not null) mods.TryGetValue(sub.ModuleNo, out mod);
                return new Sys1301MenuOptionDto
                {
                    MenuNo = m.MenuNo,
                    FormId = m.FormId,
                    FormName = m.FormName,
                    SubmoduleNo = sub?.SubmoduleNo,
                    SubmoduleName = sub?.SubmoduleName,
                    ModuleNo = mod?.ModuleNo,
                    ModuleName = mod?.ModuleName
                };
            })
            // Grouped the way the picker shows them: module, then submodule, then form.
            .OrderBy(o => o.ModuleName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.SubmoduleName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.FormName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<SysLookupDto>> GetFinYearOptionsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();
        return await _db.FinYears.AsNoTracking()
            .Where(f => f.CompanyNo == companyNo && f.IsDeleted == Live)
            .OrderBy(f => f.FinYearNo)
            .Select(f => new SysLookupDto { Value = f.FinYearNo, Label = f.FinYearName })
            .ToListAsync(ct);
    }

    public IReadOnlyList<string> GetTokens() => IdPatternResolver.SupportedTokens;

    public async Task<Sys1301PreviewResponseDto> PreviewAsync(Sys1301PreviewRequestDto request,
                                                              CancellationToken ct = default)
    {
        IdPatternResolver.Validate(request.Pattern);

        var docDate = DateOnly.FromDateTime(request.DocDate ?? DateTime.UtcNow.Date);
        var finYear = await PickFinYearForDateAsync(docDate, ct);

        long branchNo = Branch();
        var branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BranchNo == branchNo && b.IsDeleted == Live, ct)
            ?? throw new NotFoundException("Active branch not found");

        var ctx = new IdPatternResolver.Ctx(
            DocDate: docDate,
            BranchCode: branch.BranchId,
            CompanyCode: null,          // no token needs it today; kept for parity with the resolver
            DocSubType: request.DocSubType ?? string.Empty,
            FiscalYearStart: finYear is null ? null : finYear.StartDate.Year,
            FiscalYearEnd: finYear is null ? null : finYear.EndDate.Year,
            SequenceValue: request.SampleSequence ?? 1L);

        return new Sys1301PreviewResponseDto
        {
            Preview = IdPatternResolver.Resolve(request.Pattern!, ctx),
            TokensUsed = string.Join(",", IdPatternResolver.TokensUsed(request.Pattern))
        };
    }

    public async Task<Sys1301IdGeneratorDto> SaveAsync(Sys1301IdGeneratorDto dto, CancellationToken ct = default)
    {
        IdPatternResolver.Validate(dto.Pattern);

        long companyNo = Company();
        long branchNo = dto.BranchNo ?? Branch();
        string subType = dto.DocSubType ?? string.Empty;

        DocSequence entity;
        bool isNew = dto.DocSequenceNo is null or 0;

        if (!isNew)
        {
            entity = await _db.DocSequences
                .FirstOrDefaultAsync(s => s.DocSeqNo == dto.DocSequenceNo && s.CompanyNo == companyNo
                                          && s.IsDeleted == Live, ct)
                ?? throw new NotFoundException("Sequence not found");
        }
        else
        {
            entity = new DocSequence
            {
                CompanyNo = companyNo,
                IsDeleted = Live,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
        }

        // One series per form + sub-type + fin year. Two would hand the same number to the same
        // document twice, which is the one thing a numbering table must never do.
        bool duplicate = await _db.DocSequences.AsNoTracking()
            .AnyAsync(s => s.CompanyNo == companyNo && s.BranchNo == branchNo
                           && s.MenuNo == dto.MenuNo && s.DocSubType == subType
                           && s.FinYearNo == dto.FinYearNo && s.IsDeleted == Live
                           && (isNew || s.DocSeqNo != dto.DocSequenceNo), ct);

        if (duplicate)
            throw new ValidationException("A pattern for this form + sub-type + fin year already exists");

        entity.BranchNo = branchNo;
        entity.MenuNo = dto.MenuNo;
        entity.DocSubType = subType;
        entity.DocType = await DeriveDocTypeAsync(dto, ct);
        entity.FinYearNo = dto.FinYearNo;
        entity.Pattern = dto.Pattern;
        entity.Prefix = dto.Prefix ?? string.Empty;
        entity.Suffix = dto.Suffix ?? string.Empty;
        entity.StartingNo = dto.StartingNo ?? 1L;
        entity.Padding = dto.Padding ?? 6;
        entity.ResetPolicy = dto.ResetPolicy ?? ResetNever;
        entity.IsActive = dto.IsActive ?? 1;

        // next_no is seeded once and never reset from the form afterwards: winding a live counter
        // back would re-issue numbers that are already on printed documents.
        if (isNew) entity.NextVal = dto.StartingNo ?? 1L;

        if (isNew) _db.DocSequences.Add(entity);
        else { entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow; }

        await _db.SaveChangesAsync(ct);

        var single = await GetListAsync(entity.MenuNo, ct);
        return single.FirstOrDefault(x => x.DocSequenceNo == entity.DocSeqNo) ?? ToDto(entity, new(), new(), new());
    }

    public async Task DeleteAsync(long docSequenceNo, CancellationToken ct = default)
    {
        long companyNo = Company();
        var entity = await _db.DocSequences
            .FirstOrDefaultAsync(s => s.DocSeqNo == docSequenceNo && s.CompanyNo == companyNo
                                      && s.IsDeleted == Live, ct)
            ?? throw new NotFoundException("Sequence not found");

        // A series that has issued numbers is history, not configuration. Removing it would let a
        // fresh series restart at 1 and duplicate ids already printed on documents.
        if (entity.NextVal > entity.StartingNo)
            throw new ValidationException(
                $"This series has already issued numbers (next is {entity.NextVal}) — deactivate it instead of deleting");

        entity.IsDeleted = 1;
        entity.IsActive = 0;
        entity.DeletedBy = _ctx.CurrentUserNo();
        entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>The form's own id is the natural doc type; falls back to whatever the caller sent.</summary>
    private async Task<string> DeriveDocTypeAsync(Sys1301IdGeneratorDto dto, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(dto.DocType)) return dto.DocType!.Trim();

        if (dto.MenuNo.HasValue)
        {
            var formId = await _db.Menus.AsNoTracking()
                .Where(m => m.MenuNo == dto.MenuNo.Value)
                .Select(m => m.FormId)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(formId)) return formId!;
        }

        throw new ValidationException("Document type could not be determined — pick a form");
    }

    private async Task<FinYear?> PickFinYearForDateAsync(DateOnly date, CancellationToken ct)
    {
        long companyNo = Company();
        return await _db.FinYears.AsNoTracking()
            .Where(f => f.CompanyNo == companyNo && f.IsDeleted == Live
                        && f.StartDate <= date && f.EndDate >= date)
            .OrderBy(f => f.FinYearNo)
            .FirstOrDefaultAsync(ct);
    }

    private static Sys1301IdGeneratorDto ToDto(DocSequence r,
                                               Dictionary<long, Menu> menus,
                                               Dictionary<long, SysSubmodule> subs,
                                               Dictionary<long, SysModule> mods)
    {
        Menu? menu = null;
        if (r.MenuNo.HasValue) menus.TryGetValue(r.MenuNo.Value, out menu);

        SysSubmodule? sub = null;
        if (menu?.SubmoduleNo is long subNo) subs.TryGetValue(subNo, out sub);

        SysModule? mod = null;
        if (sub is not null) mods.TryGetValue(sub.ModuleNo, out mod);

        return new Sys1301IdGeneratorDto
        {
            DocSequenceNo = r.DocSeqNo,
            MenuNo = r.MenuNo,
            FormId = menu?.FormId,
            FormName = menu?.FormName,
            SubmoduleName = sub?.SubmoduleName,
            ModuleName = mod?.ModuleName,
            DocType = r.DocType,
            DocSubType = r.DocSubType,
            FinYearNo = r.FinYearNo,
            BranchNo = r.BranchNo,
            Pattern = r.Pattern,
            Prefix = r.Prefix,
            Suffix = r.Suffix,
            StartingNo = r.StartingNo,
            NextNo = r.NextVal,
            Padding = r.Padding,
            ResetPolicy = r.ResetPolicy,
            IsActive = r.IsActive ?? 0,
            RowVersion = r.RowVersion
        };
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("No active branch in context");
}
