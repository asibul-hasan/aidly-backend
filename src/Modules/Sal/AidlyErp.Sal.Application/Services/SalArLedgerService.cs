using AidlyErp.Sal.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sal.Domain;

namespace AidlyErp.Sal.Application.Services;

public interface ISalArLedgerService
{
    Task<decimal> DebitAsync(long customerNo, decimal amount, short refDocType, string refDocNo, long? refDocPk, string? remarks, CancellationToken ct = default);
    Task<decimal> CreditAsync(long customerNo, decimal amount, short refDocType, string refDocNo, long? refDocPk, string? remarks, CancellationToken ct = default);
    Task<List<SalCustomerLedger>> GetStatementAsync(long customerNo, CancellationToken ct = default);
}

public class SalArLedgerService : ISalArLedgerService
{
    private readonly ISalDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public SalArLedgerService(ISalDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<decimal> DebitAsync(long customerNo, decimal amount, short refDocType, string refDocNo, long? refDocPk, string? remarks, CancellationToken ct = default)
    {
        return await PostAsync(customerNo, Math.Max(0, amount), 0m, refDocType, refDocNo, refDocPk, remarks, ct);
    }

    public async Task<decimal> CreditAsync(long customerNo, decimal amount, short refDocType, string refDocNo, long? refDocPk, string? remarks, CancellationToken ct = default)
    {
        return await PostAsync(customerNo, 0m, Math.Max(0, amount), refDocType, refDocNo, refDocPk, remarks, ct);
    }

    private async Task<decimal> PostAsync(long customerNo, decimal debit, decimal credit, short refDocType, string refDocNo, long? refDocPk, string? remarks, CancellationToken ct)
    {
        if (debit < 0 || credit < 0) throw new ValidationException("Ledger amounts cannot be negative");

        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var cust = await _db.SalCustomers.FirstOrDefaultAsync(c => c.CustomerNo == customerNo && c.CompanyNo == companyNo && c.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Customer not found: {customerNo}");

        decimal prevBalance = cust.CurrentDue;
        decimal balanceAfter = prevBalance + debit - credit;

        var row = new SalCustomerLedger
        {
            CompanyNo = cust.CompanyNo,
            BranchNo = cust.BranchNo ?? _ctx.CurrentBranchNo() ?? 1,
            CustomerNo = customerNo,
            TxnDate = DateTime.UtcNow,
            RefDocType = refDocType,
            RefDocNo = refDocNo,
            RefDocPk = refDocPk,
            Remarks = remarks,
            Debit = debit,
            Credit = credit,
            BalanceAfter = balanceAfter,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        _db.SalCustomerLedgers.Add(row);
        cust.CurrentDue = balanceAfter;
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
