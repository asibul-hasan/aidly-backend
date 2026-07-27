using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Inv.Dto;
using AidlyErp.Domain.Inv;

namespace AidlyErp.Application.Inv.Services;

public interface IInv2002Service
{
    Task<List<Inv2002RackDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv2002RackDto> SaveAsync(Inv2002RackDto dto, CancellationToken ct = default);
    Task DeleteAsync(long rackNo, CancellationToken ct = default);
}

public class Inv2002Service : IInv2002Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Inv2002Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Inv2002RackDto>> GetListAsync(CancellationToken ct = default) =>
        await _db.InvRacks.AsNoTracking()
            .Where(r => r.IsDeleted == 0)
            .OrderBy(r => r.RackNo)
            .Select(r => new Inv2002RackDto
            {
                RackNo = r.RackNo,
                RackCode = r.RackCode,
                RackName = r.RackName,
                WarehouseNo = r.WarehouseNo,
                IsActive = r.IsActive
            })
            .ToListAsync(ct);

    public async Task<Inv2002RackDto> SaveAsync(Inv2002RackDto dto, CancellationToken ct = default)
    {
        if (dto.RackNo > 0)
        {
            var entity = await _db.InvRacks.FirstOrDefaultAsync(r => r.RackNo == dto.RackNo && r.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Rack not found: {dto.RackNo}");
            if (dto.RackCode != null) entity.RackCode = dto.RackCode;
            if (dto.RackName != null) entity.RackName = dto.RackName;
            entity.WarehouseNo = dto.WarehouseNo;
            entity.IsActive = dto.IsActive;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return MapToDto(entity);
        }

        if (string.IsNullOrWhiteSpace(dto.RackName)) throw new ValidationException("Rack name is required");
        var newRack = new InvRack
        {
            RackCode = dto.RackCode ?? "",
            RackName = dto.RackName,
            WarehouseNo = dto.WarehouseNo,
            IsActive = dto.IsActive > 0 ? dto.IsActive : (short)1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.InvRacks.Add(newRack);
        await _db.SaveChangesAsync(ct);
        return MapToDto(newRack);
    }

    public async Task DeleteAsync(long rackNo, CancellationToken ct = default)
    {
        var entity = await _db.InvRacks.FirstOrDefaultAsync(r => r.RackNo == rackNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Rack not found: {rackNo}");
        entity.IsDeleted = 1; entity.IsActive = 0;
        entity.DeletedBy = _ctx.CurrentUserNo(); entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static Inv2002RackDto MapToDto(InvRack r) => new()
    {
        RackNo = r.RackNo,
        RackCode = r.RackCode,
        RackName = r.RackName,
        WarehouseNo = r.WarehouseNo,
        IsActive = r.IsActive
    };
}
