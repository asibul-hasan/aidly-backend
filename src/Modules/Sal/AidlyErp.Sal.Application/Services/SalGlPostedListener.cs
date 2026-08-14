using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Application.Services;

/// <summary>
/// Consumes GlPosted events to stamp gl_voucher_no on SAL source documents.
/// Registered as IGlPostedListener — the FIN drain dispatches to all listeners.
/// </summary>
public class SalGlPostedListener : IGlPostedListener
{
    private readonly ISalDbContext _db;

    public SalGlPostedListener(ISalDbContext db) => _db = db;

    public async Task OnGlPostedAsync(string sourceDocType, long sourceDocNo, long voucherNo, string voucherId, CancellationToken ct = default)
    {
        switch (sourceDocType)
        {
            case "SalesInvoicePosted":
                var invoice = await _db.SalInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == sourceDocNo && i.IsDeleted == 0, ct);
                if (invoice != null) { invoice.GlVoucherNo = voucherId; await _db.SaveChangesAsync(ct); }
                break;

            case "CustomerReceiptPosted":
                var receipt = await _db.SalReceipts.FirstOrDefaultAsync(r => r.ReceiptNo == sourceDocNo && r.IsDeleted == 0, ct);
                if (receipt != null) { receipt.GlVoucherNo = voucherId; await _db.SaveChangesAsync(ct); }
                break;

            case "SalesReturnPosted":
                var ret = await _db.SalReturns.FirstOrDefaultAsync(r => r.ReturnNo == sourceDocNo && r.IsDeleted == 0, ct);
                if (ret != null) { ret.GlVoucherNo = voucherId; await _db.SaveChangesAsync(ct); }
                break;
        }
    }
}
