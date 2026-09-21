using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Application.Services;

public interface ISalReportService
{
    Task<SalSalesReportDto> GetSalesAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}

/// <summary>
/// SAL_1301 Sales Reports — read-only, over confirmed sales only.
///
/// <para>Margin comes from <c>line_cost</c>, which the stock engine wrote at post time from the
/// costing method in force. That is the honest number: recomputing it now from today's average
/// cost would silently restate history every time a purchase changed the average.</para>
/// </summary>
public class SalReportService : ISalReportService
{
    private const short Deleted = 0;
    private const short SaleConfirmed = 2;

    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvLookup _invLookup;
    private readonly IPartyLookup _partyLookup;

    public SalReportService(ISalDbContext db, ICompanyBranchContext ctx, IInvLookup invLookup,
                            IPartyLookup partyLookup)
    {
        _db = db;
        _ctx = ctx;
        _invLookup = invLookup;
        _partyLookup = partyLookup;
    }

    public async Task<SalSalesReportDto> GetSalesAsync(DateTime fromDate, DateTime toDate,
                                                       CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var from = fromDate.Date;
        var to = toDate.Date;
        if (to < from) throw new ValidationException("End date cannot precede the start date");

        var invoices = await _db.SalInvoices.AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.BranchNo == branchNo && i.IsDeleted == Deleted
                        && i.Status == SaleConfirmed
                        && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .Select(i => new
            {
                i.InvoiceNo, i.InvoiceDate, i.InvoiceTime, i.PosSessionNo,
                i.SubTotal, i.LineDiscountTotal, i.BillDiscountAmount, i.PromotionDiscount,
                i.TaxAmount, i.GrandTotal, i.TotalCost, i.PaidAmount, i.DueAmount
            })
            .ToListAsync(ct);

        var report = new SalSalesReportDto { FromDate = from, ToDate = to };
        if (invoices.Count == 0) return report;

        // ── daily ────────────────────────────────────────────────────────────
        report.Daily = invoices
            .GroupBy(i => i.InvoiceDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => Summarise(
                g.Key, g.Count(),
                g.Sum(x => x.SubTotal),
                g.Sum(x => x.LineDiscountTotal + x.BillDiscountAmount + x.PromotionDiscount),
                g.Sum(x => x.TaxAmount), g.Sum(x => x.GrandTotal), g.Sum(x => x.TotalCost),
                g.Sum(x => x.PaidAmount), g.Sum(x => x.DueAmount)))
            .ToList();

        report.Totals = Summarise(
            from, invoices.Count,
            invoices.Sum(x => x.SubTotal),
            invoices.Sum(x => x.LineDiscountTotal + x.BillDiscountAmount + x.PromotionDiscount),
            invoices.Sum(x => x.TaxAmount), invoices.Sum(x => x.GrandTotal), invoices.Sum(x => x.TotalCost),
            invoices.Sum(x => x.PaidAmount), invoices.Sum(x => x.DueAmount));

        // ── by hour ──────────────────────────────────────────────────────────
        report.ByHour = invoices
            .GroupBy(i => i.InvoiceTime.Hour)
            .OrderBy(g => g.Key)
            .Select(g => new SalHourlySalesDto
            {
                Hour = g.Key,
                InvoiceCount = g.Count(),
                NetSales = g.Sum(x => x.GrandTotal)
            })
            .ToList();

        var invoiceNos = invoices.Select(i => i.InvoiceNo).ToList();

        report.ByProduct = await ByProductAsync(invoiceNos, companyNo, ct);
        report.ByCashier = await ByCashierAsync(invoices.Select(i => i.PosSessionNo).ToList(),
                                                invoiceNos, ct);
        return report;
    }

    // ── breakdowns ───────────────────────────────────────────────────────────

