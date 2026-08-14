using AidlyErp.Inv.Contracts;
using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;

namespace AidlyErp.Sal.Application.Services;

public interface ISalPricingService
{
    Task<SalQuotePriceResultDto> QuoteAsync(SalQuotePriceRequestDto request, CancellationToken ct = default);
}

/// <summary>
/// Prices a cart server-side, in the fixed order sal-business §3.1 lays down: base price → line
/// discount → (promotions, POS-5) → bill discount → tax → round-off. The till sends quantities and
/// intent; every figure it displays comes back from here, so a tampered or stale client cannot
/// change what a sale is worth.
/// </summary>
public class SalPricingService : ISalPricingService
{
    private const short Deleted = 0;
    private const short DiscountAmount = 1, DiscountPercent = 2;

    /// <summary>Money is held to 4 dp internally; the grand total rounds to 2 for tender.</summary>
    private const int MoneyScale = 4;

    private readonly ICompanyBranchContext _ctx;
    private readonly IInvCatalog _catalog;
    private readonly IVatTaxLookup _vatTaxLookup;
    private readonly ISalPromotionEngine _promotions;

    public SalPricingService(ICompanyBranchContext ctx, IInvCatalog catalog, IVatTaxLookup vatTaxLookup,
                             ISalPromotionEngine promotions)
    {
        _ctx = ctx;
        _catalog = catalog;
        _vatTaxLookup = vatTaxLookup;
        _promotions = promotions;
    }

    public async Task<SalQuotePriceResultDto> QuoteAsync(SalQuotePriceRequestDto request,
                                                         CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

        var lines = (request.Lines ?? new()).Where(l => l.ProductNo > 0 && l.Qty > 0).ToList();
        if (lines.Count == 0) throw new ValidationException("Add at least one line");

        var productNos = lines.Select(l => l.ProductNo).Distinct().ToList();
        var pricing = await _catalog.GetPricingAsync(productNos, companyNo, ct);
        var products = await _catalog.GetProductsAsync(productNos, companyNo, ct);
        var factors = await _catalog.GetUomFactorsAsync(productNos, ct);
        var taxRates = await TaxRatesAsync(pricing.Values.Where(p => p.VatTaxNo.HasValue)
                                                         .Select(p => p.VatTaxNo!.Value), ct);

        // ── steps 1–2: base price and line discount ──────────────────────────
        var priced = new List<SalPricedLineDto>();
        foreach (var l in lines)
        {
            if (!pricing.TryGetValue(l.ProductNo, out var price))
                throw new NotFoundException($"Product not found: {l.ProductNo}");
            if (!products.TryGetValue(l.ProductNo, out var product))
                throw new NotFoundException($"Product not found: {l.ProductNo}");

            decimal unitPrice = l.UnitPrice is > 0 ? l.UnitPrice.Value : price.SalePrice;

            // The floor exists so a cashier cannot quietly sell below cost-plus-margin.
            if (price.MinSalePrice is > 0 && unitPrice < price.MinSalePrice && !request.AllowBelowMinPrice)
                throw new ValidationException(
                    $"{price.ProductName}: price {unitPrice:0.##} is below the minimum {price.MinSalePrice:0.##} — needs approval");

            decimal gross = Round(l.Qty * unitPrice);
            decimal lineDiscount = l.LineDiscountType == DiscountPercent
                ? Round(gross * l.LineDiscountValue / 100m)
                : Round(l.LineDiscountValue);

            if (lineDiscount < 0) throw new ValidationException("A discount cannot be negative");
            if (lineDiscount > gross) throw new ValidationException($"{price.ProductName}: discount exceeds the line value");
            if (lineDiscount > 0 && price.AllowDiscount == 0)
                throw new ValidationException($"{price.ProductName} is not discountable");

            long uomNo = l.UomNo ?? product.BaseUomNo;
            priced.Add(new SalPricedLineDto
            {
                ProductNo = l.ProductNo,
                ProductName = price.ProductName,
                VariantNo = l.VariantNo,
                UomNo = uomNo,
                Qty = l.Qty,
                QtyBase = ToBaseQty(product, uomNo, l.Qty, factors),
                UnitPrice = unitPrice,
                Mrp = price.Mrp > 0 ? price.Mrp : null,
                LineDiscountAmount = lineDiscount,
                VatTaxNo = price.VatTaxNo,
                TaxRatePct = price.VatTaxNo is null ? 0m : taxRates.GetValueOrDefault(price.VatTaxNo.Value),
                IsTaxInclusive = price.IsTaxInclusive,
                Remarks = l.Remarks
            });
        }

        decimal subTotal = priced.Sum(p => Round(p.Qty * p.UnitPrice));
        decimal lineDiscountTotal = priced.Sum(p => p.LineDiscountAmount);

        // ── step 3: automatic promotions ─────────────────────────────────────
        var appliedPromotions = await ApplyPromotionsAsync(priced, products, pricing, factors, request, ct);

        decimal promotionTotal = priced.Sum(p => p.PromotionDiscount);
        decimal netAfterLine = subTotal - lineDiscountTotal - promotionTotal;

        // ── step 4: bill discount, spread back over lines by net value ───────
        decimal billDiscount = request.BillDiscountType == DiscountPercent
            ? Round(netAfterLine * request.BillDiscountValue / 100m)
            : Round(request.BillDiscountValue);

        if (billDiscount < 0) throw new ValidationException("A discount cannot be negative");
        if (billDiscount > netAfterLine) throw new ValidationException("Bill discount exceeds the cart value");

        // Spreading it (rather than subtracting at the footer) is what keeps per-line tax and
        // margin correct on a discounted sale.
        decimal basis = netAfterLine > 0 ? netAfterLine : 1m;
        decimal allocated = 0m;
        for (int i = 0; i < priced.Count; i++)
        {
            var p = priced[i];
            decimal lineNet = Round(p.Qty * p.UnitPrice) - p.LineDiscountAmount - p.PromotionDiscount;

            // Last line absorbs the rounding remainder so the shares sum exactly to the discount.
            p.BillDiscountShare = i == priced.Count - 1
                ? billDiscount - allocated
                : Round(billDiscount * lineNet / basis);
            allocated += p.BillDiscountShare;
        }

        // ── steps 5–6: tax per line, then round-off ──────────────────────────
        decimal taxableTotal = 0m, taxTotal = 0m;
        foreach (var p in priced)
        {
            decimal net = Round(p.Qty * p.UnitPrice) - p.LineDiscountAmount - p.PromotionDiscount
                          - p.BillDiscountShare;

            if (p.IsTaxInclusive == 1 && p.TaxRatePct > 0)
            {
                // The price already contains the tax, so extract it rather than adding on top.
                p.TaxableAmount = Round(net / (1 + p.TaxRatePct / 100m));
                p.TaxAmount = net - p.TaxableAmount;
            }
            else
            {
                p.TaxableAmount = net;
                p.TaxAmount = Round(net * p.TaxRatePct / 100m);
            }

            p.LineTotal = p.TaxableAmount + p.TaxAmount;
            taxableTotal += p.TaxableAmount;
            taxTotal += p.TaxAmount;
        }

        decimal beforeRounding = taxableTotal + taxTotal + request.ShippingCharge;
        decimal grandTotal = Math.Round(beforeRounding, 2, MidpointRounding.AwayFromZero);

        return new SalQuotePriceResultDto
        {
            SubTotal = subTotal,
            LineDiscountTotal = lineDiscountTotal,
            PromotionDiscount = promotionTotal,
            AppliedPromotions = appliedPromotions,
            BillDiscountAmount = billDiscount,
            TaxableAmount = taxableTotal,
            TaxAmount = taxTotal,
            ShippingCharge = request.ShippingCharge,
            RoundOff = grandTotal - beforeRounding,
            GrandTotal = grandTotal,
            Lines = priced
        };
    }

