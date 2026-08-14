using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sal.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Application.Services;

/// <summary>What a promotion did to a cart, so the caller can attribute the discount per line.</summary>
public sealed record PromotionOutcome(
    IReadOnlyDictionary<int, PromotionHit> LineHits,
    decimal BillDiscount,
    long? BillPromotionNo,
    IReadOnlyList<PromotionFreeLine> FreeLines);

public sealed record PromotionHit(long PromotionNo, decimal Discount);

/// <summary>A giveaway line a BuyXGetY rule added: real stock movement, zero price.</summary>
public sealed record PromotionFreeLine(long ProductNo, long? VariantNo, long UomNo, decimal Qty, long PromotionNo);

public interface ISalPromotionEngine
{
    Task<PromotionOutcome> EvaluateAsync(IReadOnlyList<PromotionCandidateLine> lines, long? customerNo,
                                         decimal cartNet, CancellationToken ct = default);

    /// <summary>Bumps <c>used_count</c> once a sale that used these promotions is confirmed.</summary>
    Task RecordUsageAsync(IEnumerable<long> promotionNos, CancellationToken ct = default);
}

/// <summary>A priced cart line, reduced to what promotion matching needs.</summary>
public sealed record PromotionCandidateLine(
    int Index, long ProductNo, long? VariantNo, long UomNo, long? CategoryNo, long? BrandNo,
    decimal Qty, decimal NetValue);

/// <summary>
/// Step 3 of pricing: automatic promotions.
///
/// <para>The rules it obeys, in order: only live promotions (date, time-of-day, weekday, usage
/// limit, branch, customer) are considered; they run highest-priority first; a non-stackable hit
/// closes that line to any further promotion, while stackable ones accumulate.</para>
///
/// <para>Types 6 (QtyBreak), 7 (Coupon) and 8 (Bundle) are <b>skipped</b>, not approximated. A
/// half-implemented discount rule that silently fires on real money is worse than one that visibly
/// does nothing.</para>
/// </summary>
public class SalPromotionEngine : ISalPromotionEngine
{
    private const short Deleted = 0;

    private const short TypeLinePct = 1, TypeLineAmount = 2, TypeBillPct = 3,
                        TypeBillAmount = 4, TypeBuyXGetY = 5;

    private const short ScopeProduct = 1, ScopeCategory = 2, ScopeBrand = 3, ScopeAll = 4, ScopeCustomer = 5;

    private const short RoleCondition = 1, RoleReward = 2;

    /// <summary>The types the engine will actually fire. Everything else is ignored.</summary>
    private static readonly short[] Supported = { TypeLinePct, TypeLineAmount, TypeBillPct, TypeBillAmount, TypeBuyXGetY };

    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public SalPromotionEngine(ISalDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<PromotionOutcome> EvaluateAsync(IReadOnlyList<PromotionCandidateLine> lines,
                                                      long? customerNo, decimal cartNet,
                                                      CancellationToken ct = default)
    {
        var empty = new PromotionOutcome(new Dictionary<int, PromotionHit>(), 0m, null,
                                         Array.Empty<PromotionFreeLine>());
        if (lines.Count == 0) return empty;

        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long? branchNo = _ctx.CurrentBranchNo();
        var now = DateTime.Now;

        var live = await LivePromotionsAsync(companyNo, branchNo, now, ct);
        if (live.Count == 0) return empty;

        var customer = customerNo is > 0
            ? await _db.SalCustomers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerNo == customerNo, ct)
            : null;

        live = live.Where(p => MatchesCustomer(p, customer)).ToList();
        if (live.Count == 0) return empty;

        var targets = await TargetsAsync(live.Select(p => p.PromotionNo).ToList(), ct);

        var lineHits = new Dictionary<int, PromotionHit>();
        var lineClosed = new HashSet<int>();
        var freeLines = new List<PromotionFreeLine>();
        decimal billDiscount = 0m;
        long? billPromotionNo = null;

        foreach (var promo in live)
        {
            var promoTargets = targets.GetValueOrDefault(promo.PromotionNo) ?? new List<SalPromotionDtl>();

            switch (promo.PromoType)
            {
                case TypeLinePct:
                case TypeLineAmount:
                    ApplyLineDiscount(promo, promoTargets, lines, lineHits, lineClosed);
                    break;

                case TypeBillPct:
                case TypeBillAmount:
                    // Only one bill-level promotion can win; the first by priority takes it.
                    if (billPromotionNo is null)
                    {
                        decimal amount = BillDiscountFor(promo, cartNet);
                        if (amount > 0)
                        {
                            billDiscount = amount;
                            billPromotionNo = promo.PromotionNo;
                        }
                    }
                    break;

                case TypeBuyXGetY:
                    ApplyBuyXGetY(promo, promoTargets, lines, freeLines);
                    break;

                // 6 QtyBreak, 7 Coupon, 8 Bundle — not implemented; deliberately inert.
                default:
                    break;
            }
        }

        return new PromotionOutcome(lineHits, billDiscount, billPromotionNo, freeLines);
    }

