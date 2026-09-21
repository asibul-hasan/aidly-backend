using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1006Service
{
    Task<List<Inv1006AttributeDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1006AttributeDto> SaveAsync(Inv1006AttributeDto dto, CancellationToken ct = default);
    Task DeleteAsync(long attributeNo, CancellationToken ct = default);
}

/// <summary>
/// INV_1006 Product Attribute — the variant axes (Size, Colour…) and their allowed values. Values
/// are edited inline with their attribute, so the save replaces the whole value set.
/// </summary>
public class Inv1006Service : IInv1006Service
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv1006Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv1006AttributeDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        var attributes = await _db.InvProductAttributes.AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == Deleted)
            .OrderBy(a => a.AttributeNo)
            .ToListAsync(ct);

        if (attributes.Count == 0) return new List<Inv1006AttributeDto>();

        var attributeNos = attributes.Select(a => a.AttributeNo).ToList();
        var values = await _db.InvProductAttributeValues.AsNoTracking()
            .Where(v => attributeNos.Contains(v.AttributeNo) && v.IsDeleted == Deleted)
            .OrderBy(v => v.OrderSl).ThenBy(v => v.AttributeValueNo)
            .ToListAsync(ct);

        var byAttribute = values.GroupBy(v => v.AttributeNo)
            .ToDictionary(g => g.Key, g => g.Select(ToValueDto).ToList());

        return attributes.Select(a => ToDto(a, byAttribute.GetValueOrDefault(a.AttributeNo) ?? new())).ToList();
    }

    public async Task<Inv1006AttributeDto> SaveAsync(Inv1006AttributeDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();

        if (string.IsNullOrWhiteSpace(dto.AttributeId)) throw new ValidationException("Attribute code is required");
        if (string.IsNullOrWhiteSpace(dto.AttributeName)) throw new ValidationException("Attribute name is required");

        AssertNoDuplicateValueCodes(dto.Values);

        InvProductAttribute e;
        if (dto.AttributeNo is > 0)
        {
            e = await _db.InvProductAttributes.FirstOrDefaultAsync(
                    a => a.AttributeNo == dto.AttributeNo.Value && a.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException($"Attribute not found: {dto.AttributeNo}");

            if (e.CompanyNo != companyNo) throw new ValidationException("Attribute belongs to another company");

            if (!string.Equals(e.AttributeId, dto.AttributeId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(companyNo, dto.AttributeId!, e.AttributeNo, ct))
            {
                throw new ValidationException($"Attribute code already exists: {dto.AttributeId}");
            }
        }
        else
        {
            if (await CodeTakenAsync(companyNo, dto.AttributeId!, null, ct))
                throw new ValidationException($"Attribute code already exists: {dto.AttributeId}");

            e = new InvProductAttribute
            {
                CompanyNo = companyNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvProductAttributes.Add(e);
        }

        e.AttributeId = dto.AttributeId!;
        e.AttributeName = dto.AttributeName!;
        e.OrderSl = dto.OrderSl ?? 0;
        e.IsActive = dto.IsActive ?? 1;
        e.UpdatedBy = _ctx.CurrentUserNo();
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await ReconcileValuesAsync(e.AttributeNo, dto.Values, ct);
        await _db.SaveChangesAsync(ct);

        var saved = await _db.InvProductAttributeValues.AsNoTracking()
            .Where(v => v.AttributeNo == e.AttributeNo && v.IsDeleted == Deleted)
            .OrderBy(v => v.OrderSl).ThenBy(v => v.AttributeValueNo)
            .ToListAsync(ct);

        return ToDto(e, saved.Select(ToValueDto).ToList());
    }

    public async Task DeleteAsync(long attributeNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var e = await _db.InvProductAttributes.FirstOrDefaultAsync(
                    a => a.AttributeNo == attributeNo && a.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException($"Attribute not found: {attributeNo}");

        if (e.CompanyNo != companyNo) throw new ValidationException("Attribute belongs to another company");

        var values = await _db.InvProductAttributeValues
            .Where(v => v.AttributeNo == attributeNo && v.IsDeleted == Deleted)
            .ToListAsync(ct);

        foreach (var v in values) v.PerformSoftDelete(_ctx.CurrentUserNo());
        e.PerformSoftDelete(_ctx.CurrentUserNo());

        await _db.SaveChangesAsync(ct);
    }

    // ── values ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Matches incoming rows to existing ones by <c>value_code</c> so a rename keeps the same
    /// <c>attribute_value_no</c> — variants already generated from it keep pointing at the right row.
    /// </summary>
    private async Task ReconcileValuesAsync(long attributeNo, List<Inv1006AttributeValueDto> rows,
                                            CancellationToken ct)
    {
        var existing = await _db.InvProductAttributeValues
            .Where(v => v.AttributeNo == attributeNo && v.IsDeleted == Deleted)
            .ToListAsync(ct);

        var byCode = new Dictionary<string, InvProductAttributeValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in existing) byCode.TryAdd(v.ValueCode, v);

        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int order = 1;

        foreach (var r in rows ?? new List<Inv1006AttributeValueDto>())
        {
            if (string.IsNullOrWhiteSpace(r.ValueCode)) continue;
            kept.Add(r.ValueCode);

            if (!byCode.TryGetValue(r.ValueCode, out var v))
            {
                // A soft-deleted row still holds the code under the table's live-row unique index.
                bool taken = await _db.InvProductAttributeValues.AnyAsync(
                    x => x.AttributeNo == attributeNo && x.ValueCode == r.ValueCode && x.IsDeleted != Deleted, ct);
                if (taken) throw new ValidationException($"Value code already exists: {r.ValueCode}");

                v = new InvProductAttributeValue
                {
                    AttributeNo = attributeNo,
                    ValueCode = r.ValueCode,
                    CreatedBy = _ctx.CurrentUserNo(),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = 0
                };
                _db.InvProductAttributeValues.Add(v);
            }

            v.ValueName = string.IsNullOrWhiteSpace(r.ValueName) ? r.ValueCode : r.ValueName;
            v.OrderSl = r.OrderSl ?? order;
            v.IsActive = r.IsActive ?? 1;
            v.UpdatedBy = _ctx.CurrentUserNo();
            v.UpdatedAt = DateTime.UtcNow;
            order++;
        }

        foreach (var v in existing)
        {
            if (!kept.Contains(v.ValueCode)) v.PerformSoftDelete(_ctx.CurrentUserNo());
        }
    }

    private static void AssertNoDuplicateValueCodes(List<Inv1006AttributeValueDto> rows)
    {
        if (rows == null) return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.ValueCode)) continue;
            if (!seen.Add(r.ValueCode)) throw new ValidationException($"Duplicate value code: {r.ValueCode}");
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<bool> CodeTakenAsync(long companyNo, string attributeId, long? excludeNo, CancellationToken ct) =>
        await _db.InvProductAttributes.AnyAsync(
            a => a.CompanyNo == companyNo && a.AttributeId == attributeId && a.IsDeleted == Deleted
                 && (excludeNo == null || a.AttributeNo != excludeNo), ct);

    private static Inv1006AttributeDto ToDto(InvProductAttribute e, List<Inv1006AttributeValueDto> values) => new()
    {
        AttributeNo = e.AttributeNo,
        AttributeId = e.AttributeId,
        AttributeName = e.AttributeName,
        OrderSl = e.OrderSl,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion,
        Values = values
    };

    private static Inv1006AttributeValueDto ToValueDto(InvProductAttributeValue v) => new()
    {
        AttributeValueNo = v.AttributeValueNo,
        ValueCode = v.ValueCode,
        ValueName = v.ValueName,
        OrderSl = v.OrderSl,
        IsActive = v.IsActive,
        RowVersion = v.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");
}