    // ── promotions ───────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the promotion engine over the priced lines, writes the discount it awarded onto each
    /// line, and appends any BuyXGetY giveaways as zero-priced lines. Returns the promotions that
    /// fired so the confirm path can bump their usage counts.
    /// </summary>
    private async Task<List<long>> ApplyPromotionsAsync(
        List<SalPricedLineDto> priced,
        IReadOnlyDictionary<long, ProductInfo> products,
        IReadOnlyDictionary<long, ProductPricingInfo> pricing,
        IReadOnlyDictionary<(long ProductNo, long UomNo), decimal> factors,
        SalQuotePriceRequestDto request,
        CancellationToken ct)
    {
        var candidates = priced
            .Select((p, i) =>
            {
                var info = pricing.GetValueOrDefault(p.ProductNo);
                return new PromotionCandidateLine(
                    i, p.ProductNo, p.VariantNo, p.UomNo,
                    info?.CategoryNo, info?.BrandNo,
                    p.Qty, Round(p.Qty * p.UnitPrice) - p.LineDiscountAmount);
            })
            .ToList();

        decimal cartNet = candidates.Sum(c => c.NetValue);
        var outcome = await _promotions.EvaluateAsync(candidates, request.CustomerNo, cartNet, ct);

        var applied = new List<long>();

        foreach (var (index, hit) in outcome.LineHits)
        {
            priced[index].PromotionDiscount = hit.Discount;
            priced[index].PromotionNo = hit.PromotionNo;
            applied.Add(hit.PromotionNo);
        }

        // A giveaway is a real line: it moves stock and prints on the receipt at zero.
        foreach (var free in outcome.FreeLines)
        {
            if (!products.TryGetValue(free.ProductNo, out var product)) continue;

            priced.Add(new SalPricedLineDto
            {
                ProductNo = free.ProductNo,
                ProductName = product.ProductName,
                VariantNo = free.VariantNo,
                UomNo = free.UomNo,
                Qty = free.Qty,
                QtyBase = ToBaseQty(product, free.UomNo, free.Qty, factors),
                UnitPrice = 0m,
                PromotionNo = free.PromotionNo,
                IsFreeItem = 1,
            });
            applied.Add(free.PromotionNo);
        }

        if (outcome.BillPromotionNo is not null && outcome.BillDiscount > 0)
        {
            // Folded into the request's bill discount so it distributes over lines the same way a
            // manual one does — otherwise per-line tax on a promoted basket comes out wrong.
            request.BillDiscountType = 1;
            request.BillDiscountValue += outcome.BillDiscount;
            applied.Add(outcome.BillPromotionNo.Value);
        }

        return applied.Distinct().ToList();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static decimal Round(decimal value) => Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    private async Task<Dictionary<long, decimal>> TaxRatesAsync(IEnumerable<long> vatTaxNos, CancellationToken ct) =>
        await _vatTaxLookup.GetTaxRatesAsync(vatTaxNos.Distinct(), ct);

    private static decimal ToBaseQty(ProductInfo product, long uomNo, decimal qty,
                                     IReadOnlyDictionary<(long ProductNo, long UomNo), decimal> factors)
    {
        if (uomNo <= 0) throw new ValidationException("UOM is required");
        if (uomNo == product.BaseUomNo) return qty;

        if (!factors.TryGetValue((product.ProductNo, uomNo), out decimal factor))
            throw new ValidationException($"No UOM conversion defined for product {product.ProductId}");

        return qty * factor;
    }
}
