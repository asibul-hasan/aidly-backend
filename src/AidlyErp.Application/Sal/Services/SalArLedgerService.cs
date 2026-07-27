using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Sal;

namespace AidlyErp.Application.Sal.Services;

public interface ISalArLedgerService
{
    Task<decimal> DebitAsync(long customerNo, decimal amount, string transType, long docNo, string? docId, string? narration, CancellationToken ct = default);
    Task<decimal> CreditAsync(long customerNo, decimal amount, string transType, long docNo, string? docId, string? narration, CancellationToken ct = default);
    Task<List<SalCustomerLedger>> GetStatementAsync(long customerNo, CancellationToken ct = default);
}

public class SalArLedgerService : ISalArLedgerService
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public SalArLedgerService(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<decimal> DebitAsync(long customerNo, decimal amount, string transType, long docNo, string? docId, string? narration, CancellationToken ct = default)
    {
        return await PostAsync(customerNo, Math.Max(0, amount), 0m, transType, docNo, docId, narration, ct);
    }

    public async Task<decimal> CreditAsync(long customerNo, decimal amount, string transType, long docNo, string? docId, string? narration, CancellationToken ct = default)
    {
        return await PostAsync(customerNo, 0m, Math.Max(0, amount), transType, docNo, docId, narration, ct);
    }

    private async Task<decimal> PostAsync(long customerNo, decimal debit, decimal credit, string transType, long docNo, string? docId, string? narration, CancellationToken ct)
    {
        var cust = await _db.SalCustomers.FirstOrDefaultAsync(c => c.CustomerNo == customerNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Customer not found: {customerNo}");

        decimal prevBalance = cust.CurrentBalance;
        decimal balanceAfter = prevBalance + debit - credit;

        var row = new SalCustomerLedger
        {
            CompanyNo = cust.CompanyNo,
            BranchNo = cust.BranchNo ?? _ctx.CurrentBranchNo() ?? 1,
            CustomerNo = customerNo,
            TransDate = DateTime.UtcNow,
            TransType = transType,
            DocNo = docNo,
            DocId = docId,
            Narration = narration,
            DebitAmount = debit,
            CreditAmount = credit,
            BalanceAmount = balanceAfter,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        _db.SalCustomerLedgers.Add(row);
        cust.CurrentBalance = balanceAfter;
        cust.UpdatedBy = _ctx.CurrentUserNo(); cust.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return balanceAfter;
    }

    public async Task<List<SalCustomerLedger>> GetStatementAsync(long customerNo, CancellationToken ct = default)
    {
        return await _db.SalCustomerLedgers
            .AsNoTracking()
            .Where(l => l.CustomerNo == customerNo && l.IsDeleted == 0)
            .OrderBy(l => l.CustomerLedgerNo)
            .ToListAsync(ct);
    }
}
