using System.Globalization;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sal.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Application.Services;

public interface ISal1104Service
{
    Task<Sal1104LookupDto> GetLookupsAsync(CancellationToken ct = default);
    Task<List<Sal1104PromotionDto>> GetListAsync(CancellationToken ct = default);
    Task<Sal1104PromotionDto> GetDetailAsync(long promotionNo, CancellationToken ct = default);
    Task<Sal1104PromotionDto> SaveAsync(Sal1104PromotionDto dto, CancellationToken ct = default);
    Task DeleteAsync(long promotionNo, CancellationToken ct = default);
}

/// <summary>
/// SAL_1104 Promotions. The rules the till applies automatically at pricing time.
/// Validation here is about making a rule that can only mean one thing — a promotion with
/// contradictory conditions is worse than no promotion, because it silently misprices.
/// </summary>
public class Sal1104Service : ISal1104Service
{
    private const short Deleted = 0;
    private const short TypeLinePct = 1, TypeLineAmount = 2, TypeBillPct = 3,
                        TypeBillAmount = 4, TypeBuyXGetY = 5;
    private const short ScopeProduct = 1, ScopeCategory = 2, ScopeBrand = 3, ScopeAll = 4, ScopeCustomer = 5;

    /// <summary>Types the pricing engine fires. Others save but stay inert.</summary>
    private static readonly short[] Supported = { TypeLinePct, TypeLineAmount, TypeBillPct, TypeBillAmount, TypeBuyXGetY };

    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvLookup _invLookup;

    public Sal1104Service(ISalDbContext db, ICompanyBranchContext ctx, IInvLookup invLookup)
    {
        _db = db;
        _ctx = ctx;
        _invLookup = invLookup;
    }

    public async Task<Sal1104LookupDto> GetLookupsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        var products = await _invLookup.GetProductsAsync(companyNo, ct);
        var categories = await _invLookup.GetCategoriesAsync(companyNo, ct);
        var brands = await _invLookup.GetBrandsAsync(companyNo, ct);

