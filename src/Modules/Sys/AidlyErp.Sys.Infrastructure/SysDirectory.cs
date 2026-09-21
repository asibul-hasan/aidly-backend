using AidlyErp.Shared.Core.Numbering;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Infrastructure;

/// <summary>SYS's side of <see cref="ISysUserDirectory"/>.</summary>
internal sealed class SysUserDirectory : ISysUserDirectory
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;

    public SysUserDirectory(ISysDbContext db) => _db = db;

    public async Task<long?> FindUserNoByEmployeeNoAsync(long employeeNo,
                                                          CancellationToken cancellationToken = default)
    {
        if (employeeNo <= 0) return null;

        // 0 is the "no employee linked" sentinel on sys_user, so it must never match.
        var userNo = await _db.Users
            .AsNoTracking()
            .Where(u => u.EmployeeNo == employeeNo && u.IsDeleted == Deleted && u.IsActive == 1)
            .Select(u => u.UserNo)
            .FirstOrDefaultAsync(cancellationToken);

        return userNo == 0 ? null : userNo;
    }

    public async Task<long?> FindEmployeeNoByUserNoAsync(long userNo,
                                                          CancellationToken cancellationToken = default)
    {
        if (userNo <= 0) return null;

        var employeeNo = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserNo == userNo && u.IsDeleted == Deleted)
            .Select(u => u.EmployeeNo)
            .FirstOrDefaultAsync(cancellationToken);

        // 0 is the "no employee linked" sentinel on sys_user.
        return employeeNo == 0 ? null : employeeNo;
    }

    public async Task<IReadOnlyDictionary<long, long>> MapEmployeesToUsersAsync(
        IReadOnlyCollection<long> employeeNos, CancellationToken cancellationToken = default)
    {
        if (employeeNos.Count == 0) return new Dictionary<long, long>();

        var rows = await _db.Users
            .AsNoTracking()
            .Where(u => u.EmployeeNo.HasValue && employeeNos.Contains(u.EmployeeNo.Value) && u.IsDeleted == Deleted && u.IsActive == 1)
            .Select(u => new { EmployeeNo = u.EmployeeNo!.Value, u.UserNo })
            .ToListAsync(cancellationToken);

        // An employee could in principle have more than one login; take the lowest so the
        // recipient is deterministic rather than dependent on row order.
        return rows
            .GroupBy(r => r.EmployeeNo)
            .ToDictionary(g => g.Key, g => g.Min(r => r.UserNo));
    }
}

