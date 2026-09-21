using AidlyErp.Inv.Application.Dto;
using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

public interface IInv1003Service
{
    Task<List<Inv1003CategoryDto>> GetListAsync(CancellationToken ct = default);
    Task<List<InvOptionDto>> GetParentOptionsAsync(CancellationToken ct = default);
    Task<Inv1003CategoryDto> SaveAsync(Inv1003CategoryDto dto, CancellationToken ct = default);
    Task DeleteAsync(long categoryNo, CancellationToken ct = default);
}

/// <summary>
/// INV_1003 Category — the company-wide product hierarchy. The service owns <c>depth</c> and
/// <c>tree_path</c>: both are recomputed from the parent whenever a node moves, and cascaded to its
/// descendants, so the client never sends them.
/// </summary>
public class Inv1003Service : IInv1003Service
{
    private const short Deleted = 0;

    /// <summary>Six levels is deep enough for retail and keeps the path column bounded.</summary>
    private const int MaxDepth = 6;

    private readonly IInvDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public Inv1003Service(IInvDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Inv1003CategoryDto>> GetListAsync(CancellationToken ct = default)
    {
        var all = await AllAsync(ct);
        var names = all.ToDictionary(c => c.CategoryNo, c => c.CategoryName);
        return all.Select(c => ToDto(c, names)).ToList();
    }

    /// <summary>Parent picker source — the caller excludes itself and its descendants client-side.</summary>
    public async Task<List<InvOptionDto>> GetParentOptionsAsync(CancellationToken ct = default)
    {
        var all = await AllAsync(ct);
        return all.Select(c => new InvOptionDto(c.CategoryNo, c.CategoryName)).ToList();
    }

    public async Task<Inv1003CategoryDto> SaveAsync(Inv1003CategoryDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();

        if (string.IsNullOrWhiteSpace(dto.CategoryId)) throw new ValidationException("Category code is required");
        if (string.IsNullOrWhiteSpace(dto.CategoryName)) throw new ValidationException("Category name is required");

        if (dto.ParentCategoryNo is > 0)
        {
            bool parentExists = await _db.InvCategories.AnyAsync(
                c => c.CategoryNo == dto.ParentCategoryNo && c.CompanyNo == companyNo && c.IsDeleted == Deleted, ct);
            if (!parentExists) throw new NotFoundException("Parent category not found");

            await AssertNoCycleAsync(dto.CategoryNo, dto.ParentCategoryNo.Value, ct);
        }

        InvCategory e;
        if (dto.CategoryNo is > 0)
        {
            e = await _db.InvCategories.FirstOrDefaultAsync(
                    c => c.CategoryNo == dto.CategoryNo.Value && c.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Category not found");

            if (e.CompanyNo != companyNo) throw new ValidationException("Category belongs to another company");

            if (!string.Equals(e.CategoryId, dto.CategoryId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(companyNo, dto.CategoryId!, e.CategoryNo, ct))
            {
                throw new ValidationException($"Category code already exists: {dto.CategoryId}");
            }
        }
        else
        {
            if (await CodeTakenAsync(companyNo, dto.CategoryId!, null, ct))
                throw new ValidationException($"Category code already exists: {dto.CategoryId}");

            e = new InvCategory
            {
                CompanyNo = companyNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.InvCategories.Add(e);
        }

        e.CategoryId = dto.CategoryId!;
        e.CategoryName = dto.CategoryName!;
        e.CategoryNameNls = dto.CategoryNameNls;
        e.ParentCategoryNo = dto.ParentCategoryNo is > 0 ? dto.ParentCategoryNo : null;
        e.DefaultVatTaxNo = dto.DefaultVatTaxNo;
        e.ImagePath = dto.ImagePath;
        e.OrderSl = dto.OrderSl ?? 0;
        e.Remarks = dto.Remarks;
        e.IsActive = dto.IsActive ?? 1;
        e.UpdatedBy = _ctx.CurrentUserNo();
        e.UpdatedAt = DateTime.UtcNow;

        // The node needs its PK before its own tree_path can be built.
        await _db.SaveChangesAsync(ct);

        await RecomputeAsync(e, ct);
        await _db.SaveChangesAsync(ct);

        var names = (await AllAsync(ct)).ToDictionary(c => c.CategoryNo, c => c.CategoryName);
        return ToDto(e, names);
    }

    public async Task DeleteAsync(long categoryNo, CancellationToken ct = default)
    {
        long companyNo = Company();

        var e = await _db.InvCategories.FirstOrDefaultAsync(
                    c => c.CategoryNo == categoryNo && c.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Category not found");

        if (e.CompanyNo != companyNo) throw new ValidationException("Category belongs to another company");

        bool hasChildren = await _db.InvCategories.AnyAsync(
            c => c.ParentCategoryNo == categoryNo && c.IsDeleted == Deleted, ct);
        if (hasChildren) throw new ValidationException("Remove or reassign the child categories first");

        bool hasProducts = await _db.InvProducts.AnyAsync(
            p => p.CategoryNo == categoryNo && p.IsDeleted == Deleted, ct);
        if (hasProducts) throw new ValidationException("Products are assigned to this category — reassign them first");

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── hierarchy ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes <c>depth</c> and <c>tree_path</c> from the parent, then cascades to descendants —
    /// re-parenting a node moves its whole subtree.
    /// </summary>
    private async Task RecomputeAsync(InvCategory c, CancellationToken ct)
    {
        short depth;
        string path;

        if (c.ParentCategoryNo is null)
        {
            depth = 0;
            path = $"/{c.CategoryNo}/";
        }
        else
        {
            var parent = await _db.InvCategories.FirstOrDefaultAsync(
                    p => p.CategoryNo == c.ParentCategoryNo && p.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Parent category not found");

            depth = (short)(parent.Depth + 1);
            path = (parent.TreePath ?? "/") + c.CategoryNo + "/";
        }

        if (depth > MaxDepth)
            throw new ValidationException($"Category tree exceeds the maximum depth of {MaxDepth}");

        c.Depth = depth;
        c.TreePath = path;

        var children = await _db.InvCategories
            .Where(x => x.ParentCategoryNo == c.CategoryNo && x.IsDeleted == Deleted)
            .ToListAsync(ct);

        foreach (var child in children) await RecomputeAsync(child, ct);
    }

    /// <summary>
    /// Refuses a parent that is the node itself or one of its descendants — either would detach the
    /// subtree from the root and loop forever on recompute.
    /// </summary>
    private async Task AssertNoCycleAsync(long? categoryNo, long parentNo, CancellationToken ct)
    {
        if (categoryNo is null or <= 0) return;
        if (categoryNo == parentNo) throw new ValidationException("A category cannot be its own parent");

        long? walk = parentNo;
        // Bounded by MaxDepth+1 so a pre-existing bad row can't spin here.
        for (int hops = 0; walk is not null && hops <= MaxDepth + 1; hops++)
        {
            if (walk == categoryNo)
                throw new ValidationException("Cannot move a category under its own descendant");

            walk = await _db.InvCategories.AsNoTracking()
                .Where(c => c.CategoryNo == walk && c.IsDeleted == Deleted)
                .Select(c => c.ParentCategoryNo)
                .FirstOrDefaultAsync(ct);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<List<InvCategory>> AllAsync(CancellationToken ct)
    {
        long companyNo = Company();
        return await _db.InvCategories.AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted)
            .OrderBy(c => c.CategoryNo)
            .ToListAsync(ct);
    }

    private async Task<bool> CodeTakenAsync(long companyNo, string categoryId, long? excludeNo, CancellationToken ct) =>
        await _db.InvCategories.AnyAsync(
            c => c.CompanyNo == companyNo && c.CategoryId == categoryId && c.IsDeleted == Deleted
                 && (excludeNo == null || c.CategoryNo != excludeNo), ct);

    private static Inv1003CategoryDto ToDto(InvCategory e, Dictionary<long, string> names) => new()
    {
        CategoryNo = e.CategoryNo,
        CategoryId = e.CategoryId,
        CategoryName = e.CategoryName,
        CategoryNameNls = e.CategoryNameNls,
        ParentCategoryNo = e.ParentCategoryNo,
        ParentCategoryName = e.ParentCategoryNo is null ? null : names.GetValueOrDefault(e.ParentCategoryNo.Value),
        DefaultVatTaxNo = e.DefaultVatTaxNo,
        Depth = e.Depth,
        TreePath = e.TreePath,
        ImagePath = e.ImagePath,
        OrderSl = e.OrderSl,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");
}
