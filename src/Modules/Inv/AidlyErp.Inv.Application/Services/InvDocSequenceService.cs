using AidlyErp.Inv.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;

namespace AidlyErp.Inv.Application.Services;

public interface IInvDocSequenceService
{
    Task<string> GetNextSequenceAsync(long companyNo, long branchNo, string docSeqCode, long? finYearNo, CancellationToken ct = default);
}

public class InvDocSequenceService : IInvDocSequenceService
{
    private readonly IInvDbContext _db;

    public InvDocSequenceService(IInvDbContext db) => _db = db;

    public async Task<string> GetNextSequenceAsync(long companyNo, long branchNo, string docSeqCode, long? finYearNo, CancellationToken ct = default)
    {
        long seq = 1;
        var existingCount = await _db.InvStockLedgers.CountAsync(l => l.CompanyNo == companyNo && l.BranchNo == branchNo, ct);
        seq = existingCount + 1;
        return $"{docSeqCode}-{DateTime.UtcNow:yyyyMM}-{seq:D6}";
    }
}
