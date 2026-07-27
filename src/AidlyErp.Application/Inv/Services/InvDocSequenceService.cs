using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Interfaces;

namespace AidlyErp.Application.Inv.Services;

public interface IInvDocSequenceService
{
    Task<string> GetNextSequenceAsync(long companyNo, long branchNo, string docSeqCode, long? finYearNo, CancellationToken ct = default);
}

public class InvDocSequenceService : IInvDocSequenceService
{
    private readonly IApplicationDbContext _db;

    public InvDocSequenceService(IApplicationDbContext db) => _db = db;

    public async Task<string> GetNextSequenceAsync(long companyNo, long branchNo, string docSeqCode, long? finYearNo, CancellationToken ct = default)
    {
        long seq = 1;
        var existingCount = await _db.InvStockLedgers.CountAsync(l => l.CompanyNo == companyNo && l.BranchNo == branchNo, ct);
        seq = existingCount + 1;
        return $"{docSeqCode}-{DateTime.UtcNow:yyyyMM}-{seq:D6}";
    }
}