/// <summary>SYS's side of <see cref="ISysBranchDirectory"/>.</summary>
internal sealed class SysBranchDirectory : ISysBranchDirectory
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;

    public SysBranchDirectory(ISysDbContext db) => _db = db;

    public Task<bool> ExistsAsync(long branchNo, CancellationToken cancellationToken = default) =>
        _db.Branches.AnyAsync(b => b.BranchNo == branchNo && b.IsDeleted == Deleted, cancellationToken);

    public async Task<BranchInfo?> FindAsync(long branchNo, CancellationToken cancellationToken = default) =>
        await _db.Branches
            .AsNoTracking()
            .Where(b => b.BranchNo == branchNo && b.IsDeleted == Deleted)
            .Select(b => new BranchInfo(b.BranchNo, b.BranchId, b.BranchName, b.CompanyNo))
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>SYS's side of <see cref="ISysSettingsStore"/>.</summary>
internal sealed class SysSettingsStore : ISysSettingsStore
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;

    public SysSettingsStore(ISysDbContext db) => _db = db;

    public async Task<string?> GetAsync(long companyNo, string key, CancellationToken cancellationToken = default) =>
        await _db.Settings
            .AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.SettingKey == key && s.IsDeleted == Deleted)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertAsync(long companyNo, string key, string value, short valueType,
                                  string? group, string? description,
                                  CancellationToken cancellationToken = default)
    {
        var entity = await _db.Settings
            .FirstOrDefaultAsync(s => s.CompanyNo == companyNo && s.SettingKey == key && s.IsDeleted == Deleted,
                cancellationToken);

        if (entity == null)
        {
            entity = new Setting
            {
                CompanyNo = companyNo,
                SettingKey = key,
                SettingGroup = group,
                IsActive = 1,
                IsDeleted = Deleted,
                CreatedAt = DateTime.UtcNow
            };
            _db.Settings.Add(entity);
        }

        entity.SettingValue = value;
        entity.ValueType = valueType;
        // Group and description are only filled in when absent — never overwritten.
        entity.SettingGroup ??= group;
        entity.Description ??= description;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>SYS's side of <see cref="IDocSequenceGenerator"/>.</summary>
internal sealed class DocSequenceGenerator : IDocSequenceGenerator
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;

    public DocSequenceGenerator(ISysDbContext db) => _db = db;

    /// <summary>
    /// Hands out the next document number for a series, honouring whatever SYS_1301 has configured
    /// for it — pattern, prefix, suffix, padding, starting number and yearly reset.
    ///
    /// <para>The <paramref name="prefix"/> and <paramref name="width"/> arguments are only defaults
    /// used to SEED a series the first time it is asked for. Once the row exists, the configuration
    /// on it wins: that is the whole point of the setup form, and previously these arguments
    /// overrode it on every call, so the form's settings did nothing.</para>
    /// </summary>
    public async Task<string> NextAsync(long companyNo, long? branchNo, string docType, string prefix,
                                        int width = 4, CancellationToken cancellationToken = default)
    {
        var seq = await _db.DocSequences.FirstOrDefaultAsync(
            s => s.CompanyNo == companyNo && s.BranchNo == branchNo && s.DocType == docType
                 && s.IsDeleted == Deleted, cancellationToken);

        if (seq == null)
        {
            // First document of this type — seed the series from the caller's defaults so a company
            // that has not visited SYS_1301 still gets sensible numbers.
            seq = new DocSequence
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                DocType = docType,
                Prefix = prefix,
                Padding = (short)width,
                StartingNo = 1,
                NextVal = 1,
                IsActive = 1,
                IsDeleted = Deleted,
                CreatedAt = DateTime.UtcNow
            };
            _db.DocSequences.Add(seq);
        }

        // reset_policy 1 = restart the count each financial year. Series carrying a fin_year_no that
        // is no longer the current one start again at starting_no rather than running on.
        if (seq.ResetPolicy == ResetYearly)
        {
            var currentYear = await CurrentFinYearAsync(companyNo, cancellationToken);
            if (currentYear is not null && seq.FinYearNo != currentYear.FinYearNo)
            {
                seq.FinYearNo = currentYear.FinYearNo;
                seq.NextVal = seq.StartingNo > 0 ? seq.StartingNo : 1;
            }
        }

        long current = seq.NextVal > 0 ? seq.NextVal : (seq.StartingNo > 0 ? seq.StartingNo : 1);
        seq.NextVal = current + 1;
        seq.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return await FormatAsync(seq, current, companyNo, branchNo, cancellationToken);
    }

    /// <summary>reset_policy 1 = yearly. 2 = never (the default).</summary>
    private const short ResetYearly = 1;

    private async Task<FinYear?> CurrentFinYearAsync(long companyNo, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return await _db.FinYears.AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.IsDeleted == Deleted
                        && y.StartDate <= today && y.EndDate >= today)
            .OrderBy(y => y.FinYearNo)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// A configured pattern wins; otherwise the number falls back to prefix + padded counter +
    /// suffix, which is what every series produced before patterns existed.
    /// </summary>
    private async Task<string> FormatAsync(DocSequence seq, long value, long companyNo,
                                           long? branchNo, CancellationToken ct)
    {
        int pad = seq.Padding > 0 ? seq.Padding : 6;

        if (string.IsNullOrWhiteSpace(seq.Pattern))
            return (seq.Prefix ?? string.Empty) + value.ToString(new string('0', pad)) + seq.Suffix;

        var year = await CurrentFinYearAsync(companyNo, ct);

        string? companyCode = await _db.Companies.AsNoTracking()
            .Where(c => c.CompanyNo == companyNo).Select(c => c.CompanyId).FirstOrDefaultAsync(ct);

        string? branchCode = branchNo is null ? null : await _db.Branches.AsNoTracking()
            .Where(b => b.BranchNo == branchNo).Select(b => b.BranchId).FirstOrDefaultAsync(ct);

        return IdPatternResolver.Resolve(seq.Pattern, new IdPatternResolver.Ctx(
            DocDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
            BranchCode: branchCode,
            CompanyCode: companyCode,
            DocSubType: string.IsNullOrWhiteSpace(seq.DocSubType) ? seq.DocType : seq.DocSubType,
            FiscalYearStart: year?.StartDate.Year,
            FiscalYearEnd: year?.EndDate.Year,
            SequenceValue: value));
    }
}

