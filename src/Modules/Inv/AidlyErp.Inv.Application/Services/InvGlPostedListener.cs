using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Services;

/// <summary>
/// Consumes GlPosted events to stamp gl_voucher_no on INV source documents.
/// </summary>
public class InvGlPostedListener : IGlPostedListener
{
    private readonly IInvDbContext _db;

    public InvGlPostedListener(IInvDbContext db) => _db = db;

    public async Task OnGlPostedAsync(string sourceDocType, long sourceDocNo, long voucherNo, string voucherId, CancellationToken ct = default)
    {
        switch (sourceDocType)
        {
            case "OpeningStockPosted":
            case "StockAdjustmentPosted":
            case "StockAdjustmentReversed":
                var adj = await _db.InvStockAdjustments.FirstOrDefaultAsync(a => a.AdjustmentNo == sourceDocNo && a.IsDeleted == 0, ct);
                if (adj != null) { adj.GlVoucherNo = long.TryParse(voucherId, out var vn) ? vn : null; await _db.SaveChangesAsync(ct); }
                break;
        }
    }
}
