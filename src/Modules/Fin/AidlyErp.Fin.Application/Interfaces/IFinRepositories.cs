using AidlyErp.Fin.Domain;

namespace AidlyErp.Fin.Application.Interfaces;

// ═══════════════════════════════════════════════════════════════════════════
// FIN Repository Interfaces — port of Spring Data JPA repositories
// Every method here has a 1:1 counterpart in the Java fin/repository/ package.
// ═══════════════════════════════════════════════════════════════════════════

// ── Chart of Accounts ────────────────────────────────────────────────────

public interface IFinAccountRepository
{
    Task<List<FinAccount>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<List<FinAccount>> FindByCompanyPaginatedAsync(long companyNo, int skip, int take, CancellationToken ct = default);
    Task<FinAccount?> FindByIdAsync(long accountNo, CancellationToken ct = default);
    Task<FinAccount?> FindByIdAndCompanyAsync(long accountNo, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAndCompanyAsync(string accountCode, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByAccountGroupAsync(long accountGroupNo, CancellationToken ct = default);
    Task<bool> ExistsByAccountGroupAndCompanyAsync(long accountGroupNo, long companyNo, CancellationToken ct = default);
    Task<List<FinAccount>> FindByCompanyAndControlTypeAsync(long companyNo, short controlType, CancellationToken ct = default);
    void Add(FinAccount entity);
    void Update(FinAccount entity);
}

public interface IFinAccountGroupRepository
{
    Task<List<FinAccountGroup>> FindByCompanyOrderedAsync(long companyNo, CancellationToken ct = default);
    Task<List<FinAccountGroup>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<FinAccountGroup?> FindByIdAsync(long accountGroupNo, CancellationToken ct = default);
    Task<FinAccountGroup?> FindByIdAndCompanyAsync(long accountGroupNo, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAndCompanyAsync(string accountGroupId, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByParentGroupAsync(long parentGroupNo, CancellationToken ct = default);
    Task<bool> ExistsByParentGroupAndCompanyAsync(long parentGroupNo, long companyNo, CancellationToken ct = default);
    void Add(FinAccountGroup entity);
    void Update(FinAccountGroup entity);
}

public interface IFinAccountBalanceRepository
{
    Task<FinAccountBalance?> FindByAccountAndPeriodAsync(long accountNo, long finYearNo, long finPeriodNo, long branchNo, CancellationToken ct = default);
    Task<List<FinAccountBalance>> FindByCompanyAndPeriodAsync(long companyNo, long finPeriodNo, CancellationToken ct = default);
    void Add(FinAccountBalance entity);
    void Update(FinAccountBalance entity);
}

// ── Vouchers ─────────────────────────────────────────────────────────────

public interface IFinVoucherRepository
{
    Task<List<FinVoucher>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<List<FinVoucher>> FindByCompanyPaginatedAsync(long companyNo, int skip, int take, CancellationToken ct = default);
    Task<FinVoucher?> FindByIdAsync(long voucherNo, CancellationToken ct = default);
    Task<FinVoucher?> FindByIdAndCompanyAsync(long voucherNo, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByVoucherIdAndCompanyAsync(string voucherId, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByVoucherTypeAndCompanyAsync(long voucherTypeNo, long companyNo, CancellationToken ct = default);
    Task<FinVoucher?> FindBySourceDocAsync(string sourceDocType, long sourceDocNo, long companyNo, CancellationToken ct = default);
    Task<FinVoucher?> FindBySourceDocAndBranchAsync(string sourceDocType, long sourceDocNo, long companyNo, long branchNo, CancellationToken ct = default);
    Task<List<FinVoucher>> FindFilteredAsync(long companyNo, long? type, DateTime? fromDate, DateTime? toDate, string? search, CancellationToken ct = default);
    void Add(FinVoucher entity);
    void Update(FinVoucher entity);
}

public interface IFinVoucherDtlRepository
{
    Task<List<FinVoucherDtl>> FindByVoucherAsync(long voucherNo, CancellationToken ct = default);
    Task<bool> ExistsByAccountAsync(long accountNo, CancellationToken ct = default);
    void Add(FinVoucherDtl entity);
    void Update(FinVoucherDtl entity);
}

public interface IFinVoucherTypeRepository
{
    Task<List<FinVoucherType>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<FinVoucherType?> FindByIdAsync(long voucherTypeNo, CancellationToken ct = default);
    Task<FinVoucherType?> FindByIdAndCompanyAsync(long voucherTypeNo, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAndCompanyAsync(string voucherTypeId, long companyNo, CancellationToken ct = default);
    void Add(FinVoucherType entity);
    void Update(FinVoucherType entity);
}

// ── Banking ──────────────────────────────────────────────────────────────

public interface IFinBankAccountRepository
{
    Task<List<FinBankAccount>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<FinBankAccount?> FindByIdAsync(long bankAccountNo, CancellationToken ct = default);
    Task<FinBankAccount?> FindByIdAndCompanyAsync(long bankAccountNo, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAndCompanyAsync(string bankAccountId, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByAccountNoAndCompanyAsync(long accountNo, long companyNo, CancellationToken ct = default);
    void Add(FinBankAccount entity);
    void Update(FinBankAccount entity);
}

public interface IFinBankReconRepository
{
    Task<List<FinBankRecon>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<FinBankRecon?> FindByIdAsync(long bankReconNo, CancellationToken ct = default);
    Task<FinBankRecon?> FindByIdAndCompanyAsync(long bankReconNo, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByAccountAndCompanyAsync(long accountNo, long companyNo, CancellationToken ct = default);
    void Add(FinBankRecon entity);
    void Update(FinBankRecon entity);
}

public interface IFinBankReconLineRepository
{
    Task<List<FinBankReconLine>> FindByReconAsync(long bankReconNo, CancellationToken ct = default);
    Task<List<long>> FindClearedLedgerNosAsync(long companyNo, long accountNo, CancellationToken ct = default);
    Task<List<FinBankReconLine>> FindClearedLinesAsync(long companyNo, long accountNo, CancellationToken ct = default);
    void Add(FinBankReconLine entity);
    void Update(FinBankReconLine entity);
}

// ── GL Mapping ───────────────────────────────────────────────────────────

public interface IFinGlMapRepository
{
    Task<List<FinGlMap>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<FinGlMap?> FindByIdAsync(long glMapNo, CancellationToken ct = default);
    Task<FinGlMap?> FindByIdAndCompanyAsync(long glMapNo, long companyNo, CancellationToken ct = default);
    Task<FinGlMap?> FindByEventAndLegAsync(long companyNo, string eventType, string legKey, string? subKey, CancellationToken ct = default);
    Task<FinGlMap?> FindByEventLegAndActiveAsync(long companyNo, string eventType, string legKey, string? subKey, short isActive, CancellationToken ct = default);
    void Add(FinGlMap entity);
    void Update(FinGlMap entity);
}

// ── Ledger (append-only source of truth) ─────────────────────────────────

public interface IFinLedgerRepository
{
    Task<List<FinLedger>> FindByVoucherAsync(long voucherNo, CancellationToken ct = default);
    Task<bool> ExistsByAccountAsync(long accountNo, CancellationToken ct = default);
    Task<List<FinLedger>> FindByAccountAsync(long accountNo, CancellationToken ct = default);
    Task<List<FinLedger>> FindByCompanyAndPeriodAsync(long companyNo, long finPeriodNo, CancellationToken ct = default);
    Task<List<FinLedger>> FindByAccountAndDateRangeAsync(long accountNo, long companyNo, DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<FinLedger>> FindByCompanyAndDateRangeAsync(long companyNo, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Returns opening debit/credit totals for an account before the given date.</summary>
    Task<(decimal Debit, decimal Credit)> OpeningBeforeAsync(long accountNo, long companyNo, DateTime before, CancellationToken ct = default);

    /// <summary>Trial balance: per-account debit/credit totals up to asOf.</summary>
    Task<List<(long AccountNo, decimal Debit, decimal Credit)>> TrialBalanceAsync(long companyNo, DateTime asOf, CancellationToken ct = default);

    /// <summary>Per-account totals between two dates.</summary>
    Task<List<(long AccountNo, decimal Debit, decimal Credit)>> TotalsBetweenAsync(long companyNo, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Party ledger entries for given accounts up to asOf.</summary>
    Task<List<FinLedger>> FindPartyLedgerAsync(long companyNo, List<long> accountNos, DateTime asOf, CancellationToken ct = default);

    void Add(FinLedger entity);
    void Update(FinLedger entity);
}
