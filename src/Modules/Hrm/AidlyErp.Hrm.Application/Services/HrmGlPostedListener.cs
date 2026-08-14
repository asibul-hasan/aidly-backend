using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Hrm.Application.Services;

/// <summary>
/// Consumes GlPosted events to stamp gl_voucher_no on HRM source documents.
/// </summary>
public class HrmGlPostedListener : IGlPostedListener
{
    private readonly IHrmDbContext _db;

    public HrmGlPostedListener(IHrmDbContext db) => _db = db;

    public async Task OnGlPostedAsync(string sourceDocType, long sourceDocNo, long voucherNo, string voucherId, CancellationToken ct = default)
    {
        switch (sourceDocType)
        {
            case "PayrollPosted":
                var payroll = await _db.HrmPayrollRuns.FirstOrDefaultAsync(p => p.PayrollRunNo == sourceDocNo && p.IsDeleted == 0, ct);
                if (payroll != null) { payroll.GlVoucherNo = voucherId; await _db.SaveChangesAsync(ct); }
                break;

            case "FinalSettlementPosted":
                // Final settlement doesn't have a dedicated run table to stamp
                break;
        }
    }
}
