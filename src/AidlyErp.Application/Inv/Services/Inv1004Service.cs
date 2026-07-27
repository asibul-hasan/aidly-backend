using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Inv.Dto;
using AidlyErp.Domain.Inv;

namespace AidlyErp.Application.Inv.Services;

public interface IInv1004Service
{
    Task<List<Inv1004BrandDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1004BrandDto> SaveAsync(Inv1004BrandDto dto, CancellationToken ct = default);
    Task DeleteAsync(long brandNo, CancellationToken ct = default);
}

public class Inv1004Service : IInv1004Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Inv1004Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Inv1004BrandDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.InvBrands.AsNoTracking().Where(b => b.CompanyNo == companyNo && b.IsDeleted == 0).OrderBy(b => b.BrandName)
            .Select(b => new Inv1004BrandDto { BrandNo = b.BrandNo, BrandCode = b.BrandCode, BrandName = b.BrandName, IsActive = b.IsActive }).ToListAsync(ct);
    }

    public async Task<Inv1004BrandDto> SaveAsync(Inv1004BrandDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        InvBrand brand;
        if (dto.BrandNo.HasValue && dto.BrandNo.Value > 0)
        {
            brand = await _db.InvBrands.FirstOrDefaultAsync(b => b.BrandNo == dto.BrandNo.Value && b.IsDeleted == 0, ct) ?? throw new NotFoundException($"Brand not found: {dto.BrandNo}");
            brand.BrandName = dto.BrandName!; brand.IsActive = dto.IsActive;
        }
        else
        {
            brand = new InvBrand { CompanyNo = companyNo, BrandCode = dto.BrandCode ?? $"BRD-{DateTime.UtcNow:MMddHHmmss}", BrandName = dto.BrandName!, IsActive = dto.IsActive, IsDeleted = 0, CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow };
            _db.InvBrands.Add(brand);
        }
        await _db.SaveChangesAsync(ct);
        dto.BrandNo = brand.BrandNo;
        return dto;
    }

    public async Task DeleteAsync(long brandNo, CancellationToken ct = default)
    {
        var brand = await _db.InvBrands.FirstOrDefaultAsync(b => b.BrandNo == brandNo && b.IsDeleted == 0, ct) ?? throw new NotFoundException($"Brand not found: {brandNo}");
        brand.IsDeleted = 1; await _db.SaveChangesAsync(ct);
    }
}
