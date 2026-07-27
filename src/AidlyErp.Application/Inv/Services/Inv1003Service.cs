using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Inv.Dto;
using AidlyErp.Domain.Inv;

namespace AidlyErp.Application.Inv.Services;

public interface IInv1003Service
{
    Task<List<Inv1003CategoryDto>> GetListAsync(CancellationToken ct = default);
    Task<Inv1003CategoryDto> SaveAsync(Inv1003CategoryDto dto, CancellationToken ct = default);
    Task DeleteAsync(long categoryNo, CancellationToken ct = default);
}

public class Inv1003Service : IInv1003Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public Inv1003Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<Inv1003CategoryDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.InvCategories.AsNoTracking().Where(c => c.CompanyNo == companyNo && c.IsDeleted == 0).OrderBy(c => c.CategoryName)
            .Select(c => new Inv1003CategoryDto { CategoryNo = c.CategoryNo, CategoryCode = c.CategoryCode, CategoryName = c.CategoryName, ParentCategoryNo = c.ParentCategoryNo, Description = c.Description, IsActive = c.IsActive }).ToListAsync(ct);
    }

    public async Task<Inv1003CategoryDto> SaveAsync(Inv1003CategoryDto dto, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        InvCategory cat;
        if (dto.CategoryNo.HasValue && dto.CategoryNo.Value > 0)
        {
            cat = await _db.InvCategories.FirstOrDefaultAsync(c => c.CategoryNo == dto.CategoryNo.Value && c.IsDeleted == 0, ct) ?? throw new NotFoundException($"Category not found: {dto.CategoryNo}");
            cat.CategoryName = dto.CategoryName!; cat.ParentCategoryNo = dto.ParentCategoryNo; cat.Description = dto.Description; cat.IsActive = dto.IsActive;
        }
        else
        {
            cat = new InvCategory { CompanyNo = companyNo, CategoryCode = dto.CategoryCode ?? $"CAT-{DateTime.UtcNow:MMddHHmmss}", CategoryName = dto.CategoryName!, ParentCategoryNo = dto.ParentCategoryNo, Description = dto.Description, IsActive = dto.IsActive, IsDeleted = 0, CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow };
            _db.InvCategories.Add(cat);
        }
        await _db.SaveChangesAsync(ct);
        dto.CategoryNo = cat.CategoryNo;
        return dto;
    }

    public async Task DeleteAsync(long categoryNo, CancellationToken ct = default)
    {
        var cat = await _db.InvCategories.FirstOrDefaultAsync(c => c.CategoryNo == categoryNo && c.IsDeleted == 0, ct) ?? throw new NotFoundException($"Category not found: {categoryNo}");
        cat.IsDeleted = 1; await _db.SaveChangesAsync(ct);
    }
}
