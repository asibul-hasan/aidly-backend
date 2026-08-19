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
    Task<List<Fin1301TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime? asOfDate, long? branchNo = null, CancellationToken ct = default);
    Task<Fin1302LedgerDto> GetGeneralLedgerAsync(long accountNo, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<List<Fin1303DayBookRowDto>> GetDayBookAsync(DateTime fromDate, DateTime toDate, long? branchNo = null, CancellationToken ct = default);
    Task<Fin1304PnlDto> GetPnlAsync(DateTime fromDate, DateTime toDate, long? branchNo = null, CancellationToken ct = default);
    Task<Fin1305BalanceSheetDto> GetBalanceSheetAsync(DateTime asOfDate, long? branchNo = null, CancellationToken ct = default);
    Task<Fin1306CashFlowDto> GetCashFlowAsync(DateTime fromDate, DateTime toDate, long? branchNo = null, CancellationToken ct = default);
    Task<Fin1307AgingDto> GetAgingAsync(short partyType, DateTime asOfDate, CancellationToken ct = default);
}

public class FinReportService : IFinReportService
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPartyLookup _partyLookup;
    private readonly IFinCalendar _calendar;

    public FinReportService(IFinDbContext db, ICompanyBranchContext ctx, IPartyLookup partyLookup, IFinCalendar calendar)
    {
        _db = db;
        _ctx = ctx;
        _partyLookup = partyLookup;
        _calendar = calendar;
    }

    public async Task<List<Fin1301TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime? asOfDate, long? branchNo = null, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        DateTime cutoff = asOfDate ?? DateTime.UtcNow.Date;

        var accounts = await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0 && a.IsPostable == 1)
            .OrderBy(a => a.AccountCode)
            .ToListAsync(ct);

        var ledgerQuery = _db.FinLedgers
            .AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.VoucherDate <= cutoff && l.IsDeleted == 0);

        if (branchNo.HasValue)
            ledgerQuery = ledgerQuery.Where(l => l.BranchNo == branchNo.Value);

        var ledgerSums = await ledgerQuery
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

            // Opening balance is ledger-derived only — fin_account.opening_balance is a data-entry seed, never read here.
            decimal closing = a.NormalBalance == "dr" ? (deb - cred) : (cred - deb);

            result.Add(new Fin1301TrialBalanceRowDto
            {
                AccountNo = a.AccountNo,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                RootType = a.RootType,
                OpeningBalance = 0m,
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

        // Opening balance is ledger-derived only — fin_account.opening_balance is a data-entry seed.
        decimal openBal = acc.NormalBalance == "dr"
            ? (priorDeb - priorCred)
            : (priorCred - priorDeb);

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

    public async Task<List<Fin1303DayBookRowDto>> GetDayBookAsync(DateTime fromDate, DateTime toDate, long? branchNo = null, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        // Day Book reads from fin_ledger — the single source of truth.
        // Cancelled vouchers appear as original + reversal rows, netting to zero.
        var ledgerQuery = _db.FinLedgers.AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.VoucherDate >= fromDate && l.VoucherDate <= toDate && l.IsDeleted == 0);

        if (branchNo.HasValue)
            ledgerQuery = ledgerQuery.Where(l => l.BranchNo == branchNo.Value);

        var ledgerRows = await ledgerQuery
            .OrderBy(l => l.VoucherDate).ThenBy(l => l.VoucherNo).ThenBy(l => l.LedgerNo)
            .ToListAsync(ct);

        var voucherNos = ledgerRows.Select(l => l.VoucherNo).Distinct().ToList();
        var vouchers = await _db.FinVouchers.AsNoTracking()
            .Where(v => voucherNos.Contains(v.VoucherNo))
            .ToDictionaryAsync(v => v.VoucherNo, ct);

        var typeNos = vouchers.Values.Select(v => v.VoucherTypeNo).Distinct().ToList();
        var types = await _db.FinVoucherTypes.AsNoTracking()
            .Where(t => typeNos.Contains(t.VoucherTypeNo))
            .ToDictionaryAsync(t => t.VoucherTypeNo, t => t.VoucherTypeName, ct);

        var accountNos = ledgerRows.Select(l => l.AccountNo).Distinct().ToList();
        var accounts = await _db.FinAccounts.AsNoTracking()
            .Where(a => accountNos.Contains(a.AccountNo) && a.IsDeleted == 0)
            .ToDictionaryAsync(a => a.AccountNo, ct);

        var result = new List<Fin1303DayBookRowDto>();

        foreach (var l in ledgerRows)
        {
            vouchers.TryGetValue(l.VoucherNo, out var v);
            accounts.TryGetValue(l.AccountNo, out var a);

            result.Add(new Fin1303DayBookRowDto
            {
                VoucherNo = l.VoucherNo,
                VoucherId = v?.VoucherId,
                VoucherTypeName = v != null && types.TryGetValue(v.VoucherTypeNo, out var tn) ? tn : "Journal",
                VoucherDate = l.VoucherDate,
                Narration = v?.Narration,
                AccountCode = a?.AccountCode,
                AccountName = a?.AccountName,
                Debit = l.Debit,
                Credit = l.Credit
            });
        }

        return result;
    }

    public async Task<Fin1304PnlDto> GetPnlAsync(DateTime fromDate, DateTime toDate, long? branchNo = null, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        // P&L is a FLOW statement: windowed on [fromDate, toDate], never cumulative.
        var ledgerQuery = _db.FinLedgers
            .AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.VoucherDate >= fromDate && l.VoucherDate <= toDate && l.IsDeleted == 0);

        if (branchNo.HasValue)
            ledgerQuery = ledgerQuery.Where(l => l.BranchNo == branchNo.Value);

        var ledgerSums = await ledgerQuery
            .GroupBy(l => l.AccountNo)
            .Select(g => new
            {
                AccountNo = g.Key,
                TotalDebit = g.Sum(x => x.Debit),
                TotalCredit = g.Sum(x => x.Credit)
            })
            .ToDictionaryAsync(x => x.AccountNo, ct);

        var accountNos = ledgerSums.Keys.ToList();
        var accounts = await _db.FinAccounts
            .AsNoTracking()
            .Where(a => accountNos.Contains(a.AccountNo) && a.IsDeleted == 0)
            .ToDictionaryAsync(a => a.AccountNo, ct);

        var revenueRows = new List<FinStatementRowDto>();
        var expenseRows = new List<FinStatementRowDto>();
        decimal totalRev = 0, totalExp = 0;

        foreach (var kv in ledgerSums)
        {
            if (!accounts.TryGetValue(kv.Key, out var acc)) continue;
            decimal movement = 0;

            if (acc.RootType == 4) // Revenue
            {
                movement = kv.Value.TotalCredit - kv.Value.TotalDebit;
                if (movement == 0) continue;
                revenueRows.Add(new FinStatementRowDto
                {
                    AccountNo = acc.AccountNo,
                    AccountCode = acc.AccountCode,
                    AccountName = acc.AccountName,
                    RootType = acc.RootType,
                    Amount = movement
                });
                totalRev += movement;
            }
            else if (acc.RootType == 5) // Expense
            {
                movement = kv.Value.TotalDebit - kv.Value.TotalCredit;
                if (movement == 0) continue;
                expenseRows.Add(new FinStatementRowDto
                {
                    AccountNo = acc.AccountNo,
                    AccountCode = acc.AccountCode,
                    AccountName = acc.AccountName,
                    RootType = acc.RootType,
                    Amount = movement
                });
                totalExp += movement;
            }
        }

        revenueRows.Sort((a, b) => string.Compare(a.AccountCode, b.AccountCode, StringComparison.Ordinal));
        expenseRows.Sort((a, b) => string.Compare(a.AccountCode, b.AccountCode, StringComparison.Ordinal));

        return new Fin1304PnlDto
        {
            RevenueRows = revenueRows,
            TotalRevenue = totalRev,
            ExpenseRows = expenseRows,
            TotalExpense = totalExp,
            NetProfit = totalRev - totalExp
        };
    }

    public async Task<Fin1305BalanceSheetDto> GetBalanceSheetAsync(DateTime asOfDate, long? branchNo = null, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var tb = await GetTrialBalanceAsync(asOfDate, branchNo, ct);

        // Resolve fiscal-year start for the P&L window
        string? warning = null;
        DateTime pnlFromDate;
        var finYear = await _calendar.FindYearForDateAsync(companyNo, DateOnly.FromDateTime(asOfDate), ct);
        if (finYear != null)
        {
            pnlFromDate = finYear.StartDate.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            // No fiscal year covers the date — fall back to calendar year with explicit warning
            pnlFromDate = asOfDate.AddYears(-1);
            warning = $"No fiscal year covers {asOfDate:yyyy-MM-dd}; current-year earnings assume a calendar year";
        }

        var pnl = await GetPnlAsync(pnlFromDate, asOfDate, branchNo, ct);

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
            RetainedEarnings = pnl.NetProfit,
            IsBalanced = Math.Abs(totalAssets - (totalLiabilities + totalEquity + pnl.NetProfit)) < 0.005m,
            Warning = warning
        };
    }

    public async Task<Fin1306CashFlowDto> GetCashFlowAsync(DateTime fromDate, DateTime toDate, long? branchNo = null, CancellationToken ct = default)
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
        var openingQuery = _db.FinLedgers.AsNoTracking()
            .Where(l => cashAccNos.Contains(l.AccountNo) && l.VoucherDate <= openingDate && l.IsDeleted == 0);
        if (branchNo.HasValue) openingQuery = openingQuery.Where(l => l.BranchNo == branchNo.Value);

        var openingLedger = await openingQuery
            .Select(l => new { l.AccountNo, l.Debit, l.Credit })
            .ToListAsync(ct);
        var opening = openingLedger
            .GroupBy(l => l.AccountNo)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));

        // Period movements = debits (receipts) and credits (payments) within [fromDate, toDate].
        var moveQuery = _db.FinLedgers.AsNoTracking()
            .Where(l => cashAccNos.Contains(l.AccountNo) && l.VoucherDate >= fromDate && l.VoucherDate <= toDate && l.IsDeleted == 0);
        if (branchNo.HasValue) moveQuery = moveQuery.Where(l => l.BranchNo == branchNo.Value);

        var moveLedger = await moveQuery
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

        // Aging reads from fin_ledger — the single source of truth.
        // Restrict to control accounts: AR (ControlType=1) for customers, AP (ControlType=2) for suppliers.
        short controlType = partyType == 1 ? (short)1 : (short)2;

        var controlAccounts = await _db.FinAccounts.AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.ControlType == controlType && a.IsDeleted == 0)
            .Select(a => a.AccountNo)
            .ToListAsync(ct);

        if (controlAccounts.Count == 0)
        {
            return new Fin1307AgingDto { AsOfDate = asOfDate, PartyType = partyType, Rows = new(), GrandTotal = 0 };
        }

        // Load ledger rows for control accounts with party info
        var ledgerRows = await _db.FinLedgers.AsNoTracking()
            .Where(l => controlAccounts.Contains(l.AccountNo) && l.CompanyNo == companyNo
                        && l.VoucherDate <= asOfDate && l.PartyType == partyType && l.PartyNo.HasValue
                        && l.IsDeleted == 0)
            .ToListAsync(ct);

        // Group by party and compute aging buckets
        var groups = ledgerRows.GroupBy(l => l.PartyNo!.Value);
        var partyNos = groups.Select(g => g.Key).ToList();

        // Batch-load party names in ONE query
        var partyNames = await _partyLookup.GetPartyNamesAsync(partyType, partyNos, ct);

        var rows = new List<Fin1307AgingRowDto>();

        foreach (var g in groups)
        {
            decimal current = 0, days31_60 = 0, days61_90 = 0, daysOver90 = 0;

            // Age the OPEN balance, not each ledger line by its own date.
            //
            // Bucketing every line independently put a payment in whichever bucket its own date
            // fell in, while the invoice it settled stayed in another. A 200-day-old invoice paid
            // last week reported as 1,000 in "90+ days" and -1,000 in "current": the party owes
            // nothing, yet the report shows them as materially overdue and a negative current
            // balance. The total was right; every bucket was wrong — and the buckets are the entire
            // point of an aging report, since they decide who gets chased.
            //
            // Settlements are applied oldest-invoice-first (FIFO), which is how a supplier or
            // customer statement is read when no explicit allocation is recorded. What remains of
            // each invoice is then aged by THAT invoice's date.
            var opens = new List<(DateTime Date, decimal Remaining)>();
            decimal unapplied = 0m;

            foreach (var l in g.OrderBy(x => x.VoucherDate).ThenBy(x => x.LedgerNo))
            {
                // AR: debit increases receivable; AP: credit increases payable
                decimal amount = partyType == 1 ? (l.Debit - l.Credit) : (l.Credit - l.Debit);

                if (amount > 0)
                {
                    opens.Add((l.VoucherDate, amount));
                }
                else if (amount < 0)
                {
                    decimal settle = -amount;
                    for (int i = 0; i < opens.Count && settle > 0; i++)
                    {
                        if (opens[i].Remaining <= 0) continue;
                        decimal take = Math.Min(settle, opens[i].Remaining);
                        opens[i] = (opens[i].Date, opens[i].Remaining - take);
                        settle -= take;
                    }
                    // An overpayment or a credit note with nothing left to settle is still money
                    // that moved — carry it so the party total stays reconcilable to the ledger.
                    unapplied -= settle;
                }
            }

            foreach (var o in opens)
            {
                if (o.Remaining <= 0) continue;
                int days = (asOfDate - o.Date).Days;

                if (days <= 30) current += o.Remaining;
                else if (days <= 60) days31_60 += o.Remaining;
                else if (days <= 90) days61_90 += o.Remaining;
                else daysOver90 += o.Remaining;
            }

            // Credit sitting on the account belongs in the newest bucket — it is not overdue.
            current += unapplied;

            rows.Add(new Fin1307AgingRowDto
            {
                PartyNo = g.Key,
                PartyName = partyNames.GetValueOrDefault(g.Key) ?? $"Party #{g.Key}",
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
            Rows = rows.OrderByDescending(r => r.TotalOutstanding).ToList(),
            GrandTotal = rows.Sum(r => r.TotalOutstanding)
        };
    }
}