    public async Task RecordUsageAsync(IEnumerable<long> promotionNos, CancellationToken ct = default)
    {
        var nos = promotionNos.Distinct().ToList();
        if (nos.Count == 0) return;

        var promos = await _db.SalPromotions.Where(p => nos.Contains(p.PromotionNo)).ToListAsync(ct);
        foreach (var p in promos) p.UsedCount += 1;

        await _db.SaveChangesAsync(ct);
    }

    // ── application ──────────────────────────────────────────────────────────

    private static void ApplyLineDiscount(SalPromotion promo, List<SalPromotionDtl> targets,
                                          IReadOnlyList<PromotionCandidateLine> lines,
                                          Dictionary<int, PromotionHit> hits, HashSet<int> closed)
    {
        foreach (var line in lines)
        {
            if (closed.Contains(line.Index)) continue;
            if (!Matches(promo, targets, line, RoleCondition)) continue;
            if (promo.MinQty is > 0 && line.Qty < promo.MinQty) continue;
            if (promo.MinAmount is > 0 && line.NetValue < promo.MinAmount) continue;

            decimal discount = promo.PromoType == TypeLinePct
                ? Round(line.NetValue * (promo.DiscountPct ?? 0m) / 100m)
                : Round(promo.DiscountAmount ?? 0m);

            if (promo.MaxDiscountAmount is > 0) discount = Math.Min(discount, promo.MaxDiscountAmount.Value);

            // Never let a rule hand back more than the line is worth.
            decimal already = hits.GetValueOrDefault(line.Index)?.Discount ?? 0m;
            discount = Math.Min(discount, line.NetValue - already);
            if (discount <= 0) continue;

            hits[line.Index] = new PromotionHit(promo.PromotionNo, already + discount);

            // A non-stackable win ends the bidding for this line.
            if (promo.IsStackable != 1) closed.Add(line.Index);
        }
    }

    private static decimal BillDiscountFor(SalPromotion promo, decimal cartNet)
    {
        if (promo.MinAmount is > 0 && cartNet < promo.MinAmount) return 0m;

        decimal discount = promo.PromoType == TypeBillPct
            ? Round(cartNet * (promo.DiscountPct ?? 0m) / 100m)
            : Round(promo.DiscountAmount ?? 0m);

        if (promo.MaxDiscountAmount is > 0) discount = Math.Min(discount, promo.MaxDiscountAmount.Value);
        return Math.Min(discount, cartNet);
    }

    /// <summary>
    /// Buy X, get Y free. The reward is added as a zero-priced line rather than as a discount, so
    /// the giveaway moves stock and shows on the receipt as the free item it is.
    /// </summary>
    private static void ApplyBuyXGetY(SalPromotion promo, List<SalPromotionDtl> targets,
                                      IReadOnlyList<PromotionCandidateLine> lines,
                                      List<PromotionFreeLine> freeLines)
    {
        decimal buyQty = promo.BuyQty ?? 0m;
        decimal getQty = promo.GetQty ?? 0m;
        if (buyQty <= 0 || getQty <= 0) return;

        decimal qualifying = lines
            .Where(l => Matches(promo, targets, l, RoleCondition))
            .Sum(l => l.Qty);

        // Whole multiples only — buying 5 on a "buy 2 get 1" earns two, not two and a half.
        int times = (int)Math.Floor(qualifying / buyQty);
        if (times <= 0) return;

        var reward = targets.FirstOrDefault(t => t.TargetRole == RoleReward);

        if (reward?.ProductNo is > 0)
        {
            // An explicit reward product: give that, at the UOM of a matching cart line if present.
            long uomNo = lines.FirstOrDefault(l => l.ProductNo == reward.ProductNo)?.UomNo
                         ?? lines[0].UomNo;

            freeLines.Add(new PromotionFreeLine(reward.ProductNo.Value, reward.VariantNo, uomNo,
                                                times * getQty, promo.PromotionNo));
            return;
        }

        // No reward named: the cheapest qualifying line is what goes free, which is the
        // conventional reading of "buy 3 of these, get one free".
        var cheapest = lines
            .Where(l => Matches(promo, targets, l, RoleCondition) && l.Qty > 0)
            .OrderBy(l => l.NetValue / l.Qty)
            .FirstOrDefault();

        if (cheapest is null) return;

        freeLines.Add(new PromotionFreeLine(cheapest.ProductNo, cheapest.VariantNo, cheapest.UomNo,
                                            times * getQty, promo.PromotionNo));
    }

