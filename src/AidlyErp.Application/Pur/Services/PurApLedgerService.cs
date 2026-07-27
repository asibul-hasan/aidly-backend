using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Pur;

namespace AidlyErp.Application.Pur.Services;

public interface IPurApLedgerService
{
    Task<decimal> CreditAsync(long supplierNo, decimal amount, string transType, long docNo, string? docId, string? narration, CancellationToken ct = default);
    Task<decimal> DebitAsync(long supplierNo, decimal amount, string transType, long docNo, string? docId, string? narration, CancellationToken ct = default);
    Task<List<PurSupplierLedger>> GetStatementAsync(long supplierNo, CancellationToken ct = default);
}

public class PurApLedgerService : IPurApLedgerService
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public PurApLedgerService(IApplicationDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<decimal> CreditAsync(long supplierNo, decimal amount, string transType, long docNo, string? docId, string? narration, CancellationToken ct = default)
    {
        return await PostAsync(supplierNo, 0m, Math.Max(0, amount), transType, docNo, docId, narration, ct);
    }

    public async Task<decimal> DebitAsync(long supplierNo, decimal amount, string transType, long docNo, string? docId, string? narration, CancellationToken ct = default)
    {
        return await PostAsync(supplierNo, Math.Max(0, amount), 0m, transType, docNo, docId, narration, ct);
    }

    private async Task<decimal> PostAsync(long supplierNo, decimal debit, decimal credit, string transType, long docNo, string? docId, string? narration, CancellationToken ct)
    {
        var sup = await _db.PurSuppliers.FirstOrDefaultAsync(s => s.SupplierNo == supplierNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier not found: {supplierNo}");

        decimal prevBalance = sup.CurrentBalance;
        decimal balanceAfter = prevBalance + credit - debit; // Credit increases payable, Debit decreases payable

        var row = new PurSupplierLedger
        {
            CompanyNo = sup.CompanyNo,
            BranchNo = sup.BranchNo ?? _ctx.CurrentBranchNo() ?? 1,
            SupplierNo = supplierNo,
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

        _db.PurSupplierLedgers.Add(row);
        sup.CurrentBalance = balanceAfter;
        sup.UpdatedBy = _ctx.CurrentUserNo(); sup.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return balanceAfter;
    }

    public async Task<List<PurSupplierLedger>> GetStatementAsync(long supplierNo, CancellationToken ct = default)
    {
        return await _db.PurSupplierLedgers
            .AsNoTracking()
            .Where(l => l.SupplierNo == supplierNo && l.IsDeleted == 0)
            .OrderBy(l => l.SupplierLedgerNo)
            .ToListAsync(ct);
    }
}
