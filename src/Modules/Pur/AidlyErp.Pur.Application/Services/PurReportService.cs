using AidlyErp.Inv.Contracts;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Pur.Application.Services;

public interface IPurReportService
{
    Task<PurAgingReportDto> GetAgingAsync(DateTime asOf, CancellationToken ct = default);
    Task<PurSpendReportDto> GetSpendAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}

/// <summary>
/// PUR_1301 Supplier Aging &amp; Spend — read-only over posted purchase invoices.
///
/// <para>Aging reads <c>pur_invoice.due_amount</c> rather than replaying the supplier ledger: the
/// invoice is what a supplier chases you about, and it is the grain a payment run is planned at.
/// The ledger remains the movement history; this is the outstanding position.</para>
/// </summary>
public class PurReportService : IPurReportService
{
    private const short Deleted = 0;

    /// <summary>Only posted bills are owed; drafts and cancelled documents are not liabilities.</summary>
    private const short StPosted = 2;

    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IInvCatalog _catalog;
    private readonly IInvLookup _invLookup;

    public PurReportService(IPurDbContext db, ICompanyBranchContext ctx, IInvCatalog catalog,
                            IInvLookup invLookup)
    {
        _db = db;
        _ctx = ctx;
        _catalog = catalog;
        _invLookup = invLookup;
    }

    // ── aging ────────────────────────────────────────────────────────────────

    public async Task<PurAgingReportDto> GetAgingAsync(DateTime asOf, CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();
        var cutoff = asOf.Date;

        var open = await _db.PurInvoices.AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.BranchNo == branchNo && i.IsDeleted == Deleted
                        && i.Status == StPosted && i.DueAmount > 0 && i.InvoiceDate <= cutoff)
            .Select(i => new { i.SupplierNo, i.InvoiceDate, i.DueDate, i.DueAmount })
            .ToListAsync(ct);

        var report = new PurAgingReportDto { AsOf = cutoff };
        if (open.Count == 0) return report;

        var supplierNos = open.Select(i => i.SupplierNo).Distinct().ToList();
        var names = await _db.PurSuppliers.AsNoTracking()
            .Where(s => supplierNos.Contains(s.SupplierNo))
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        report.Rows = open
            .GroupBy(i => i.SupplierNo)
            .Select(g =>
            {
                var row = new PurSupplierAgingDto
                {
                    SupplierNo = g.Key,
                    SupplierName = names.GetValueOrDefault(g.Key),
                    InvoiceCount = g.Count()
                };

                foreach (var bill in g)
                {
                    // Age from the due date; a bill with no terms is due the day it was raised.
                    var due = (bill.DueDate ?? bill.InvoiceDate).Date;
                    int daysOverdue = (cutoff - due).Days;

                    if (daysOverdue <= 0) row.CurrentAmount += bill.DueAmount;
                    else if (daysOverdue <= 30) row.Days1To30 += bill.DueAmount;
                    else if (daysOverdue <= 60) row.Days31To60 += bill.DueAmount;
                    else if (daysOverdue <= 90) row.Days61To90 += bill.DueAmount;
                    else row.DaysOver90 += bill.DueAmount;

                    if (daysOverdue > row.OldestDays) row.OldestDays = daysOverdue;
                }

                row.TotalDue = g.Sum(x => x.DueAmount);
                return row;
            })
            // Worst first: the supplier owed longest is the one about to stop delivering.
            .OrderByDescending(r => r.OldestDays)
            .ThenByDescending(r => r.TotalDue)
            .ToList();

        report.TotalDue = report.Rows.Sum(r => r.TotalDue);
        return report;
    }

    // ── spend ────────────────────────────────────────────────────────────────

    public async Task<PurSpendReportDto> GetSpendAsync(DateTime fromDate, DateTime toDate,
                                                       CancellationToken ct = default)
    {
        long companyNo = Company(), branchNo = Branch();

        var from = fromDate.Date;
        var to = toDate.Date;
        if (to < from) throw new ValidationException("End date cannot precede the start date");

        var invoices = await _db.PurInvoices.AsNoTracking()
            .Where(i => i.CompanyNo == companyNo && i.BranchNo == branchNo && i.IsDeleted == Deleted
                        && i.Status == StPosted && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .Select(i => new { i.InvoiceNo, i.SupplierNo, i.TaxableAmount })
            .ToListAsync(ct);

        var report = new PurSpendReportDto { FromDate = from, ToDate = to };
        if (invoices.Count == 0) return report;

        // Spend is measured before tax — input VAT is reclaimable, not cost.
        report.TotalSpend = invoices.Sum(i => i.TaxableAmount);

        // ── by supplier ──────────────────────────────────────────────────────
        var supplierNos = invoices.Select(i => i.SupplierNo).Distinct().ToList();
        var supplierNames = await _db.PurSuppliers.AsNoTracking()
            .Where(s => supplierNos.Contains(s.SupplierNo))
            .ToDictionaryAsync(s => s.SupplierNo, s => s.SupplierName, ct);

        report.BySupplier = invoices
            .GroupBy(i => i.SupplierNo)
            .Select(g => new PurSpendRowDto
            {
                KeyNo = g.Key,
                Label = supplierNames.GetValueOrDefault(g.Key),
                InvoiceCount = g.Count(),
                Spend = g.Sum(x => x.TaxableAmount),
                SharePct = Share(g.Sum(x => x.TaxableAmount), report.TotalSpend)
            })
            .OrderByDescending(r => r.Spend)
            .ToList();

        // ── by category ──────────────────────────────────────────────────────
        var invoiceNos = invoices.Select(i => i.InvoiceNo).ToList();
        var lines = await _db.PurInvoiceDtls.AsNoTracking()
            .Where(l => invoiceNos.Contains(l.InvoiceNo) && l.IsDeleted == Deleted)
            .Select(l => new { l.ProductNo, l.TaxableAmount })
            .ToListAsync(ct);

        if (lines.Count > 0)
        {
            var products = await _catalog.GetProductsAsync(
                lines.Select(l => l.ProductNo).Distinct().ToList(), companyNo, ct);

            var categoryNames = (await _invLookup.GetCategoriesAsync(companyNo, ct))
                .ToDictionary(c => c.No, c => c.Name);

            decimal lineTotal = lines.Sum(l => l.TaxableAmount);

            report.ByCategory = lines
                .GroupBy(l => products.GetValueOrDefault(l.ProductNo)?.CategoryNo ?? 0)
                .Select(g => new PurSpendRowDto
                {
                    KeyNo = g.Key,
                    // A product with no category still bought something; do not hide the spend.
                    Label = g.Key == 0 ? "(uncategorised)" : categoryNames.GetValueOrDefault(g.Key),
                    InvoiceCount = g.Count(),
                    Spend = g.Sum(x => x.TaxableAmount),
                    SharePct = Share(g.Sum(x => x.TaxableAmount), lineTotal)
                })
                .OrderByDescending(r => r.Spend)
                .ToList();
        }

        return report;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static decimal Share(decimal part, decimal whole) =>
        whole == 0 ? 0m : Math.Round(part / whole * 100m, 2, MidpointRounding.AwayFromZero);

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");

    private long Branch() =>
        _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");
}
