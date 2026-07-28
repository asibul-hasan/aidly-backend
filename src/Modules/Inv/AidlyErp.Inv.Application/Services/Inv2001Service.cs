using AidlyErp.Inv.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Domain;

namespace AidlyErp.Inv.Application.Services;

public interface IInv2001Service
{
    Task<List<Inv2001WarehouseDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv2001WarehouseDto> SaveAsync(Inv2001WarehouseDto dto, CancellationToken ct = default);
    Task DeleteAsync(long warehouseNo, CancellationToken ct = default);
}

public class Inv2001Service : IInv2001Service
{
    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Inv2001Service(IInvDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Inv2001WarehouseDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.InvWarehouses.AsNoTracking().Where(w => w.CompanyNo == companyNo && w.IsDeleted == 0).OrderBy(w => w.WarehouseName)
            .Select(w => new Inv2001WarehouseDto { WarehouseNo = w.WarehouseNo, WarehouseCode = w.WarehouseCode, WarehouseName = w.WarehouseName, Location = w.Location, IsActive = w.IsActive }).ToListAsync(ct);
    }

    public async Task<Inv2001WarehouseDto> SaveAsync(Inv2001WarehouseDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        long branchNo = _ctx.CurrentBranchNo() ?? 1;
        InvWarehouse wh;
        if (dto.WarehouseNo.HasValue && dto.WarehouseNo.Value > 0)
        {
            wh = await _db.InvWarehouses.FirstOrDefaultAsync(w => w.WarehouseNo == dto.WarehouseNo.Value && w.IsDeleted == 0, ct) ?? throw new NotFoundException($"Warehouse not found: {dto.WarehouseNo}");
            wh.WarehouseName = dto.WarehouseName!; wh.Location = dto.Location; wh.IsActive = dto.IsActive;
        }
        else
        {
            wh = new InvWarehouse { CompanyNo = companyNo, BranchNo = branchNo, WarehouseCode = dto.WarehouseCode ?? $"WH-{DateTime.UtcNow:MMddHHmmss}", WarehouseName = dto.WarehouseName!, Location = dto.Location, IsActive = dto.IsActive, IsDeleted = 0, CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow };
            _db.InvWarehouses.Add(wh);
        }
        await _db.SaveChangesAsync(ct);
        dto.WarehouseNo = wh.WarehouseNo;
        return dto;
    }

    public async Task DeleteAsync(long warehouseNo, CancellationToken ct = default)
    {
        var wh = await _db.InvWarehouses.FirstOrDefaultAsync(w => w.WarehouseNo == warehouseNo && w.IsDeleted == 0, ct) ?? throw new NotFoundException($"Warehouse not found: {warehouseNo}");
        wh.IsDeleted = 1; await _db.SaveChangesAsync(ct);
    }
}
