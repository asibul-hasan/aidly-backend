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

public interface IInv1005Service
{
    Task<List<Inv1005UomDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1005UomDto> SaveAsync(Inv1005UomDto dto, CancellationToken ct = default);
    Task DeleteAsync(long uomNo, CancellationToken ct = default);
}

public class Inv1005Service : IInv1005Service
{
    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Inv1005Service(IInvDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Inv1005UomDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.InvUoms.AsNoTracking().Where(u => u.CompanyNo == companyNo && u.IsDeleted == 0).OrderBy(u => u.UomName)
            .Select(u => new Inv1005UomDto { UomNo = u.UomNo, UomCode = u.UomId, UomName = u.UomName, IsActive = u.IsActive }).ToListAsync(ct);
    }

    public async Task<Inv1005UomDto> SaveAsync(Inv1005UomDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        InvUom uom;
        if (dto.UomNo.HasValue && dto.UomNo.Value > 0)
        {
            uom = await _db.InvUoms.FirstOrDefaultAsync(u => u.UomNo == dto.UomNo.Value && u.IsDeleted == 0, ct) ?? throw new NotFoundException($"UOM not found: {dto.UomNo}");
            uom.UomName = dto.UomName!; uom.IsActive = dto.IsActive;
        }
        else
        {
            uom = new InvUom { CompanyNo = companyNo, UomId = dto.UomCode ?? $"UOM-{DateTime.UtcNow:MMddHHmmss}", UomName = dto.UomName!, IsActive = dto.IsActive, IsDeleted = 0, CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow };
            _db.InvUoms.Add(uom);
        }
        await _db.SaveChangesAsync(ct);
        dto.UomNo = uom.UomNo;
        return dto;
    }

    public async Task DeleteAsync(long uomNo, CancellationToken ct = default)
    {
        var uom = await _db.InvUoms.FirstOrDefaultAsync(u => u.UomNo == uomNo && u.IsDeleted == 0, ct) ?? throw new NotFoundException($"UOM not found: {uomNo}");
        uom.IsDeleted = 1; await _db.SaveChangesAsync(ct);
    }
}
