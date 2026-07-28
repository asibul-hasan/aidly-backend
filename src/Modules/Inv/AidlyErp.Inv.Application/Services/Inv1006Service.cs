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

public interface IInv1006Service
{
    Task<List<Inv1006AttributeDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1006AttributeDto> SaveAsync(Inv1006AttributeDto dto, CancellationToken ct = default);
    Task DeleteAsync(long attributeNo, CancellationToken ct = default);
}

public class Inv1006Service : IInv1006Service
{
    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Inv1006Service(IInvDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Inv1006AttributeDto>> GetListAsync(CancellationToken ct = default) =>
        await _db.InvProductAttributes.AsNoTracking()
            .Where(a => a.IsDeleted == 0)
            .OrderBy(a => a.AttributeNo)
            .Select(a => new Inv1006AttributeDto
            {
                AttributeNo = a.AttributeNo,
                AttributeName = a.AttributeName,
                IsActive = a.IsActive
            })
            .ToListAsync(ct);

    public async Task<Inv1006AttributeDto> SaveAsync(Inv1006AttributeDto dto, CancellationToken ct = default)
    {
        if (dto.AttributeNo > 0)
        {
            var entity = await _db.InvProductAttributes.FirstOrDefaultAsync(a => a.AttributeNo == dto.AttributeNo && a.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Attribute not found: {dto.AttributeNo}");
            if (dto.AttributeName != null) entity.AttributeName = dto.AttributeName;
            entity.IsActive = dto.IsActive;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new Inv1006AttributeDto { AttributeNo = entity.AttributeNo, AttributeName = entity.AttributeName, IsActive = entity.IsActive };
        }

        if (string.IsNullOrWhiteSpace(dto.AttributeName)) throw new ValidationException("Attribute name is required");
        var newAttr = new InvProductAttribute
        {
            AttributeName = dto.AttributeName,
            IsActive = dto.IsActive > 0 ? dto.IsActive : (short)1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.InvProductAttributes.Add(newAttr);
        await _db.SaveChangesAsync(ct);
        return new Inv1006AttributeDto { AttributeNo = newAttr.AttributeNo, AttributeName = newAttr.AttributeName, IsActive = newAttr.IsActive };
    }

    public async Task DeleteAsync(long attributeNo, CancellationToken ct = default)
    {
        var entity = await _db.InvProductAttributes.FirstOrDefaultAsync(a => a.AttributeNo == attributeNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Attribute not found: {attributeNo}");
        entity.IsDeleted = 1; entity.IsActive = 0;
        entity.DeletedBy = _ctx.CurrentUserNo(); entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