/// <summary>SYS's side of <see cref="IFinCalendar"/>.</summary>
internal sealed class FinCalendar : IFinCalendar
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;

    public FinCalendar(ISysDbContext db) => _db = db;

    private static readonly System.Linq.Expressions.Expression<Func<FinYear, FinYearInfo>> ToYear =
        y => new FinYearInfo(y.FinYearNo, y.CompanyNo ?? 0, y.FinYearId, y.FinYearName, y.YearName,
            y.StartDate, y.EndDate, y.YearStatus, y.IsClosed, y.BranchNo);

    private static readonly System.Linq.Expressions.Expression<Func<FinYearDtl, FinPeriodInfo>> ToPeriod =
        p => new FinPeriodInfo(p.FinPeriodNo, p.FinYearNo, p.FinPeriodId, p.FinPeriodName,
            p.StartDate, p.EndDate, p.PeriodType, p.PeriodStatus, p.IsClosed);

    public async Task<FinYearInfo?> FindYearAsync(long finYearNo, CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.FinYearNo == finYearNo && y.IsDeleted == Deleted)
            .Select(ToYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinYearInfo?> FindYearForDateAsync(long companyNo, DateOnly date,
                                                         CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.StartDate <= date && y.EndDate >= date
                        && y.IsDeleted == Deleted)
            .Select(ToYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinYearInfo?> FindActiveYearForDateAsync(DateOnly date,
                                                               CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.IsActive == 1 && y.IsDeleted == Deleted && y.StartDate <= date && y.EndDate >= date)
            .Select(ToYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinYearInfo?> FindAnyActiveYearAsync(CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.IsActive == 1 && y.IsDeleted == Deleted)
            .Select(ToYear)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<FinYearInfo>> ListYearsAsync(long companyNo,
                                                                 CancellationToken cancellationToken = default) =>
        await _db.FinYears.AsNoTracking()
            .Where(y => y.CompanyNo == companyNo && y.IsDeleted == Deleted)
            .OrderByDescending(y => y.StartDate)
            .Select(ToYear)
            .ToListAsync(cancellationToken);

    public async Task<FinPeriodInfo?> FindPeriodAsync(long finPeriodNo,
                                                      CancellationToken cancellationToken = default) =>
        await _db.FinYearDtls.AsNoTracking()
            .Where(p => p.FinPeriodNo == finPeriodNo && p.IsDeleted == Deleted)
            .Select(ToPeriod)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinPeriodInfo?> FindPeriodForDateAsync(long finYearNo, DateOnly date,
                                                             CancellationToken cancellationToken = default) =>
        await _db.FinYearDtls.AsNoTracking()
            .Where(p => p.FinYearNo == finYearNo && p.StartDate <= date && p.EndDate >= date
                        && p.IsDeleted == Deleted)
            .Select(ToPeriod)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<FinPeriodInfo>> ListPeriodsAsync(IReadOnlyCollection<long> finYearNos,
                                                                     CancellationToken cancellationToken = default)
    {
        if (finYearNos.Count == 0) return Array.Empty<FinPeriodInfo>();

        return await _db.FinYearDtls.AsNoTracking()
            .Where(p => finYearNos.Contains(p.FinYearNo) && p.IsDeleted == Deleted)
            .OrderBy(p => p.StartDate)
            .Select(ToPeriod)
            .ToListAsync(cancellationToken);
    }

    public async Task ClosePeriodAsync(long finPeriodNo, long actingUserNo,
                                       CancellationToken cancellationToken = default)
    {
        var period = await _db.FinYearDtls
            .FirstOrDefaultAsync(p => p.FinPeriodNo == finPeriodNo && p.IsDeleted == Deleted, cancellationToken);

        if (period == null) return;

        period.PeriodStatus = 2; // Closed
        period.UpdatedBy = actingUserNo;
        period.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPeriodStatusAsync(long finPeriodNo, short status, long actingUserNo,
                                           CancellationToken cancellationToken = default)
    {
        var period = await _db.FinYearDtls
            .FirstOrDefaultAsync(p => p.FinPeriodNo == finPeriodNo && p.IsDeleted == Deleted, cancellationToken);

        if (period == null) return;

        period.PeriodStatus = status;
        period.UpdatedBy = actingUserNo;
        period.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseYearAsync(long finYearNo, long actingUserNo,
                                     CancellationToken cancellationToken = default)
    {
        var year = await _db.FinYears
            .FirstOrDefaultAsync(y => y.FinYearNo == finYearNo && y.IsDeleted == Deleted, cancellationToken);

        if (year == null) return;

        year.YearStatus = 2; // Closed
        year.UpdatedBy = actingUserNo;
        year.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>SYS's side of <see cref="IApprovalRequestReader"/>.</summary>
internal sealed class ApprovalRequestReader : IApprovalRequestReader
{
    private readonly ISysDbContext _db;

    public ApprovalRequestReader(ISysDbContext db) => _db = db;

    public async Task<short?> GetStatusAsync(long approvalRequestNo, CancellationToken cancellationToken = default) =>
        await _db.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.ApprovalRequestNo == approvalRequestNo)
            .Select(r => (short?)r.Status)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(short Status, short CurrentStep)?> GetStateAsync(long approvalRequestNo, CancellationToken cancellationToken = default)
    {
        var req = await _db.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.ApprovalRequestNo == approvalRequestNo)
            .Select(r => new { r.Status, r.CurrentStep })
            .FirstOrDefaultAsync(cancellationToken);

        if (req == null) return null;
        return (req.Status, req.CurrentStep);
    }
}

/// <summary>SYS's side of <see cref="IVatTaxLookup"/>.</summary>
internal sealed class VatTaxLookup : IVatTaxLookup
{
    private readonly ISysDbContext _db;
    public VatTaxLookup(ISysDbContext db) => _db = db;

    public async Task<Dictionary<long, string>> GetTaxCodesAsync(IEnumerable<long> vatTaxNos, CancellationToken ct = default)
    {
        var nos = vatTaxNos.ToList();
        return await _db.VatTaxes
            .AsNoTracking()
            .Where(t => nos.Contains(t.VatTaxNo) && t.IsDeleted == 0)
            .ToDictionaryAsync(t => t.VatTaxNo, t => t.TaxCode, ct);
    }

    public async Task<Dictionary<long, decimal>> GetTaxRatesAsync(IEnumerable<long> vatTaxNos, CancellationToken ct = default)
    {
        var nos = vatTaxNos.ToList();
        if (nos.Count == 0) return new Dictionary<long, decimal>();

        return await _db.VatTaxes
            .AsNoTracking()
            .Where(t => nos.Contains(t.VatTaxNo) && t.IsDeleted == 0)
            .ToDictionaryAsync(t => t.VatTaxNo, t => t.RatePercentage, ct);
    }
}

/// <summary>SYS's side of <see cref="ICurrencyLookup"/>.</summary>
internal sealed class CurrencyLookup : ICurrencyLookup
{
    private readonly ISysDbContext _db;
    public CurrencyLookup(ISysDbContext db) => _db = db;

    public async Task<long> GetBaseCurrencyNoAsync(long companyNo, CancellationToken ct = default) =>
        await _db.Currencies
            .AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsBaseCurrency == 1 && c.IsDeleted == 0)
            .Select(c => c.CurrencyNo)
            .FirstOrDefaultAsync(ct);
}

/// <summary>SYS's side of <see cref="IPartyLookup"/>.</summary>
internal sealed class PartyLookup : IPartyLookup
{
    private readonly ISysDbContext _db;
    public PartyLookup(ISysDbContext db) => _db = db;

    public async Task<Dictionary<long, string>> GetPartyNamesAsync(short partyType, IEnumerable<long> partyNos, CancellationToken ct = default)
    {
        var nos = partyNos.ToList();
        if (nos.Count == 0) return new Dictionary<long, string>();

        // partyType: 1=Customer (sal_customer), 2=Supplier (pur_supplier), 3=Employee (hrm_employee)
        string table = partyType switch
        {
            1 => "sal_customer",
            2 => "pur_supplier",
            3 => "hrm_employee",
            _ => throw new ArgumentException($"Invalid party type: {partyType}")
        };

        string idCol = partyType switch
        {
            1 => "customer_no",
            2 => "supplier_no",
            3 => "employee_no",
            _ => throw new ArgumentException($"Invalid party type: {partyType}")
        };

        // hrm_employee has no full_name column — it stores first/middle/last separately, so every
        // party-type-3 lookup failed with 42703. Built here instead, skipping a null middle name.
        string nameCol = partyType switch
        {
            1 => "customer_name",
            2 => "supplier_name",
            3 => "concat_ws(' ', first_name, middle_name, last_name)",
            _ => throw new ArgumentException($"Invalid party type: {partyType}")
        };

        // Batch-load all names in ONE query
        var result = new Dictionary<long, string>();
        var conn = _db.Database.GetDbConnection();
        try
        {
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            using var cmd = conn.CreateCommand();
            var nosParam = string.Join(",", nos);
            cmd.CommandText = $"SELECT {idCol}, {nameCol} FROM {table} WHERE {idCol} IN ({nosParam}) AND is_deleted = 0";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetInt64(0);
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(name))
                    result[id] = name;
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }

        return result;
    }

    public async Task<Dictionary<long, string>> GetUserNamesAsync(IEnumerable<long> userNos,
                                                                  CancellationToken ct = default)
    {
        var nos = userNos.Distinct().Where(n => n > 0).ToList();
        var result = new Dictionary<long, string>();
        if (nos.Count == 0) return result;

        var conn = _db.Database.GetDbConnection();
        try
        {
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            using var cmd = conn.CreateCommand();
            // user_name is the display name; user_id is the login. Falls back so a user with no
            // display name still shows something a human recognises.
            cmd.CommandText =
                $"SELECT user_no, COALESCE(NULLIF(user_name, ''), user_id) FROM sys_user " +
                $"WHERE user_no IN ({string.Join(",", nos)}) AND is_deleted = 0";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(name)) result[reader.GetInt64(0)] = name;
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }

        return result;
    }
}

/// <summary>
/// SYS's side of <see cref="IInvLookup"/>. Raw SQL against the INV master tables so SAL
/// and PUR can build option lists without referencing AidlyErp.Inv.Domain — the module
/// boundary tests forbid that reference. Same approach as <see cref="PartyLookup"/>.
/// Column names verified against information_schema.
/// </summary>
internal sealed class InvLookup : IInvLookup
{
    private readonly ISysDbContext _db;
    public InvLookup(ISysDbContext db) => _db = db;

    public Task<List<LookupOption>> GetWarehousesAsync(long companyNo, long? branchNo, CancellationToken ct = default) =>
        QueryAsync(
            "SELECT warehouse_no, warehouse_name FROM inv_warehouse " +
            "WHERE company_no = @company AND is_deleted = 0 AND COALESCE(is_active,1) = 1 " +
            "  AND (@branch IS NULL OR branch_no = @branch) " +
            "ORDER BY warehouse_name",
            companyNo, branchNo, ct);

    public Task<List<LookupOption>> GetProductsAsync(long companyNo, CancellationToken ct = default) =>
        QueryAsync(
            "SELECT product_no, product_id || ' — ' || product_name FROM inv_product " +
            "WHERE company_no = @company AND is_deleted = 0 AND COALESCE(is_active,1) = 1 " +
            "ORDER BY product_id",
            companyNo, null, ct);

    public Task<List<LookupOption>> GetUomsAsync(long companyNo, CancellationToken ct = default) =>
        QueryAsync(
            "SELECT uom_no, uom_name FROM inv_uom " +
            "WHERE company_no = @company AND is_deleted = 0 AND COALESCE(is_active,1) = 1 " +
            "ORDER BY uom_name",
            companyNo, null, ct);

    public Task<List<LookupOption>> GetCategoriesAsync(long companyNo, CancellationToken ct = default) =>
        QueryAsync(
            "SELECT category_no, category_name FROM inv_category " +
            "WHERE company_no = @company AND is_deleted = 0 AND COALESCE(is_active,1) = 1 " +
            "ORDER BY category_name",
            companyNo, null, ct);

    public Task<List<LookupOption>> GetBrandsAsync(long companyNo, CancellationToken ct = default) =>
        QueryAsync(
            "SELECT brand_no, brand_name FROM inv_brand " +
            "WHERE company_no = @company AND is_deleted = 0 AND COALESCE(is_active,1) = 1 " +
            "ORDER BY brand_name",
            companyNo, null, ct);

    private async Task<List<LookupOption>> QueryAsync(string sql, long companyNo, long? branchNo, CancellationToken ct)
    {
        var result = new List<LookupOption>();
        var conn = _db.Database.GetDbConnection();
        bool opened = false;
        try
        {
            if (conn.State != System.Data.ConnectionState.Open) { await conn.OpenAsync(ct); opened = true; }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            var pc = cmd.CreateParameter();
            pc.ParameterName = "company"; pc.Value = companyNo;
            cmd.Parameters.Add(pc);

            var pb = cmd.CreateParameter();
            pb.ParameterName = "branch"; pb.Value = (object?)branchNo ?? DBNull.Value;
            cmd.Parameters.Add(pb);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.Add(new LookupOption(reader.GetInt64(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
        return result;
    }
}
