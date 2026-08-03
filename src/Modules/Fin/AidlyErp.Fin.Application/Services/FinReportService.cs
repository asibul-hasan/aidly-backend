using AidlyErp.Fin.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;

namespace AidlyErp.Fin.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// FinReportService — Financial Statements & Analytical Reports
// ═══════════════════════════════════════════════════════════════════════════

public interface IFinReportService
{
    Task<List<Fin1301TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime? asOfDate, CancellationToken ct = default);
    Task<Fin1302LedgerDto> GetGeneralLedgerAsync(long accountNo, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<List<Fin1303DayBookRowDto>> GetDayBookAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<Fin1304PnlDto> GetPnlAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<Fin1305BalanceSheetDto> GetBalanceSheetAsync(DateTime asOfDate, CancellationToken ct = default);
    Task<Fin1306CashFlowDto> GetCashFlowAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<Fin1307AgingDto> GetAgingAsync(short partyType, DateTime asOfDate, CancellationToken ct = default);
}

public class FinReportService : IFinReportService
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    public FinReportService(IFinDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1301TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime? asOfDate, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        DateTime cutoff = asOfDate ?? DateTime.UtcNow.Date;

        var accounts = await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0 && a.IsPostable == 1)
            .OrderBy(a => a.AccountCode)
            .ToListAsync(ct);

        var ledgerSums = await _db.FinLedgers
            .AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.VoucherDate <= cutoff && l.IsDeleted == 0)
            .GroupBy(l => l.AccountNo)
            .Select(g => new
            {
                AccountNo = g.Key,
                TotalDebit = g.Sum(x => x.Debit),
                TotalCredit = g.Sum(x => x.Credit)
            })
            .ToDictionaryAsync(x => x.AccountNo, ct);

        var result = new List<Fin1301TrialBalanceRowDto>();

        foreach (var a in accounts)
        {
            decimal deb = ledgerSums.ContainsKey(a.AccountNo) ? ledgerSums[a.AccountNo].TotalDebit : 0m;
            decimal cred = ledgerSums.ContainsKey(a.AccountNo) ? ledgerSums[a.AccountNo].TotalCredit : 0m;

            decimal openBal = a.OpeningBalance;
            decimal closing = a.NormalBalance == "dr"
                ? (openBal + deb - cred)
                : (openBal + cred - deb);

            result.Add(new Fin1301TrialBalanceRowDto
            {
                AccountNo = a.AccountNo,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                RootType = a.RootType,
                OpeningBalance = openBal,
                Debit = deb,
                Credit = cred,
                ClosingBalance = closing
            });
        }

        return result;
    }

    public async Task<Fin1302LedgerDto> GetGeneralLedgerAsync(long accountNo, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        var acc = await _db.FinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNo == accountNo && a.CompanyNo == companyNo && a.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Account not found: {accountNo}");

        var priorLedgers = await _db.FinLedgers.AsNoTracking()
            .Where(l => l.AccountNo == accountNo && l.VoucherDate < fromDate && l.IsDeleted == 0)
            .ToListAsync(ct);

        decimal priorDeb = priorLedgers.Sum(l => l.Debit);
        decimal priorCred = priorLedgers.Sum(l => l.Credit);

        decimal openBal = acc.NormalBalance == "dr"
            ? (acc.OpeningBalance + priorDeb - priorCred)
            : (acc.OpeningBalance + priorCred - priorDeb);

        var periodLedgers = await _db.FinLedgers.AsNoTracking()
            .Where(l => l.AccountNo == accountNo && l.VoucherDate >= fromDate && l.VoucherDate <= toDate && l.IsDeleted == 0)
            .OrderBy(l => l.VoucherDate).ThenBy(l => l.LedgerNo)
            .ToListAsync(ct);

        var voucherNos = periodLedgers.Select(l => l.VoucherNo).Distinct().ToList();
        var vouchers = await _db.FinVouchers.AsNoTracking().Where(v => voucherNos.Contains(v.VoucherNo)).ToDictionaryAsync(v => v.VoucherNo, ct);

        var rows = new List<Fin1302LedgerRowDto>();
        decimal running = openBal;

        foreach (var l in periodLedgers)
        {
            decimal deb = l.Debit;
            decimal cred = l.Credit;

            if (acc.NormalBalance == "dr") running += (deb - cred);
            else running += (cred - deb);

            rows.Add(new Fin1302LedgerRowDto
            {
                LedgerNo = l.LedgerNo,
                VoucherNo = l.VoucherNo,
                VoucherId = vouchers.ContainsKey(l.VoucherNo) ? vouchers[l.VoucherNo].VoucherId : null,
                TxnDate = l.VoucherDate,
                Narration = vouchers.ContainsKey(l.VoucherNo) ? vouchers[l.VoucherNo].Narration : null,
                Debit = deb,
                Credit = cred,
                RunningBalance = running
            });
        }

        return new Fin1302LedgerDto
        {
            AccountNo = acc.AccountNo,
            AccountCode = acc.AccountCode,
            AccountName = acc.AccountName,
            OpeningBalance = openBal,
            TotalDebit = periodLedgers.Sum(l => l.Debit),
            TotalCredit = periodLedgers.Sum(l => l.Credit),
            ClosingBalance = running,
            Rows = rows
        };
    }

    public async Task<List<Fin1303DayBookRowDto>> GetDayBookAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        // Load vouchers first (company-scoped), then load only their details.
        var vouchers = await _db.FinVouchers.AsNoTracking()
            .Where(v => v.CompanyNo == companyNo && v.VoucherDate >= fromDate && v.VoucherDate <= toDate && v.IsDeleted == 0 && v.Status == 2)
            .ToDictionaryAsync(v => v.VoucherNo, ct);

        var voucherNos = vouchers.Keys.ToList();

        var dtls = await _db.FinVoucherDtls.AsNoTracking()
            .Where(d => voucherNos.Contains(d.VoucherNo) && d.IsDeleted == 0)
            .ToListAsync(ct);

        var typeNos = vouchers.Values.Select(v => v.VoucherTypeNo).Distinct().ToList();
        var types = await _db.FinVoucherTypes.AsNoTracking().Where(t => typeNos.Contains(t.VoucherTypeNo)).ToDictionaryAsync(t => t.VoucherTypeNo, t => t.VoucherTypeName, ct);

        var accounts = await _db.FinAccounts.AsNoTracking().Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0).ToDictionaryAsync(a => a.AccountNo, ct);

        var result = new List<Fin1303DayBookRowDto>();

        foreach (var d in dtls)
        {
            if (!vouchers.ContainsKey(d.VoucherNo)) continue;
            var v = vouchers[d.VoucherNo];
            var a = accounts.ContainsKey(d.AccountNo) ? accounts[d.AccountNo] : null;

            result.Add(new Fin1303DayBookRowDto
            {
                VoucherNo = v.VoucherNo,
                VoucherId = v.VoucherId,
                VoucherTypeName = types.ContainsKey(v.VoucherTypeNo) ? types[v.VoucherTypeNo] : "Journal",
                VoucherDate = v.VoucherDate,
                Narration = d.LineNarration ?? v.Narration,
                AccountCode = a?.AccountCode,
                AccountName = a?.AccountName,
                Debit = d.Debit,
                Credit = d.Credit
            });
        }

        return result.OrderBy(r => r.VoucherDate).ThenBy(r => r.VoucherNo).ToList();
    }

    public async Task<Fin1304PnlDto> GetPnlAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var tb = await GetTrialBalanceAsync(toDate, ct);

        var revRows = tb.Where(r => r.RootType == 4).ToList(); // 4 = Revenue
        var expRows = tb.Where(r => r.RootType == 5).ToList(); // 5 = Expense

        decimal totalRev = revRows.Sum(r => r.ClosingBalance);
        decimal totalExp = expRows.Sum(r => r.ClosingBalance);

        return new Fin1304PnlDto
        {
            RevenueRows = revRows.Cast<FinStatementRowDto>().ToList(),
            TotalRevenue = totalRev,
            ExpenseRows = expRows.Cast<FinStatementRowDto>().ToList(),
            TotalExpense = totalExp,
            NetProfit = totalRev - totalExp
        };
    }

    public async Task<Fin1305BalanceSheetDto> GetBalanceSheetAsync(DateTime asOfDate, CancellationToken ct = default)
    {
        var tb = await GetTrialBalanceAsync(asOfDate, ct);
        var pnl = await GetPnlAsync(DateTime.MinValue, asOfDate, ct);

        var assetRows = tb.Where(r => r.RootType == 1).ToList();      // 1 = Asset
        var liabilityRows = tb.Where(r => r.RootType == 2).ToList();  // 2 = Liability
        var equityRows = tb.Where(r => r.RootType == 3).ToList();     // 3 = Equity

        decimal totalAssets = assetRows.Sum(r => r.ClosingBalance);
        decimal totalLiabilities = liabilityRows.Sum(r => r.ClosingBalance);
        decimal totalEquity = equityRows.Sum(r => r.ClosingBalance);

        return new Fin1305BalanceSheetDto
        {
            AssetRows = assetRows.Cast<FinStatementRowDto>().ToList(),
            TotalAssets = totalAssets,
            LiabilityRows = liabilityRows.Cast<FinStatementRowDto>().ToList(),
            TotalLiabilities = totalLiabilities,
            EquityRows = equityRows.Cast<FinStatementRowDto>().ToList(),
            TotalEquity = totalEquity,
            RetainedEarnings = pnl.NetProfit
        };
    }

    public async Task<Fin1306CashFlowDto> GetCashFlowAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        // Identify cash/bank accounts (control_type 3=Bank, 4=Cash).
        var cashAccs = await _db.FinAccounts.AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0
                        && (a.ControlType == 3 || a.ControlType == 4))
            .ToListAsync(ct);
        var cashAccNos = cashAccs.Select(a => a.AccountNo).ToHashSet();
        var accMap = cashAccs.ToDictionary(a => a.AccountNo);

        // Opening balances = cumulative debit-credit up to day before fromDate.
        var openingDate = fromDate.AddDays(-1);
        var openingLedger = await _db.FinLedgers.AsNoTracking()
            .Where(l => cashAccNos.Contains(l.AccountNo) && l.VoucherDate <= openingDate && l.IsDeleted == 0)
            .Select(l => new { l.AccountNo, l.Debit, l.Credit })
            .ToListAsync(ct);
        var opening = openingLedger
            .GroupBy(l => l.AccountNo)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));

        // Period movements = debits (receipts) and credits (payments) within [fromDate, toDate].
        var moveLedger = await _db.FinLedgers.AsNoTracking()
            .Where(l => cashAccNos.Contains(l.AccountNo) && l.VoucherDate >= fromDate && l.VoucherDate <= toDate && l.IsDeleted == 0)
            .Select(l => new { l.AccountNo, l.Debit, l.Credit })
            .ToListAsync(ct);
        var move = moveLedger
            .GroupBy(l => l.AccountNo)
            .ToDictionary(g => g.Key, g => (
                Debits: g.Sum(l => l.Debit),
                Credits: g.Sum(l => l.Credit)
            ));

        var rows = new List<Fin1306CashFlowRowDto>();
        decimal totOpen = 0, totRec = 0, totPay = 0, totClose = 0;

        foreach (var accNo in cashAccNos)
        {
            decimal op = opening.GetValueOrDefault(accNo);
            var m = move.GetValueOrDefault(accNo);
            decimal rec = m.Debits;
            decimal pay = m.Credits;
            decimal close = op + rec - pay;

            if (op == 0 && rec == 0 && pay == 0) continue;

            accMap.TryGetValue(accNo, out var acc);
            rows.Add(new Fin1306CashFlowRowDto
            {
                AccountNo = accNo,
                AccountCode = acc?.AccountCode,
                AccountName = acc?.AccountName,
                ControlType = acc?.ControlType,
                Opening = op,
                Receipts = rec,
                Payments = pay,
                Closing = close
            });

            totOpen += op; totRec += rec; totPay += pay; totClose += close;
        }

        rows.Sort((a, b) => string.Compare(a.AccountCode, b.AccountCode, StringComparison.Ordinal));

        return new Fin1306CashFlowDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            OpeningCash = totOpen,
            TotalReceipts = totRec,
            TotalPayments = totPay,
            NetCashFlow = totRec - totPay,
            ClosingCash = totClose,
            Rows = rows
        };
    }

    public async Task<Fin1307AgingDto> GetAgingAsync(short partyType, DateTime asOfDate, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        // Load vouchers first (company-scoped), then only their details.
        var vouchers = await _db.FinVouchers.AsNoTracking()
            .Where(v => v.CompanyNo == companyNo && v.VoucherDate <= asOfDate && v.Status == 2 && v.IsDeleted == 0)
            .ToDictionaryAsync(v => v.VoucherNo, ct);

        var voucherNos = vouchers.Keys.ToList();

        var dtls = await _db.FinVoucherDtls.AsNoTracking()
            .Where(d => voucherNos.Contains(d.VoucherNo) && d.PartyType == partyType && d.PartyNo.HasValue && d.IsDeleted == 0)
            .ToListAsync(ct);

        var groups = dtls.GroupBy(d => d.PartyNo!.Value);

        var rows = new List<Fin1307AgingRowDto>();

        foreach (var g in groups)
        {
            decimal current = 0, days31_60 = 0, days61_90 = 0, daysOver90 = 0;

            foreach (var d in g)
            {
                decimal amount = d.Debit - d.Credit;
                if (!vouchers.TryGetValue(d.VoucherNo, out var v)) continue;
                int days = (asOfDate - v.VoucherDate).Days;

                if (days <= 30) current += amount;
                else if (days <= 60) days31_60 += amount;
                else if (days <= 90) days61_90 += amount;
                else daysOver90 += amount;
            }

            rows.Add(new Fin1307AgingRowDto
            {
                PartyNo = g.Key,
                PartyName = $"Party #{g.Key}",
                CurrentAmount = current,
                Days3160 = days31_60,
                Days6190 = days61_90,
                DaysOver90 = daysOver90,
                TotalOutstanding = current + days31_60 + days61_90 + daysOver90
            });
        }

        return new Fin1307AgingDto
        {
            AsOfDate = asOfDate,
            PartyType = partyType,
            Rows = rows,
            GrandTotal = rows.Sum(r => r.TotalOutstanding)
        };
    }
}