    // ── matching ─────────────────────────────────────────────────────────────

    private static bool Matches(SalPromotion promo, List<SalPromotionDtl> targets,
                                PromotionCandidateLine line, short role)
    {
        if (promo.ScopeType == ScopeAll || promo.ScopeType == ScopeCustomer) return true;

        var rows = targets.Where(t => t.TargetRole == role).ToList();
        if (rows.Count == 0) return false;

        return promo.ScopeType switch
        {
            ScopeProduct => rows.Any(t => t.ProductNo == line.ProductNo
                                          && (t.VariantNo == null || t.VariantNo == line.VariantNo)),
            ScopeCategory => rows.Any(t => t.CategoryNo != null && t.CategoryNo == line.CategoryNo),
            ScopeBrand => rows.Any(t => t.BrandNo != null && t.BrandNo == line.BrandNo),
            _ => false,
        };
    }

    private static bool MatchesCustomer(SalPromotion promo, SalCustomer? customer)
    {
        if (promo.CustomerType is not null && customer?.CustomerType != promo.CustomerType) return false;
        if (promo.PriceTier is not null && customer?.PriceTier != promo.PriceTier) return false;
        return true;
    }

    /// <summary>
    /// Promotions live right now: in date, in the time-of-day window, on today's weekday, under
    /// their usage limit, and for this branch.
    /// </summary>
    private async Task<List<SalPromotion>> LivePromotionsAsync(long companyNo, long? branchNo, DateTime now,
                                                               CancellationToken ct)
    {
        var today = now.Date;

        var candidates = await _db.SalPromotions.AsNoTracking()
            .Where(p => p.CompanyNo == companyNo && p.IsDeleted == Deleted && p.IsActive == 1
                        && p.StartDate <= today && p.EndDate >= today
                        && (p.BranchNo == null || p.BranchNo == branchNo)
                        && (p.UsageLimit == null || p.UsedCount < p.UsageLimit))
            .ToListAsync(ct);

        return candidates
            .Where(p => Supported.Contains(p.PromoType))
            .Where(p => InTimeWindow(p, now.TimeOfDay))
            .Where(p => OnWeekday(p, now.DayOfWeek))
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.PromotionNo)
            .ToList();
    }

    private static bool InTimeWindow(SalPromotion promo, TimeSpan timeOfDay)
    {
        if (promo.StartTime is null || promo.EndTime is null) return true;

        // A window that wraps midnight (22:00–02:00) is two ranges, not one.
        return promo.StartTime <= promo.EndTime
            ? timeOfDay >= promo.StartTime && timeOfDay <= promo.EndTime
            : timeOfDay >= promo.StartTime || timeOfDay <= promo.EndTime;
    }

    private static bool OnWeekday(SalPromotion promo, DayOfWeek day)
    {
        if (promo.WeekdayMask is null or 0) return true;

        // Bit 0 is Monday, so Sunday (DayOfWeek 0) sits at bit 6.
        int bit = day == DayOfWeek.Sunday ? 6 : (int)day - 1;
        return (promo.WeekdayMask.Value & (1 << bit)) != 0;
    }

    private async Task<Dictionary<long, List<SalPromotionDtl>>> TargetsAsync(List<long> promotionNos,
                                                                             CancellationToken ct)
    {
        if (promotionNos.Count == 0) return new Dictionary<long, List<SalPromotionDtl>>();

        var rows = await _db.SalPromotionDtls.AsNoTracking()
            .Where(d => promotionNos.Contains(d.PromotionNo))
            .ToListAsync(ct);

        return rows.GroupBy(d => d.PromotionNo).ToDictionary(g => g.Key, g => g.ToList());
    }

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
