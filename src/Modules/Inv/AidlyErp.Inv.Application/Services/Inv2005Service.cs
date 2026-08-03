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

public interface IInv2005Service
{
    Task<List<Inv2005ReorderDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv2005ReorderDto> SaveAsync(Inv2005ReorderDto dto, CancellationToken ct = default);
    Task DeleteAsync(long reorderNo, CancellationToken ct = default);
}

public class Inv2005Service : IInv2005Service
{
    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Inv2005Service(IInvDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Inv2005ReorderDto>> GetListAsync(CancellationToken ct = default) =>
        await _db.InvReorders.AsNoTracking()
            .Where(r => r.IsDeleted == 0)
            .OrderBy(r => r.ReorderNo)
            .Select(r => new Inv2005ReorderDto
            {
                ReorderNo = r.ReorderNo,
                ProductNo = r.ProductNo,
                WarehouseNo = r.WarehouseNo,
                MinQty = r.MinQty,
                MaxQty = r.MaxQty,
                ReorderQty = r.ReorderQty
            })
            .ToListAsync(ct);

    public async Task<Inv2005ReorderDto> SaveAsync(Inv2005ReorderDto dto, CancellationToken ct = default)
    {
        if (dto.ReorderNo > 0)
        {
            var entity = await _db.InvReorders.FirstOrDefaultAsync(r => r.ReorderNo == dto.ReorderNo && r.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Reorder level not found: {dto.ReorderNo}");
            entity.ProductNo = dto.ProductNo;
            entity.WarehouseNo = dto.WarehouseNo;
            entity.MinQty = dto.MinQty;
            entity.MaxQty = dto.MaxQty;
            entity.ReorderQty = dto.ReorderQty;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return MapToDto(entity);
        }

        var newReorder = new InvReorder
        {
            ProductNo = dto.ProductNo,
            WarehouseNo = dto.WarehouseNo,
            MinQty = dto.MinQty,
            MaxQty = dto.MaxQty,
            ReorderQty = dto.ReorderQty,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.InvReorders.Add(newReorder);
        await _db.SaveChangesAsync(ct);
        return MapToDto(newReorder);
    }

    public async Task DeleteAsync(long reorderNo, CancellationToken ct = default)
    {
        var entity = await _db.InvReorders.FirstOrDefaultAsync(r => r.ReorderNo == reorderNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Reorder level not found: {reorderNo}");
        entity.IsDeleted = 1; entity.IsActive = 0;
        entity.DeletedBy = _ctx.CurrentUserNo(); entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Inv2005ReorderDto MapToDto(InvReorder r) => new()
    {
        ReorderNo = r.ReorderNo,
        ProductNo = r.ProductNo,
        WarehouseNo = r.WarehouseNo,
        MinQty = r.MinQty,
        MaxQty = r.MaxQty,
        ReorderQty = r.ReorderQty
    };
}
