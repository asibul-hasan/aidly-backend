using AidlyErp.Pur.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Pur.Domain;

namespace AidlyErp.Pur.Application.Services;

public interface IPurApLedgerService
{
    Task<decimal> CreditAsync(long supplierNo, decimal amount, short refDocType, string refDocNo, long? refDocPk,
        string? remarks, long? finYearNo = null, long? finPeriodNo = null, DateTime? txnDate = null, CancellationToken ct = default);
    Task<decimal> DebitAsync(long supplierNo, decimal amount, short refDocType, string refDocNo, long? refDocPk,
        string? remarks, long? finYearNo = null, long? finPeriodNo = null, DateTime? txnDate = null, CancellationToken ct = default);
    Task<List<PurSupplierLedger>> GetStatementAsync(long supplierNo, CancellationToken ct = default);
}

/// <summary>
/// AP subsidiary-ledger service — append-only writes to pur_supplier_ledger with a running balance,
/// keeping pur_supplier.current_payable in sync. Ref-doc types: 1=Opening 2=Invoice 3=Payment 4=Return 5=Adjustment.
/// </summary>
public class PurApLedgerService : IPurApLedgerService
{
    private readonly IPurDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public PurApLedgerService(IPurDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<decimal> CreditAsync(long supplierNo, decimal amount, short refDocType, string refDocNo,
        long? refDocPk, string? remarks, long? finYearNo = null, long? finPeriodNo = null, DateTime? txnDate = null,
        CancellationToken ct = default)
    {
        return await PostAsync(supplierNo, 0m, Math.Max(0, amount), refDocType, refDocNo, refDocPk, remarks,
            finYearNo, finPeriodNo, txnDate, ct);
    }

    public async Task<decimal> DebitAsync(long supplierNo, decimal amount, short refDocType, string refDocNo,
        long? refDocPk, string? remarks, long? finYearNo = null, long? finPeriodNo = null, DateTime? txnDate = null,
        CancellationToken ct = default)
    {
        return await PostAsync(supplierNo, Math.Max(0, amount), 0m, refDocType, refDocNo, refDocPk, remarks,
            finYearNo, finPeriodNo, txnDate, ct);
    }

    private async Task<decimal> PostAsync(long supplierNo, decimal debit, decimal credit, short refDocType,
        string refDocNo, long? refDocPk, string? remarks, long? finYearNo, long? finPeriodNo, DateTime? txnDate,
        CancellationToken ct)
    {
        if (debit < 0 || credit < 0) throw new ValidationException("Ledger amounts cannot be negative");

        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context required");
        var sup = await _db.PurSuppliers.FirstOrDefaultAsync(s => s.SupplierNo == supplierNo && s.CompanyNo == companyNo && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Supplier not found: {supplierNo}");

        decimal prevBalance = sup.CurrentPayable;
        decimal balanceAfter = prevBalance + credit - debit;

        var row = new PurSupplierLedger
        {
            CompanyNo = sup.CompanyNo,
            BranchNo = sup.BranchNo ?? _ctx.CurrentBranchNo() ?? 1,
            SupplierNo = supplierNo,
            TxnDate = txnDate ?? DateTime.UtcNow,
            RefDocType = refDocType,
            RefDocNo = refDocNo,
            RefDocPk = refDocPk,
            Remarks = remarks,
            Debit = debit,
            Credit = credit,
            BalanceAfter = balanceAfter,
            FinYearNo = finYearNo,
            FinPeriodNo = finPeriodNo,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        _db.PurSupplierLedgers.Add(row);
        sup.CurrentPayable = balanceAfter;
        sup.UpdatedBy = _ctx.CurrentUserNo(); sup.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return balanceAfter;
    }

    public async Task<List<PurSupplierLedger>> GetStatementAsync(long supplierNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        return await _db.PurSupplierLedgers
            .AsNoTracking()
            .Where(l => l.SupplierNo == supplierNo && l.CompanyNo == companyNo && l.IsDeleted == 0)
            .OrderBy(l => l.SupplierLedgerNo)
            .ToListAsync(ct);
    }
}
