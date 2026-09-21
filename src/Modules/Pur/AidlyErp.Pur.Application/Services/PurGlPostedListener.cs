using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Pur.Application.Services;

/// <summary>
/// Consumes GlPosted events to stamp gl_voucher_no on PUR source documents.
/// </summary>
public class PurGlPostedListener : IGlPostedListener
{
    private readonly IPurDbContext _db;

    public PurGlPostedListener(IPurDbContext db) => _db = db;

    public async Task OnGlPostedAsync(string sourceDocType, long sourceDocNo, long voucherNo, string voucherId, CancellationToken ct = default)
    {
        switch (sourceDocType)
        {
            case "PurchaseInvoicePosted":
                var invoice = await _db.PurInvoices.FirstOrDefaultAsync(i => i.InvoiceNo == sourceDocNo && i.IsDeleted == 0, ct);
                if (invoice != null) { invoice.GlVoucherNo = voucherId; await _db.SaveChangesAsync(ct); }
                break;

            case "SupplierPaymentPosted":
                var payment = await _db.PurPayments.FirstOrDefaultAsync(p => p.PaymentNo == sourceDocNo && p.IsDeleted == 0, ct);
                if (payment != null) { payment.GlVoucherNo = voucherId; await _db.SaveChangesAsync(ct); }
                break;
        }
    }
}