    private async Task<List<SalProductSalesDto>> ByProductAsync(List<long> invoiceNos, long companyNo,
                                                                CancellationToken ct)
    {
        var lines = await _db.SalInvoiceDtls.AsNoTracking()
            .Where(l => invoiceNos.Contains(l.InvoiceNo) && l.IsDeleted == Deleted)
            .Select(l => new { l.ProductNo, l.Qty, l.LineTotal, l.LineCost })
            .ToListAsync(ct);

        if (lines.Count == 0) return new List<SalProductSalesDto>();

        var names = (await _invLookup.GetProductsAsync(companyNo, ct)).ToDictionary(p => p.No, p => p.Name);

        return lines
            .GroupBy(l => l.ProductNo)
            .Select(g =>
            {
                decimal net = g.Sum(x => x.LineTotal);
                decimal cost = g.Sum(x => x.LineCost);
                return new SalProductSalesDto
                {
                    ProductNo = g.Key,
                    ProductName = names.GetValueOrDefault(g.Key),
                    QtySold = g.Sum(x => x.Qty),
                    NetSales = net,
                    CostTotal = cost,
                    Margin = net - cost,
                    MarginPct = Percent(net - cost, net)
                };
            })
            .OrderByDescending(p => p.NetSales)
            .ToList();
    }

    /// <summary>
    /// Attributed through the POS session, since that is what records who was on the till.
    /// Back-office credit invoices have no session and are therefore excluded here by design.
    /// </summary>
    private async Task<List<SalCashierSalesDto>> ByCashierAsync(List<long?> sessionNos, List<long> invoiceNos,
                                                                CancellationToken ct)
    {
        var live = sessionNos.Where(s => s.HasValue).Select(s => s!.Value).Distinct().ToList();
        if (live.Count == 0) return new List<SalCashierSalesDto>();

        var sessions = await _db.SalPosSessions.AsNoTracking()
            .Where(s => live.Contains(s.SessionNo))
            .Select(s => new { s.SessionNo, s.CashierUserNo, s.CashVariance })
            .ToListAsync(ct);

        var invoices = await _db.SalInvoices.AsNoTracking()
            .Where(i => invoiceNos.Contains(i.InvoiceNo) && i.PosSessionNo != null)
            .Select(i => new { i.PosSessionNo, i.GrandTotal })
            .ToListAsync(ct);

        var cashierBySession = sessions.ToDictionary(s => s.SessionNo, s => s.CashierUserNo);

        var byCashier = invoices
            .Where(i => cashierBySession.ContainsKey(i.PosSessionNo!.Value))
            .GroupBy(i => cashierBySession[i.PosSessionNo!.Value])
            .Select(g => new SalCashierSalesDto
            {
                CashierUserNo = g.Key,
                InvoiceCount = g.Count(),
                NetSales = g.Sum(x => x.GrandTotal),
                AverageBasket = g.Count() == 0 ? 0m : Round(g.Sum(x => x.GrandTotal) / g.Count())
            })
            .ToList();

        // Variance belongs to the cashier who closed the drawer, not to any one sale.
        foreach (var row in byCashier)
        {
            row.VarianceTotal = sessions
                .Where(s => s.CashierUserNo == row.CashierUserNo)
                .Sum(s => s.CashVariance ?? 0m);
        }

        // Cashiers are sys_user rows. Party type 3 resolves hrm_employee, so a user number passed
        // there names the wrong person (or nobody) — these are different key spaces.
        var names = await _partyLookup.GetUserNamesAsync(byCashier.Select(c => c.CashierUserNo), ct);
        foreach (var row in byCashier) row.CashierName = names.GetValueOrDefault(row.CashierUserNo);

        return byCashier.OrderByDescending(c => c.NetSales).ToList();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static SalDailySalesDto Summarise(DateTime date, int count, decimal gross, decimal discount,
                                              decimal tax, decimal net, decimal cost, decimal paid,
                                              decimal due) => new()
    {
        SaleDate = date,
        InvoiceCount = count,
        GrossSales = gross,
        DiscountTotal = discount,
        TaxAmount = tax,
        NetSales = net,
        CostTotal = cost,
        Margin = net - cost,
        MarginPct = Percent(net - cost, net),
        PaidTotal = paid,
        DueTotal = due,
        AverageBasket = count == 0 ? 0m : Round(net / count)
    };

    private static decimal Percent(decimal part, decimal whole) =>
        whole == 0 ? 0m : Math.Round(part / whole * 100m, 2, MidpointRounding.AwayFromZero);

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");
}
