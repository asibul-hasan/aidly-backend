using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv2002Service
{
    Task<List<Inv2002RackDto>> GetListAsync(long warehouseNo, CancellationToken ct = default);
    Task<Inv2002RackDto> SaveAsync(Inv2002RackDto dto, CancellationToken ct = default);
    Task DeleteAsync(long rackNo, CancellationToken ct = default);
}

/// <summary>
/// INV_2002 Rack / Shelf Setup — bin locations inside a warehouse. Racks hang off a warehouse, so
/// every read is scoped through one; the warehouse itself carries the tenant.
/// </summary>
public class Inv2002Service : IInv2002Service
{
    private const short Deleted = 0;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv2002Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv2002RackDto>> GetListAsync(long warehouseNo, CancellationToken ct = default)
    {
        var warehouse = await RequireWarehouseAsync(warehouseNo, ct);

        var rows = await _db.InvRacks.AsNoTracking()
            .Where(r => r.WarehouseNo == warehouseNo && r.IsDeleted == Deleted)
            .OrderBy(r => r.RackNo)
            .ToListAsync(ct);

        return rows.Select(r => ToDto(r, warehouse.WarehouseName)).ToList();
    }

    public async Task<Inv2002RackDto> SaveAsync(Inv2002RackDto dto, CancellationToken ct = default)
    {
        var warehouse = await RequireWarehouseAsync(dto.WarehouseNo, ct);

        if (string.IsNullOrWhiteSpace(dto.RackId)) throw new ValidationException("Rack code is required");

        InvRack e;
        if (dto.RackNo is > 0)
        {
            e = await _db.InvRacks.FirstOrDefaultAsync(r => r.RackNo == dto.RackNo.Value && r.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException($"Rack not found: {dto.RackNo}");

            // Re-check through the rack's own warehouse so a foreign rack can't be edited by id.
            await RequireWarehouseAsync(e.WarehouseNo, ct);

            if (!string.Equals(e.RackId, dto.RackId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(dto.WarehouseNo, dto.RackId!, e.RackNo, ct))
            {
                throw new ValidationException($"Rack code already exists: {dto.RackId}");
            }
        }
        else
        {
            if (await CodeTakenAsync(dto.WarehouseNo, dto.RackId!, null, ct))
                throw new ValidationException($"Rack code already exists: {dto.RackId}");

            e = new InvRack
            {
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvRacks.Add(e);
        }

        e.WarehouseNo = dto.WarehouseNo;
        e.RackId = dto.RackId!;
        e.RackName = dto.RackName;
        e.Aisle = dto.Aisle;
        e.Rack = dto.Rack;
        e.Shelf = dto.Shelf;
        e.Bin = dto.Bin;
        e.IsActive = dto.IsActive ?? 1;
        e.UpdatedBy = _ctx.CurrentUserNo();
        e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(e, warehouse.WarehouseName);
    }

    public async Task DeleteAsync(long rackNo, CancellationToken ct = default)
    {
        var e = await _db.InvRacks.FirstOrDefaultAsync(r => r.RackNo == rackNo && r.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException($"Rack not found: {rackNo}");

        await RequireWarehouseAsync(e.WarehouseNo, ct);

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<bool> CodeTakenAsync(long warehouseNo, string rackId, long? excludeNo, CancellationToken ct) =>
        await _db.InvRacks.AnyAsync(
            r => r.WarehouseNo == warehouseNo && r.RackId == rackId && r.IsDeleted == Deleted
                 && (excludeNo == null || r.RackNo != excludeNo), ct);

    private async Task<InvWarehouse> RequireWarehouseAsync(long warehouseNo, CancellationToken ct)
    {
        if (warehouseNo <= 0) throw new ValidationException("Warehouse is required");

        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");

        return await _db.InvWarehouses.AsNoTracking()
                   .FirstOrDefaultAsync(w => w.WarehouseNo == warehouseNo && w.CompanyNo == companyNo
                                             && w.IsDeleted == Deleted, ct)
               ?? throw new NotFoundException("Warehouse not found");
    }

    private static Inv2002RackDto ToDto(InvRack r, string? warehouseName) => new()
    {
        RackNo = r.RackNo,
        WarehouseNo = r.WarehouseNo,
        WarehouseName = warehouseName,
        RackId = r.RackId,
        RackName = r.RackName,
        Aisle = r.Aisle,
        Rack = r.Rack,
        Shelf = r.Shelf,
        Bin = r.Bin,
        IsActive = r.IsActive,
        RowVersion = r.RowVersion
    };
}