        return new Sal1104LookupDto
        {
            Products = products.Select(p => new SalOptionDto { No = p.No, Name = p.Name }).ToList(),
            Categories = categories.Select(c => new SalOptionDto { No = c.No, Name = c.Name }).ToList(),
            Brands = brands.Select(b => new SalOptionDto { No = b.No, Name = b.Name }).ToList()
        };
    }

    public async Task<List<Sal1104PromotionDto>> GetListAsync(CancellationToken ct = default)
    {
        long companyNo = Company();

        var rows = await _db.SalPromotions.AsNoTracking()
            .Where(p => p.CompanyNo == companyNo && p.IsDeleted == Deleted)
            .OrderByDescending(p => p.PromotionNo)
            .ToListAsync(ct);

        return rows.Select(p => ToDto(p, new List<Sal1104TargetDto>())).ToList();
    }

    public async Task<Sal1104PromotionDto> GetDetailAsync(long promotionNo, CancellationToken ct = default)
    {
        var promo = await RequireAsync(promotionNo, ct);

        var targets = await _db.SalPromotionDtls.AsNoTracking()
            .Where(d => d.PromotionNo == promotionNo)
            .OrderBy(d => d.PromotionDtlNo)
            .ToListAsync(ct);

        var productNos = targets.Where(t => t.ProductNo.HasValue).Select(t => t.ProductNo!.Value).Distinct().ToList();
        var names = productNos.Count == 0
            ? new Dictionary<long, string>()
            : (await _invLookup.GetProductsAsync(Company(), ct))
                .Where(p => productNos.Contains(p.No))
                .ToDictionary(p => p.No, p => p.Name);

        return ToDto(promo, targets.Select(t => ToTargetDto(t, names)).ToList());
    }

    public async Task<Sal1104PromotionDto> SaveAsync(Sal1104PromotionDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();
        Validate(dto);

        SalPromotion promo;
        if (dto.PromotionNo is > 0)
        {
            promo = await RequireAsync(dto.PromotionNo.Value, ct);

            if (!string.Equals(promo.PromotionId, dto.PromotionId, StringComparison.OrdinalIgnoreCase)
                && await CodeTakenAsync(companyNo, dto.PromotionId!, promo.PromotionNo, ct))
            {
                throw new ValidationException($"Promotion code already exists: {dto.PromotionId}");
            }
        }
        else
        {
            if (await CodeTakenAsync(companyNo, dto.PromotionId!, null, ct))
                throw new ValidationException($"Promotion code already exists: {dto.PromotionId}");

            promo = new SalPromotion
            {
                CompanyNo = companyNo,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };
            _db.SalPromotions.Add(promo);
        }

        promo.PromotionId = dto.PromotionId!.Trim();
        promo.PromotionName = dto.PromotionName!.Trim();
        promo.PromoType = dto.PromoType;
        promo.ScopeType = dto.ScopeType;
        promo.BranchNo = dto.BranchNo;
        promo.CouponCode = dto.CouponCode;
        promo.Priority = dto.Priority;
        promo.IsStackable = dto.IsStackable;
        promo.MinQty = dto.MinQty;
        promo.MinAmount = dto.MinAmount;
        promo.DiscountPct = dto.DiscountPct;
        promo.DiscountAmount = dto.DiscountAmount;
        promo.BuyQty = dto.BuyQty;
        promo.GetQty = dto.GetQty;
        promo.MaxDiscountAmount = dto.MaxDiscountAmount;
        promo.CustomerType = dto.CustomerType;
        promo.PriceTier = dto.PriceTier;
        promo.StartDate = dto.StartDate.Date;
        promo.EndDate = dto.EndDate.Date;
        promo.StartTime = ParseTime(dto.StartTime);
        promo.EndTime = ParseTime(dto.EndTime);
        promo.WeekdayMask = dto.WeekdayMask;
        promo.UsageLimit = dto.UsageLimit;
        promo.Remarks = dto.Remarks;
        promo.IsActive = dto.IsActive ?? 1;
        promo.UpdatedBy = _ctx.CurrentUserNo();
        promo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await ReplaceTargetsAsync(promo.PromotionNo, dto.Targets, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(promo.PromotionNo, ct);
    }

    public async Task DeleteAsync(long promotionNo, CancellationToken ct = default)
    {
        var promo = await RequireAsync(promotionNo, ct);

        // Sale lines point at the promotion that discounted them; deleting it would orphan that
        // attribution, so a used promotion is deactivated instead.
        bool used = await _db.SalInvoiceDtls.AnyAsync(l => l.PromotionNo == promotionNo, ct);
        if (used) throw new ValidationException("This promotion has been applied to sales — deactivate it instead");

        var targets = await _db.SalPromotionDtls.Where(d => d.PromotionNo == promotionNo).ToListAsync(ct);
        _db.SalPromotionDtls.RemoveRange(targets);

        promo.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── validation ───────────────────────────────────────────────────────────

    private static void Validate(Sal1104PromotionDto d)
    {
        if (string.IsNullOrWhiteSpace(d.PromotionId)) throw new ValidationException("Promotion code is required");
        if (string.IsNullOrWhiteSpace(d.PromotionName)) throw new ValidationException("Promotion name is required");
        if (d.EndDate.Date < d.StartDate.Date) throw new ValidationException("End date cannot precede the start date");

        switch (d.PromoType)
        {
            case TypeLinePct:
            case TypeBillPct:
                if (d.DiscountPct is null or <= 0 or > 100)
                    throw new ValidationException("Percentage must be between 0 and 100");
                break;

            case TypeLineAmount:
            case TypeBillAmount:
                if (d.DiscountAmount is null or <= 0)
                    throw new ValidationException("Discount amount must be greater than zero");
                break;

            case TypeBuyXGetY:
                if (d.BuyQty is null or <= 0) throw new ValidationException("Buy quantity must be greater than zero");
                if (d.GetQty is null or <= 0) throw new ValidationException("Get quantity must be greater than zero");
                break;
        }

        // A product/category/brand-scoped rule with nothing selected would match nothing at all.
        bool needsTargets = d.ScopeType is ScopeProduct or ScopeCategory or ScopeBrand;
        if (needsTargets && !d.Targets.Any(t => t.TargetRole == 1))
            throw new ValidationException("Select at least one item this promotion applies to");

        if (d.MaxDiscountAmount is <= 0)
            throw new ValidationException("Maximum discount must be greater than zero when set");
    }

    // ── targets ──────────────────────────────────────────────────────────────

    private async Task ReplaceTargetsAsync(long promotionNo, List<Sal1104TargetDto> rows, CancellationToken ct)
    {
        var existing = await _db.SalPromotionDtls.Where(d => d.PromotionNo == promotionNo).ToListAsync(ct);
        _db.SalPromotionDtls.RemoveRange(existing);

        foreach (var r in rows ?? new List<Sal1104TargetDto>())
        {
            if (r.ProductNo is null && r.CategoryNo is null && r.BrandNo is null) continue;

            _db.SalPromotionDtls.Add(new SalPromotionDtl
            {
                PromotionNo = promotionNo,
                TargetRole = r.TargetRole == 2 ? (short)2 : (short)1,
                ProductNo = r.ProductNo,
                VariantNo = r.VariantNo,
                CategoryNo = r.CategoryNo,
                BrandNo = r.BrandNo,
                Qty = r.Qty
            });
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static TimeSpan? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : throw new ValidationException($"Invalid time: {value}");

    private async Task<bool> CodeTakenAsync(long companyNo, string promotionId, long? excludeNo,
                                            CancellationToken ct) =>
        await _db.SalPromotions.AnyAsync(
            p => p.CompanyNo == companyNo && p.PromotionId == promotionId && p.IsDeleted == Deleted
                 && (excludeNo == null || p.PromotionNo != excludeNo), ct);

    private async Task<SalPromotion> RequireAsync(long promotionNo, CancellationToken ct)
    {
        var promo = await _db.SalPromotions.FirstOrDefaultAsync(
                        p => p.PromotionNo == promotionNo && p.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Promotion not found");

        if (promo.CompanyNo != Company()) throw new ValidationException("Promotion belongs to another company");
        return promo;
    }

    private static Sal1104PromotionDto ToDto(SalPromotion p, List<Sal1104TargetDto> targets) => new()
    {
        PromotionNo = p.PromotionNo,
        PromotionId = p.PromotionId,
        PromotionName = p.PromotionName,
        PromoType = p.PromoType,
        ScopeType = p.ScopeType,
        BranchNo = p.BranchNo,
        CouponCode = p.CouponCode,
        Priority = p.Priority,
        IsStackable = p.IsStackable,
        MinQty = p.MinQty,
        MinAmount = p.MinAmount,
        DiscountPct = p.DiscountPct,
        DiscountAmount = p.DiscountAmount,
        BuyQty = p.BuyQty,
        GetQty = p.GetQty,
        MaxDiscountAmount = p.MaxDiscountAmount,
        CustomerType = p.CustomerType,
        PriceTier = p.PriceTier,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        StartTime = p.StartTime?.ToString(@"hh\:mm"),
        EndTime = p.EndTime?.ToString(@"hh\:mm"),
        WeekdayMask = p.WeekdayMask,
        UsageLimit = p.UsageLimit,
        UsedCount = p.UsedCount,
        Remarks = p.Remarks,
        IsActive = p.IsActive,
        RowVersion = p.RowVersion,
        IsSupported = Supported.Contains(p.PromoType),
        Targets = targets
    };

    private static Sal1104TargetDto ToTargetDto(SalPromotionDtl d, Dictionary<long, string> productNames) => new()
    {
        PromotionDtlNo = d.PromotionDtlNo,
        TargetRole = d.TargetRole,
        ProductNo = d.ProductNo,
        ProductName = d.ProductNo is null ? null : productNames.GetValueOrDefault(d.ProductNo.Value),
        VariantNo = d.VariantNo,
        CategoryNo = d.CategoryNo,
        BrandNo = d.BrandNo,
        Qty = d.Qty
    };

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
}
