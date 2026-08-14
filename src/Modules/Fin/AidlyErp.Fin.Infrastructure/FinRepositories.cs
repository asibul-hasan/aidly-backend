using Microsoft.EntityFrameworkCore;
using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Fin.Domain;

namespace AidlyErp.Fin.Infrastructure.Repositories;

// ═══════════════════════════════════════════════════════════════════════════
// FIN Repository Implementations — EF Core translations of Spring Data repos
// ═══════════════════════════════════════════════════════════════════════════

// ── Chart of Accounts ────────────────────────────────────────────────────

public class FinAccountRepository : IFinAccountRepository
{
    private readonly IFinDbContext _db;
    public FinAccountRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinAccount>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.FinAccounts.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderBy(x => x.AccountCode).ToListAsync(ct);

    public async Task<List<FinAccount>> FindByCompanyPaginatedAsync(long companyNo, int skip, int take, CancellationToken ct = default) =>
        await _db.FinAccounts.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderBy(x => x.AccountCode).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<FinAccount?> FindByIdAsync(long accountNo, CancellationToken ct = default) =>
        await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.AccountNo == accountNo && x.IsDeleted == 0, ct);

    public async Task<FinAccount?> FindByIdAndCompanyAsync(long accountNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.AccountNo == accountNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAndCompanyAsync(string accountCode, long companyNo, CancellationToken ct = default) =>
        await _db.FinAccounts.AnyAsync(x => x.AccountCode == accountCode && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByAccountGroupAsync(long accountGroupNo, CancellationToken ct = default) =>
        await _db.FinAccounts.AnyAsync(x => x.AccountGroupNo == accountGroupNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByAccountGroupAndCompanyAsync(long accountGroupNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinAccounts.AnyAsync(x => x.AccountGroupNo == accountGroupNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<List<FinAccount>> FindByCompanyAndControlTypeAsync(long companyNo, short controlType, CancellationToken ct = default) =>
        await _db.FinAccounts.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.ControlType == controlType && x.IsDeleted == 0).OrderBy(x => x.AccountCode).ToListAsync(ct);

    public void Add(FinAccount entity) => _db.FinAccounts.Add(entity);
    public void Update(FinAccount entity) => _db.FinAccounts.Update(entity);
}

public class FinAccountGroupRepository : IFinAccountGroupRepository
{
    private readonly IFinDbContext _db;
    public FinAccountGroupRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinAccountGroup>> FindByCompanyOrderedAsync(long companyNo, CancellationToken ct = default) =>
        await _db.FinAccountGroups.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderBy(x => x.DisplayOrder).ThenBy(x => x.AccountGroupNo).ToListAsync(ct);

    public async Task<List<FinAccountGroup>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.FinAccountGroups.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderBy(x => x.AccountGroupNo).ToListAsync(ct);

    public async Task<FinAccountGroup?> FindByIdAsync(long accountGroupNo, CancellationToken ct = default) =>
        await _db.FinAccountGroups.AsNoTracking().FirstOrDefaultAsync(x => x.AccountGroupNo == accountGroupNo && x.IsDeleted == 0, ct);

    public async Task<FinAccountGroup?> FindByIdAndCompanyAsync(long accountGroupNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinAccountGroups.AsNoTracking().FirstOrDefaultAsync(x => x.AccountGroupNo == accountGroupNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAndCompanyAsync(string accountGroupId, long companyNo, CancellationToken ct = default) =>
        await _db.FinAccountGroups.AnyAsync(x => x.GroupCode == accountGroupId && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByParentGroupAsync(long parentGroupNo, CancellationToken ct = default) =>
        await _db.FinAccountGroups.AnyAsync(x => x.ParentGroupNo == parentGroupNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByParentGroupAndCompanyAsync(long parentGroupNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinAccountGroups.AnyAsync(x => x.ParentGroupNo == parentGroupNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public void Add(FinAccountGroup entity) => _db.FinAccountGroups.Add(entity);
    public void Update(FinAccountGroup entity) => _db.FinAccountGroups.Update(entity);
}

public class FinAccountBalanceRepository : IFinAccountBalanceRepository
{
    private readonly IFinDbContext _db;
    public FinAccountBalanceRepository(IFinDbContext db) => _db = db;

    public async Task<FinAccountBalance?> FindByAccountAndPeriodAsync(long accountNo, long finYearNo, long finPeriodNo, long branchNo, CancellationToken ct = default) =>
        await _db.FinAccountBalances.FirstOrDefaultAsync(x => x.AccountNo == accountNo && x.FinYearNo == finYearNo && x.FinPeriodNo == finPeriodNo && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<List<FinAccountBalance>> FindByCompanyAndPeriodAsync(long companyNo, long finPeriodNo, CancellationToken ct = default) =>
        await _db.FinAccountBalances.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.FinPeriodNo == finPeriodNo && x.IsDeleted == 0).ToListAsync(ct);

    public void Add(FinAccountBalance entity) => _db.FinAccountBalances.Add(entity);
    public void Update(FinAccountBalance entity) => _db.FinAccountBalances.Update(entity);
}

// ── Vouchers ─────────────────────────────────────────────────────────────

public class FinVoucherRepository : IFinVoucherRepository
{
    private readonly IFinDbContext _db;
    public FinVoucherRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinVoucher>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.FinVouchers.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderByDescending(x => x.VoucherNo).ToListAsync(ct);

    public async Task<List<FinVoucher>> FindByCompanyPaginatedAsync(long companyNo, int skip, int take, CancellationToken ct = default) =>
        await _db.FinVouchers.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderByDescending(x => x.VoucherNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<FinVoucher?> FindByIdAsync(long voucherNo, CancellationToken ct = default) =>
        await _db.FinVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.IsDeleted == 0, ct);

    public async Task<FinVoucher?> FindByIdAndCompanyAsync(long voucherNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByVoucherIdAndCompanyAsync(string voucherId, long companyNo, CancellationToken ct = default) =>
        await _db.FinVouchers.AnyAsync(x => x.VoucherId == voucherId && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByVoucherTypeAndCompanyAsync(long voucherTypeNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinVouchers.AnyAsync(x => x.VoucherTypeNo == voucherTypeNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<FinVoucher?> FindBySourceDocAsync(string sourceDocType, long sourceDocNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.SourceDocType == sourceDocType && x.SourceDocNo == sourceDocNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<FinVoucher?> FindBySourceDocAndBranchAsync(string sourceDocType, long sourceDocNo, long companyNo, long branchNo, CancellationToken ct = default) =>
        await _db.FinVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.SourceDocType == sourceDocType && x.SourceDocNo == sourceDocNo && x.CompanyNo == companyNo && x.BranchNo == branchNo && x.IsDeleted == 0, ct);

    public async Task<List<FinVoucher>> FindFilteredAsync(long companyNo, long? type, DateTime? fromDate, DateTime? toDate, string? search, CancellationToken ct = default)
    {
        var query = _db.FinVouchers.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0);
        if (type.HasValue && type.Value > 0) query = query.Where(x => x.VoucherTypeNo == type.Value);
        if (fromDate.HasValue) query = query.Where(x => x.VoucherDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.VoucherDate <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.VoucherId.ToLower().Contains(s) || (x.Narration != null && x.Narration.ToLower().Contains(s)));
        }
        return await query.OrderByDescending(x => x.VoucherDate).ThenByDescending(x => x.VoucherNo).ToListAsync(ct);
    }

    public void Add(FinVoucher entity) => _db.FinVouchers.Add(entity);
    public void Update(FinVoucher entity) => _db.FinVouchers.Update(entity);
}

public class FinVoucherDtlRepository : IFinVoucherDtlRepository
{
    private readonly IFinDbContext _db;
    public FinVoucherDtlRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinVoucherDtl>> FindByVoucherAsync(long voucherNo, CancellationToken ct = default) =>
        await _db.FinVoucherDtls.AsNoTracking().Where(x => x.VoucherNo == voucherNo && x.IsDeleted == 0).OrderBy(x => x.LineNo).ToListAsync(ct);

    public async Task<bool> ExistsByAccountAsync(long accountNo, CancellationToken ct = default) =>
        await _db.FinVoucherDtls.AnyAsync(x => x.AccountNo == accountNo && x.IsDeleted == 0, ct);

    public void Add(FinVoucherDtl entity) => _db.FinVoucherDtls.Add(entity);
    public void Update(FinVoucherDtl entity) => _db.FinVoucherDtls.Update(entity);
}

public class FinVoucherTypeRepository : IFinVoucherTypeRepository
{
    private readonly IFinDbContext _db;
    public FinVoucherTypeRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinVoucherType>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.FinVoucherTypes.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderBy(x => x.OrderSl).ThenBy(x => x.VoucherTypeNo).ToListAsync(ct);

    public async Task<FinVoucherType?> FindByIdAsync(long voucherTypeNo, CancellationToken ct = default) =>
        await _db.FinVoucherTypes.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherTypeNo == voucherTypeNo && x.IsDeleted == 0, ct);

    public async Task<FinVoucherType?> FindByIdAndCompanyAsync(long voucherTypeNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinVoucherTypes.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherTypeNo == voucherTypeNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAndCompanyAsync(string voucherTypeId, long companyNo, CancellationToken ct = default) =>
        await _db.FinVoucherTypes.AnyAsync(x => x.VoucherTypeCode == voucherTypeId && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public void Add(FinVoucherType entity) => _db.FinVoucherTypes.Add(entity);
    public void Update(FinVoucherType entity) => _db.FinVoucherTypes.Update(entity);
}

// ── Banking ──────────────────────────────────────────────────────────────

public class FinBankAccountRepository : IFinBankAccountRepository
{
    private readonly IFinDbContext _db;
    public FinBankAccountRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinBankAccount>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.FinBankAccounts.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderByDescending(x => x.BankAccountNo).ToListAsync(ct);

    public async Task<FinBankAccount?> FindByIdAsync(long bankAccountNo, CancellationToken ct = default) =>
        await _db.FinBankAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.BankAccountNo == bankAccountNo && x.IsDeleted == 0, ct);

    public async Task<FinBankAccount?> FindByIdAndCompanyAsync(long bankAccountNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinBankAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.BankAccountNo == bankAccountNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByCodeAndCompanyAsync(string bankAccountId, long companyNo, CancellationToken ct = default) =>
        await _db.FinBankAccounts.AnyAsync(x => x.BankAccountId == bankAccountId && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByAccountNoAndCompanyAsync(long accountNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinBankAccounts.AnyAsync(x => x.AccountNo == accountNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public void Add(FinBankAccount entity) => _db.FinBankAccounts.Add(entity);
    public void Update(FinBankAccount entity) => _db.FinBankAccounts.Update(entity);
}

public class FinBankReconRepository : IFinBankReconRepository
{
    private readonly IFinDbContext _db;
    public FinBankReconRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinBankRecon>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.FinBankRecons.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderByDescending(x => x.ReconNo).ToListAsync(ct);

    public async Task<FinBankRecon?> FindByIdAsync(long bankReconNo, CancellationToken ct = default) =>
        await _db.FinBankRecons.AsNoTracking().FirstOrDefaultAsync(x => x.ReconNo == bankReconNo && x.IsDeleted == 0, ct);

    public async Task<FinBankRecon?> FindByIdAndCompanyAsync(long bankReconNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinBankRecons.AsNoTracking().FirstOrDefaultAsync(x => x.ReconNo == bankReconNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsByAccountAndCompanyAsync(long accountNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinBankRecons.AnyAsync(x => x.AccountNo == accountNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public void Add(FinBankRecon entity) => _db.FinBankRecons.Add(entity);
    public void Update(FinBankRecon entity) => _db.FinBankRecons.Update(entity);
}

public class FinBankReconLineRepository : IFinBankReconLineRepository
{
    private readonly IFinDbContext _db;
    public FinBankReconLineRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinBankReconLine>> FindByReconAsync(long bankReconNo, CancellationToken ct = default) =>
        await _db.FinBankReconLines.AsNoTracking().Where(x => x.ReconNo == bankReconNo && x.IsDeleted == 0).OrderBy(x => x.ReconLineNo).ToListAsync(ct);

    public async Task<List<long>> FindClearedLedgerNosAsync(long companyNo, long accountNo, CancellationToken ct = default) =>
        await _db.FinBankReconLines.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.ClearedDate != null &&
                        _db.FinLedgers.Any(l => l.LedgerNo == x.LedgerNo && l.CompanyNo == companyNo && l.AccountNo == accountNo && l.IsDeleted == 0))
            .Select(x => x.LedgerNo)
            .ToListAsync(ct);

    public async Task<List<FinBankReconLine>> FindClearedLinesAsync(long companyNo, long accountNo, CancellationToken ct = default) =>
        await _db.FinBankReconLines.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.ClearedDate != null &&
                        _db.FinLedgers.Any(l => l.LedgerNo == x.LedgerNo && l.CompanyNo == companyNo && l.AccountNo == accountNo && l.IsDeleted == 0))
            .OrderBy(x => x.ReconLineNo)
            .ToListAsync(ct);

    public void Add(FinBankReconLine entity) => _db.FinBankReconLines.Add(entity);
    public void Update(FinBankReconLine entity) => _db.FinBankReconLines.Update(entity);
}

// ── GL Mapping ───────────────────────────────────────────────────────────

public class FinGlMapRepository : IFinGlMapRepository
{
    private readonly IFinDbContext _db;
    public FinGlMapRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinGlMap>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.FinGlMaps.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderBy(x => x.EventType).ThenBy(x => x.LegKey).ToListAsync(ct);

    public async Task<FinGlMap?> FindByIdAsync(long glMapNo, CancellationToken ct = default) =>
        await _db.FinGlMaps.AsNoTracking().FirstOrDefaultAsync(x => x.GlMapNo == glMapNo && x.IsDeleted == 0, ct);

    public async Task<FinGlMap?> FindByIdAndCompanyAsync(long glMapNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinGlMaps.AsNoTracking().FirstOrDefaultAsync(x => x.GlMapNo == glMapNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<FinGlMap?> FindByEventAndLegAsync(long companyNo, string eventType, string legKey, string? subKey, CancellationToken ct = default) =>
        await _db.FinGlMaps.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyNo == companyNo && x.EventType == eventType && x.LegKey == legKey && x.SubKey == subKey && x.IsDeleted == 0, ct);

    public async Task<FinGlMap?> FindByEventLegAndActiveAsync(long companyNo, string eventType, string legKey, string? subKey, short isActive, CancellationToken ct = default) =>
        await _db.FinGlMaps.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyNo == companyNo && x.EventType == eventType && x.LegKey == legKey && x.SubKey == subKey && x.IsActive == isActive && x.IsDeleted == 0, ct);

    public void Add(FinGlMap entity) => _db.FinGlMaps.Add(entity);
    public void Update(FinGlMap entity) => _db.FinGlMaps.Update(entity);
}

// ── Ledger (append-only source of truth) ─────────────────────────────────

public class FinLedgerRepository : IFinLedgerRepository
{
    private readonly IFinDbContext _db;
    public FinLedgerRepository(IFinDbContext db) => _db = db;

    public async Task<List<FinLedger>> FindByVoucherAsync(long voucherNo, CancellationToken ct = default) =>
        await _db.FinLedgers.AsNoTracking().Where(x => x.VoucherNo == voucherNo && x.IsDeleted == 0).OrderBy(x => x.LedgerNo).ToListAsync(ct);

    public async Task<bool> ExistsByAccountAsync(long accountNo, CancellationToken ct = default) =>
        await _db.FinLedgers.AnyAsync(x => x.AccountNo == accountNo && x.IsDeleted == 0, ct);

    public async Task<List<FinLedger>> FindByAccountAsync(long accountNo, CancellationToken ct = default) =>
        await _db.FinLedgers.AsNoTracking().Where(x => x.AccountNo == accountNo && x.IsDeleted == 0).OrderBy(x => x.VoucherDate).ThenBy(x => x.LedgerNo).ToListAsync(ct);

    public async Task<List<FinLedger>> FindByCompanyAndPeriodAsync(long companyNo, long finPeriodNo, CancellationToken ct = default) =>
        await _db.FinLedgers.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.FinPeriodNo == finPeriodNo && x.IsDeleted == 0).ToListAsync(ct);

    public async Task<List<FinLedger>> FindByAccountAndDateRangeAsync(long accountNo, long companyNo, DateTime from, DateTime to, CancellationToken ct = default) =>
        await _db.FinLedgers.AsNoTracking().Where(x => x.AccountNo == accountNo && x.CompanyNo == companyNo && x.VoucherDate >= from && x.VoucherDate <= to && x.IsDeleted == 0)
            .OrderBy(x => x.VoucherDate).ThenBy(x => x.LedgerNo).ToListAsync(ct);

    public async Task<List<FinLedger>> FindByCompanyAndDateRangeAsync(long companyNo, DateTime from, DateTime to, CancellationToken ct = default) =>
        await _db.FinLedgers.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.VoucherDate >= from && x.VoucherDate <= to && x.IsDeleted == 0)
            .OrderBy(x => x.VoucherDate).ThenBy(x => x.VoucherNo).ThenBy(x => x.LedgerNo).ToListAsync(ct);

    public async Task<(decimal Debit, decimal Credit)> OpeningBeforeAsync(long accountNo, long companyNo, DateTime before, CancellationToken ct = default)
    {
        var rows = await _db.FinLedgers.AsNoTracking()
            .Where(x => x.AccountNo == accountNo && x.CompanyNo == companyNo && x.VoucherDate < before && x.IsDeleted == 0)
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .FirstOrDefaultAsync(ct);
        return (rows?.Debit ?? 0m, rows?.Credit ?? 0m);
    }

    public async Task<List<(long AccountNo, decimal Debit, decimal Credit)>> TrialBalanceAsync(long companyNo, DateTime asOf, CancellationToken ct = default) =>
        await _db.FinLedgers.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo && x.VoucherDate <= asOf && x.IsDeleted == 0)
            .GroupBy(x => x.AccountNo)
            .Select(g => new ValueTuple<long, decimal, decimal>(g.Key, g.Sum(x => x.Debit), g.Sum(x => x.Credit)))
            .ToListAsync(ct);

    public async Task<List<(long AccountNo, decimal Debit, decimal Credit)>> TotalsBetweenAsync(long companyNo, DateTime from, DateTime to, CancellationToken ct = default) =>
        await _db.FinLedgers.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo && x.VoucherDate >= from && x.VoucherDate <= to && x.IsDeleted == 0)
            .GroupBy(x => x.AccountNo)
            .Select(g => new ValueTuple<long, decimal, decimal>(g.Key, g.Sum(x => x.Debit), g.Sum(x => x.Credit)))
            .ToListAsync(ct);

    public async Task<List<FinLedger>> FindPartyLedgerAsync(long companyNo, List<long> accountNos, DateTime asOf, CancellationToken ct = default) =>
        await _db.FinLedgers.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo && accountNos.Contains(x.AccountNo) && x.VoucherDate <= asOf && x.IsDeleted == 0)
            .OrderBy(x => x.VoucherDate).ThenBy(x => x.LedgerNo)
            .ToListAsync(ct);

    public void Add(FinLedger entity) => _db.FinLedgers.Add(entity);
    public void Update(FinLedger entity) => _db.FinLedgers.Update(entity);
}
