using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv2001Service
{
    Task<List<Inv2001WarehouseDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv2001WarehouseDto> SaveAsync(Inv2001WarehouseDto dto, CancellationToken ct = default);
    Task DeleteAsync(long warehouseNo, CancellationToken ct = default);
}

/// <summary>INV_2001 Warehouse Setup — the stores, outlets and transit points stock can sit in.</summary>
public class Inv2001Service : IInv2001Service
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv2001Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv2001WarehouseDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var rows = await _db.InvWarehouses.AsNoTracking()
            .Where(w => w.CompanyNo == companyNo && w.BranchNo == branchNo && w.IsDeleted == Deleted)
            .OrderBy(w => w.WarehouseNo)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<Inv2001WarehouseDto> SaveAsync(Inv2001WarehouseDto dto, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        if (dto.WarehouseType is null or < 1 or > 5)
            throw new ValidationException("Warehouse type must be 1=Main,2=Outlet,3=Transit,4=Damage,5=Returns");
        if (string.IsNullOrWhiteSpace(dto.WarehouseId)) throw new ValidationException("Warehouse code is required");
        if (string.IsNullOrWhiteSpace(dto.WarehouseName)) throw new ValidationException("Warehouse name is required");

        InvWarehouse e;
        if (dto.WarehouseNo is > 0)
        {
            e = await _db.InvWarehouses.FirstOrDefaultAsync(
                    w => w.WarehouseNo == dto.WarehouseNo.Value && w.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Warehouse not found");

            if (e.CompanyNo != companyNo) throw new ValidationException("Warehouse belongs to another company");

            if (!string.Equals(e.WarehouseId, dto.WarehouseId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(branchNo, dto.WarehouseId!, e.WarehouseNo, ct))
            {
                throw new ValidationException($"Warehouse code already exists: {dto.WarehouseId}");
            }
        }
        else
        {
            if (await CodeTakenAsync(branchNo, dto.WarehouseId!, null, ct))
                throw new ValidationException($"Warehouse code already exists: {dto.WarehouseId}");

            e = new InvWarehouse
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvWarehouses.Add(e);
        }

        e.WarehouseId = dto.WarehouseId!;
        e.WarehouseName = dto.WarehouseName!;
        e.WarehouseNameNls = dto.WarehouseNameNls;
        e.WarehouseType = dto.WarehouseType!.Value;
        e.Address = dto.Address;
        e.ManagerEmployeeNo = dto.ManagerEmployeeNo;
        e.IsDefault = dto.IsDefault ?? 0;
        e.AllowNegativeStock = dto.AllowNegativeStock ?? 0;
        e.IsSalePoint = dto.IsSalePoint ?? 1;
        e.Remarks = dto.Remarks;
        e.IsActive = dto.IsActive ?? 1;
        e.UpdatedBy = _ctx.CurrentUserNo();
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (e.IsDefault == 1) await ClearOtherDefaultsAsync(branchNo, e.WarehouseNo, ct);

        return ToDto(e);
    }

    public async Task DeleteAsync(long warehouseNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var e = await _db.InvWarehouses.FirstOrDefaultAsync(
                    w => w.WarehouseNo == warehouseNo && w.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Warehouse not found");

        if (e.CompanyNo != companyNo) throw new ValidationException("Warehouse belongs to another company");

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<bool> CodeTakenAsync(long branchNo, string warehouseId, long? excludeNo, CancellationToken ct) =>
        await _db.InvWarehouses.AnyAsync(
            w => w.BranchNo == branchNo && w.WarehouseId == warehouseId && w.IsDeleted == Deleted
                 && (excludeNo == null || w.WarehouseNo != excludeNo), ct);

    /// <summary>At most one default warehouse per branch, so document forms have one obvious pick.</summary>
    private async Task ClearOtherDefaultsAsync(long branchNo, long keepWarehouseNo, CancellationToken ct)
    {
        var others = await _db.InvWarehouses
            .Where(w => w.BranchNo == branchNo && w.WarehouseNo != keepWarehouseNo
                        && w.IsDefault == 1 && w.IsDeleted == Deleted)
            .ToListAsync(ct);

        if (others.Count == 0) return;

        foreach (var w in others) w.IsDefault = 0;
        await _db.SaveChangesAsync(ct);
    }

    private static Inv2001WarehouseDto ToDto(InvWarehouse e) => new()
    {
        WarehouseNo = e.WarehouseNo,
        WarehouseId = e.WarehouseId,
        WarehouseName = e.WarehouseName,
        WarehouseNameNls = e.WarehouseNameNls,
        WarehouseType = e.WarehouseType,
        Address = e.Address,
        ManagerEmployeeNo = e.ManagerEmployeeNo,
        IsDefault = e.IsDefault,
        AllowNegativeStock = e.AllowNegativeStock,
        IsSalePoint = e.IsSalePoint,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("No active branch in context");
}
