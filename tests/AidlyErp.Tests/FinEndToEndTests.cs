using AidlyErp.Fin.Application.Services;
using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Core;
using AidlyErp.Sys.Contracts;
using AidlyErp.Pur.Contracts;
using AidlyErp.Sal.Contracts;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AidlyErp.Tests;

/// <summary>
/// End-to-end integration test exercising the full money path.
/// Each assertion maps to a specific defect this programme fixed.
/// Uses in-memory database — same pattern as FinTenantIsolationTests.
/// </summary>
public class FinEndToEndTests
{
    private static FinInMemoryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FinInMemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FinInMemoryDbContext(options);
    }

    private static ICompanyBranchContext CreateContext(long companyNo = 1, long branchNo = 1, long userNo = 1)
    {
        var ctx = new Mock<ICompanyBranchContext>();
        ctx.Setup(c => c.CompanyNo).Returns(companyNo);
        ctx.Setup(c => c.BranchNo).Returns(branchNo);
        ctx.Setup(c => c.UserNo).Returns(userNo);
        return ctx.Object;
    }

    private static IFinCalendar CreateCalendar()
    {
        var calendar = new Mock<IFinCalendar>();
        // Stub FindYearForDateAsync to return a fiscal year
        calendar.Setup(c => c.FindYearForDateAsync(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinYearInfo(1, 1, "FY2026", "FY2026", "FY2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 1, 0, null));
        // Stub FindPeriodForDateAsync to return an open period
        calendar.Setup(c => c.FindPeriodForDateAsync(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinPeriodInfo(1, 1, "P01", "January", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 1, 1, 0));
        // Stub FindPeriodAsync for period status checks
        calendar.Setup(c => c.FindPeriodAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinPeriodInfo(1, 1, "P01", "January", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 1, 1, 0));
        return calendar.Object;
    }

    private static IDocSequenceGenerator CreateDocSeq()
    {
        var seq = new Mock<IDocSequenceGenerator>();
        seq.Setup(d => d.NextAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("TEST-0001");
        return seq.Object;
    }

    private static ICurrencyLookup CreateCurrencyLookup()
    {
        var lookup = new Mock<ICurrencyLookup>();
        lookup.Setup(l => l.GetBaseCurrencyNoAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);
        return lookup.Object;
    }

    private static IApprovalService CreateApprovalService()
    {
        var svc = new Mock<IApprovalService>();
        svc.Setup(a => a.RaiseAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalOutcome(true, null)); // auto-approve
        return svc.Object;
    }

    /// <summary>
    /// Sprint 2 G1: Opening balance must NOT double-count.
    /// Trial Balance must show Cash = 100,000 — NOT 200,000.
    /// </summary>
    [Fact]
    public async Task OpeningBalance_TrialBalance_MustNotDoubleCount()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var calendar = CreateCalendar();
        var docSeq = CreateDocSeq();
        var currencyLookup = CreateCurrencyLookup();
        var approvalService = CreateApprovalService();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();

        // Seed chart of accounts
        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 1, CompanyNo = 1, AccountCode = "1010", AccountName = "Cash", AccountGroupNo = 1, RootType = 1, NormalBalance = "dr", IsPostable = 1, IsActive = 1, IsDeleted = 0, OpeningBalance = 0, OpeningDrCr = "dr" },
            new FinAccount { AccountNo = 2, CompanyNo = 1, AccountCode = "3010", AccountName = "Equity", AccountGroupNo = 2, RootType = 3, NormalBalance = "cr", IsPostable = 1, IsActive = 1, IsDeleted = 0, OpeningBalance = 0, OpeningDrCr = "cr" }
        );

        db.FinVoucherTypes.Add(new FinVoucherType { VoucherTypeNo = 1, CompanyNo = 1, VoucherTypeCode = "OPN", VoucherTypeName = "Opening", BaseKind = 7, Prefix = "OPN", IsActive = 1, IsDeleted = 0 });
        await db.SaveChangesAsync(CancellationToken.None);

        // Post opening balance: Dr Cash 100,000 / Cr Equity 100,000
        var voucherService = new Fin1101Service(db, ctx, approvalService, calendar, uow.Object, docSeq, currencyLookup);
        var openingService = new Fin1004Service(db, ctx, calendar, voucherService, uow.Object);

        uow.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Fin1004ResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Fin1004ResultDto>> fn, CancellationToken ct) => fn(ct));

        var openingDto = new Fin1004OpeningBalanceDto
        {
            AsOfDate = new DateTime(2026, 1, 1),
            Lines = new List<Fin1004OpeningLineDto>
            {
                new() { AccountNo = 1, OpeningBalance = 100000, OpeningDrCr = "dr" },
                new() { AccountNo = 2, OpeningBalance = 100000, OpeningDrCr = "cr" }
            }
        };

        var result = await openingService.SaveOpeningBalancesAsync(openingDto);
        Assert.Equal(100000, result.TotalDebit);
        Assert.Equal(100000, result.TotalCredit);

        // ASSERT: Trial Balance shows Cash = 100,000 — NOT 200,000
        // This is the opening double-count regression from Sprint 2 G1
        var reportService = new FinReportService(db, ctx, new Mock<IPartyLookup>().Object, calendar);
        var tb = await reportService.GetTrialBalanceAsync(new DateTime(2026, 1, 31));

        var cashRow = tb.First(r => r.AccountNo == 1);
        Assert.Equal(100000, cashRow.ClosingBalance); // Must be exactly 100,000

        // Trial Balance must balance
        var totalDebit = tb.Sum(r => r.Debit);
        var totalCredit = tb.Sum(r => r.Credit);
        Assert.True(Math.Abs(totalDebit - totalCredit) < 0.01m, $"Trial balance out of balance: Dr={totalDebit} Cr={totalCredit}");
    }

    /// <summary>
    /// Sprint 2 G2: P&L must respect its date window.
    /// Revenue in January must NOT appear in a February-only P&L.
    /// </summary>
    [Fact]
    public async Task PnL_MustRespectDateWindow()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var calendar = CreateCalendar();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();

        // Seed accounts
        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 1, CompanyNo = 1, AccountCode = "1010", AccountName = "Cash", AccountGroupNo = 1, RootType = 1, NormalBalance = "dr", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 3, CompanyNo = 1, AccountCode = "5010", AccountName = "Revenue", AccountGroupNo = 3, RootType = 4, NormalBalance = "cr", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );

        // Seed ledger: 50,000 revenue in January, 30,000 in February
        db.FinLedgers.AddRange(
            new FinLedger { LedgerNo = 1, CompanyNo = 1, BranchNo = 1, AccountNo = 3, VoucherNo = 1, VoucherDtlNo = 1, VoucherDate = new DateTime(2026, 1, 15), FinYearNo = 1, FinPeriodNo = 1, Debit = 0, Credit = 50000, IsDeleted = 0 },
            new FinLedger { LedgerNo = 2, CompanyNo = 1, BranchNo = 1, AccountNo = 1, VoucherNo = 1, VoucherDtlNo = 2, VoucherDate = new DateTime(2026, 1, 15), FinYearNo = 1, FinPeriodNo = 1, Debit = 50000, Credit = 0, IsDeleted = 0 },
            new FinLedger { LedgerNo = 3, CompanyNo = 1, BranchNo = 1, AccountNo = 3, VoucherNo = 2, VoucherDtlNo = 3, VoucherDate = new DateTime(2026, 2, 15), FinYearNo = 1, FinPeriodNo = 2, Debit = 0, Credit = 30000, IsDeleted = 0 },
            new FinLedger { LedgerNo = 4, CompanyNo = 1, BranchNo = 1, AccountNo = 1, VoucherNo = 2, VoucherDtlNo = 4, VoucherDate = new DateTime(2026, 2, 15), FinYearNo = 1, FinPeriodNo = 2, Debit = 30000, Credit = 0, IsDeleted = 0 }
        );
        await db.SaveChangesAsync(CancellationToken.None);

        // ASSERT: P&L for February only must show 30,000 — NOT 80,000
        // This is the windowing fix from Sprint 2 G2
        var reportService = new FinReportService(db, ctx, new Mock<IPartyLookup>().Object, calendar);
        var pnl = await reportService.GetPnlAsync(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

        Assert.Equal(30000, pnl.TotalRevenue);
        Assert.NotEqual(80000, pnl.TotalRevenue); // Must NOT be cumulative
    }

    /// <summary>
    /// Sprint 2 G6: Day Book must show both original and reversal on cancel.
    /// </summary>
    [Fact]
    public async Task Cancel_ShowsBothOriginalAndReversal_InDayBook()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var calendar = CreateCalendar();
        var docSeq = CreateDocSeq();
        var currencyLookup = CreateCurrencyLookup();
        var approvalService = CreateApprovalService();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();

        // Seed accounts and voucher
        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 1, CompanyNo = 1, AccountCode = "1010", AccountName = "Cash", AccountGroupNo = 1, RootType = 1, NormalBalance = "dr", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 3, CompanyNo = 1, AccountCode = "5010", AccountName = "Revenue", AccountGroupNo = 3, RootType = 4, NormalBalance = "cr", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );
        db.FinVoucherTypes.Add(new FinVoucherType { VoucherTypeNo = 1, CompanyNo = 1, VoucherTypeCode = "JV", VoucherTypeName = "Journal", BaseKind = 1, Prefix = "JV", IsActive = 1, IsDeleted = 0 });
        db.FinVouchers.Add(new FinVoucher { VoucherNo = 1, CompanyNo = 1, BranchNo = 1, VoucherId = "JV-000001", VoucherTypeNo = 1, VoucherDate = new DateTime(2026, 1, 15), FinYearNo = 1, FinPeriodNo = 1, Status = 2, TotalDebit = 1000, TotalCredit = 1000, IsActive = 1, IsDeleted = 0 });
        db.FinVoucherDtls.AddRange(
            new FinVoucherDtl { VoucherDtlNo = 1, VoucherNo = 1, LineNo = 1, AccountNo = 1, Debit = 1000, Credit = 0, IsActive = 1, IsDeleted = 0 },
            new FinVoucherDtl { VoucherDtlNo = 2, VoucherNo = 1, LineNo = 2, AccountNo = 3, Debit = 0, Credit = 1000, IsActive = 1, IsDeleted = 0 }
        );
        // Seed original ledger entries
        db.FinLedgers.AddRange(
            new FinLedger { LedgerNo = 1, CompanyNo = 1, BranchNo = 1, AccountNo = 1, VoucherNo = 1, VoucherDtlNo = 1, VoucherDate = new DateTime(2026, 1, 15), FinYearNo = 1, FinPeriodNo = 1, Debit = 1000, Credit = 0, IsReversal = 0, IsActive = 1, IsDeleted = 0 },
            new FinLedger { LedgerNo = 2, CompanyNo = 1, BranchNo = 1, AccountNo = 3, VoucherNo = 1, VoucherDtlNo = 2, VoucherDate = new DateTime(2026, 1, 15), FinYearNo = 1, FinPeriodNo = 1, Debit = 0, Credit = 1000, IsReversal = 0, IsActive = 1, IsDeleted = 0 }
        );
        await db.SaveChangesAsync(CancellationToken.None);

        var voucherService = new Fin1101Service(db, ctx, approvalService, calendar, uow.Object, docSeq, currencyLookup);

        uow.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Fin1101VoucherDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Fin1101VoucherDto>> fn, CancellationToken ct) => fn(ct));

        // Cancel the posted voucher
        await voucherService.CancelAsync(1);

        // ASSERT: Day Book shows BOTH the original and the reversal
        // This is the Sprint 2 G6 fix
        var reportService = new FinReportService(db, ctx, new Mock<IPartyLookup>().Object, calendar);
        var dayBook = await reportService.GetDayBookAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        // Must have 4 rows: 2 original + 2 reversal
        Assert.Equal(4, dayBook.Count);

        // Net must be zero
        var totalDebit = dayBook.Sum(r => r.Debit);
        var totalCredit = dayBook.Sum(r => r.Credit);
        Assert.True(Math.Abs(totalDebit - totalCredit) < 0.01m, $"Day book not balanced: Dr={totalDebit} Cr={totalCredit}");
    }

    /// <summary>
    /// Multi-currency dual stamping test:
    /// Section 5.3 of acc-business-doc.md:
    /// FC is converted by FxRate to BC and balances in base currency.
    /// </summary>
    [Fact]
    public async Task MultiCurrency_Voucher_StampsBothFCAndBC_AndBalances()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var calendar = CreateCalendar();
        var docSeq = CreateDocSeq();
        var currencyLookup = CreateCurrencyLookup();
        var approvalService = CreateApprovalService();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();

        // Seed chart of accounts
        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 1, CompanyNo = 1, AccountCode = "1010", AccountName = "USD Bank", AccountGroupNo = 1, RootType = 1, NormalBalance = "dr", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 2, CompanyNo = 1, AccountCode = "4010", AccountName = "Export Revenue", AccountGroupNo = 4, RootType = 4, NormalBalance = "cr", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );

        db.FinVoucherTypes.Add(new FinVoucherType { VoucherTypeNo = 1, CompanyNo = 1, VoucherTypeCode = "JV", VoucherTypeName = "Journal", BaseKind = 1, Prefix = "JV", IsActive = 1, IsDeleted = 0 });
        await db.SaveChangesAsync(CancellationToken.None);

        var voucherService = new Fin1101Service(db, ctx, approvalService, calendar, uow.Object, docSeq, currencyLookup);
        uow.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Fin1101VoucherDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Fin1101VoucherDto>> fn, CancellationToken ct) => fn(ct));

        // Create voucher in USD (currencyNo = 2, base currency is 1) with fx_rate = 120.00
        var dto = new Fin1101VoucherDto
        {
            VoucherTypeNo = 1,
            VoucherDate = new DateTime(2026, 1, 15),
            Narration = "Export receipt USD 100 @ 120",
            CurrencyNo = 2,
            FxRate = 120.00m,
            Lines = new List<Fin1101VoucherLineDto>
            {
                new() { AccountNo = 1, LineNo = 1, DebitFc = 100.00m, DrCr = "dr" },
                new() { AccountNo = 2, LineNo = 2, CreditFc = 100.00m, DrCr = "cr" }
            }
        };

        var saved = await voucherService.SaveAsync(dto);

        // Assert header conversion & balance
        Assert.NotNull(saved.VoucherNo);
        Assert.Equal(12000.00m, saved.TotalDebit);
        Assert.Equal(12000.00m, saved.TotalCredit);
        Assert.Equal(120.00m, saved.FxRate);
        Assert.Equal(2, saved.CurrencyNo);

        // Assert detail lines have both FC and BC
        Assert.Equal(2, saved.Lines.Count);
        var drLine = saved.Lines.First(l => l.DrCr == "dr");
        Assert.Equal(100.00m, drLine.DebitFc);
        Assert.Equal(12000.00m, drLine.Debit);

        var crLine = saved.Lines.First(l => l.DrCr == "cr");
        Assert.Equal(100.00m, crLine.CreditFc);
        Assert.Equal(12000.00m, crLine.Credit);

        // Submit voucher to post to ledger
        var submitted = await voucherService.SubmitAsync(saved.VoucherNo.Value);
        Assert.Equal((short)2, submitted.Status); // 2 = Posted

        // Verify ledger entries
        var ledgerRows = await db.FinLedgers.Where(l => l.VoucherNo == saved.VoucherNo.Value).ToListAsync();
        Assert.Equal(2, ledgerRows.Count);
        Assert.Equal(12000.00m, ledgerRows.Sum(l => l.Debit));
        Assert.Equal(12000.00m, ledgerRows.Sum(l => l.Credit));
    }

    /// <summary>
    /// Section 5.1 of acc-business-doc.md:
    /// Zero Imbalance Constraint: Sum(Debits) - Sum(Credits) = 0.0000.
    /// A voucher with non-zero difference must throw ValidationException.
    /// </summary>
    [Fact]
    public async Task Voucher_ZeroImbalance_ThrowsValidationException()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var calendar = CreateCalendar();
        var docSeq = CreateDocSeq();
        var currencyLookup = CreateCurrencyLookup();
        var approvalService = CreateApprovalService();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();

        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 1, CompanyNo = 1, AccountCode = "1010", AccountName = "Cash", AccountGroupNo = 1, RootType = 1, NormalBalance = "dr", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 2, CompanyNo = 1, AccountCode = "4010", AccountName = "Revenue", AccountGroupNo = 4, RootType = 4, NormalBalance = "cr", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );
        db.FinVoucherTypes.Add(new FinVoucherType { VoucherTypeNo = 1, CompanyNo = 1, VoucherTypeCode = "JV", VoucherTypeName = "Journal", BaseKind = 1, Prefix = "JV", IsActive = 1, IsDeleted = 0 });
        await db.SaveChangesAsync(CancellationToken.None);

        var voucherService = new Fin1101Service(db, ctx, approvalService, calendar, uow.Object, docSeq, currencyLookup);
        uow.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Fin1101VoucherDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Fin1101VoucherDto>> fn, CancellationToken ct) => fn(ct));

        // Unbalanced voucher: Dr 100 vs Cr 90
        var unbalancedDto = new Fin1101VoucherDto
        {
            VoucherTypeNo = 1,
            VoucherDate = new DateTime(2026, 1, 15),
            Narration = "Unbalanced voucher",
            Lines = new List<Fin1101VoucherLineDto>
            {
                new() { AccountNo = 1, LineNo = 1, Debit = 100.00m, Credit = 0m },
                new() { AccountNo = 2, LineNo = 2, Debit = 0m, Credit = 90.00m }
            }
        };

        await Assert.ThrowsAsync<ValidationException>(() => voucherService.SaveAsync(unbalancedDto));
    }

    /// <summary>
    /// Section 5.2 of acc-business-doc.md:
    /// Leaf Node Enforcement: direct GL entries are strictly blocked against parent/group accounts.
    /// Attempting to post to an account with IsPostable = 0 must throw ValidationException.
    /// </summary>
    [Fact]
    public async Task Voucher_NonPostableHeaderAccount_ThrowsValidationException()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var calendar = CreateCalendar();
        var docSeq = CreateDocSeq();
        var currencyLookup = CreateCurrencyLookup();
        var approvalService = CreateApprovalService();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();

        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 1, CompanyNo = 1, AccountCode = "1000", AccountName = "Current Assets (Header)", AccountGroupNo = 1, RootType = 1, NormalBalance = "dr", IsPostable = 0, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 2, CompanyNo = 1, AccountCode = "4010", AccountName = "Revenue", AccountGroupNo = 4, RootType = 4, NormalBalance = "cr", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );
        db.FinVoucherTypes.Add(new FinVoucherType { VoucherTypeNo = 1, CompanyNo = 1, VoucherTypeCode = "JV", VoucherTypeName = "Journal", BaseKind = 1, Prefix = "JV", IsActive = 1, IsDeleted = 0 });
        await db.SaveChangesAsync(CancellationToken.None);

        var voucherService = new Fin1101Service(db, ctx, approvalService, calendar, uow.Object, docSeq, currencyLookup);
        uow.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Fin1101VoucherDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Fin1101VoucherDto>> fn, CancellationToken ct) => fn(ct));

        var dto = new Fin1101VoucherDto
        {
            VoucherTypeNo = 1,
            VoucherDate = new DateTime(2026, 1, 15),
            Narration = "Posting to header account",
            Lines = new List<Fin1101VoucherLineDto>
            {
                new() { AccountNo = 1, LineNo = 1, Debit = 100.00m, Credit = 0m },
                new() { AccountNo = 2, LineNo = 2, Debit = 0m, Credit = 100.00m }
            }
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => voucherService.SaveAsync(dto));
        Assert.Contains("not postable", ex.Message);
    }

    /// <summary>
    /// Section 5.4 of acc-business-doc.md:
    /// Period Locking Rule: Posting to a closed period must throw ValidationException.
    /// </summary>
    [Fact]
    public async Task Voucher_ClosedPeriod_ThrowsValidationException()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var calendar = new Mock<IFinCalendar>();
        var docSeq = CreateDocSeq();
        var currencyLookup = CreateCurrencyLookup();
        var approvalService = CreateApprovalService();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();

        calendar.Setup(c => c.FindYearForDateAsync(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinYearInfo(1, 1, "FY2026", "FY2026", "FY2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 1, 0, null));
        // Period status 2 = Closed
        calendar.Setup(c => c.FindPeriodForDateAsync(It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinPeriodInfo(1, 1, "P01", "January", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 2, 0, 0));
        calendar.Setup(c => c.FindPeriodAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinPeriodInfo(1, 1, "P01", "January", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 2, 0, 0));

        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 1, CompanyNo = 1, AccountCode = "1010", AccountName = "Cash", AccountGroupNo = 1, RootType = 1, NormalBalance = "dr", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 2, CompanyNo = 1, AccountCode = "4010", AccountName = "Revenue", AccountGroupNo = 4, RootType = 4, NormalBalance = "cr", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );
        db.FinVoucherTypes.Add(new FinVoucherType { VoucherTypeNo = 1, CompanyNo = 1, VoucherTypeCode = "JV", VoucherTypeName = "Journal", BaseKind = 1, Prefix = "JV", IsActive = 1, IsDeleted = 0 });
        await db.SaveChangesAsync(CancellationToken.None);

        var voucherService = new Fin1101Service(db, ctx, approvalService, calendar.Object, uow.Object, docSeq, currencyLookup);
        uow.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Fin1101VoucherDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Fin1101VoucherDto>> fn, CancellationToken ct) => fn(ct));

        var dto = new Fin1101VoucherDto
        {
            VoucherTypeNo = 1,
            VoucherDate = new DateTime(2026, 1, 15),
            Narration = "Posting to closed period",
            Lines = new List<Fin1101VoucherLineDto>
            {
                new() { AccountNo = 1, LineNo = 1, Debit = 100.00m, Credit = 0m },
                new() { AccountNo = 2, LineNo = 2, Debit = 0m, Credit = 100.00m }
            }
        };

        var saved = await voucherService.SaveAsync(dto);
        Assert.NotNull(saved.VoucherNo);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => voucherService.SubmitAsync(saved.VoucherNo.Value));
        Assert.Contains("not Open", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Section 4.3 & FIN_1102:
    /// Bank Reconciliation Worksheet: calculates book balance, cleared lines, and uncleared difference.
    /// </summary>
    [Fact]
    public async Task BankRecon_Worksheet_CalculatesBookBalanceAndClearedItems()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();

        // Bank control account (ControlType = 3)
        db.FinAccounts.Add(new FinAccount
        {
            AccountNo = 10, CompanyNo = 1, AccountCode = "1020", AccountName = "Primary Bank",
            ControlType = 3, RootType = 1, NormalBalance = "dr", IsPostable = 1, IsActive = 1, IsDeleted = 0
        });

        // Seed 2 ledger rows: Deposit $5,000 and Check $2,000
        db.FinLedgers.AddRange(
            new FinLedger { LedgerNo = 1, CompanyNo = 1, BranchNo = 1, AccountNo = 10, VoucherNo = 1, VoucherDtlNo = 1, VoucherDate = new DateTime(2026, 1, 10), Debit = 5000, Credit = 0, IsDeleted = 0 },
            new FinLedger { LedgerNo = 2, CompanyNo = 1, BranchNo = 1, AccountNo = 10, VoucherNo = 2, VoucherDtlNo = 2, VoucherDate = new DateTime(2026, 1, 20), Debit = 0, Credit = 2000, IsDeleted = 0 }
        );
        await db.SaveChangesAsync(CancellationToken.None);

        var reconService = new Fin1102Service(db, ctx, uow.Object);

        // Act: request worksheet as of Jan 31, 2026
        var ws = await reconService.GetWorksheetAsync(10, new DateTime(2026, 1, 31));

        // Assert book balance: 5000 - 2000 = 3000
        Assert.Equal(3000m, ws.BookBalance);
        Assert.Equal(2, ws.Lines.Count);
    }

    /// <summary>
    /// FIN_1202 / FIN_1203:
    /// Sub-Ledger Reconciliation: compares operational sub-ledger outstanding against GL control accounts.
    /// </summary>
    [Fact]
    public async Task SubLedger_Reconciliation_CalculatesDiscrepancyBetweenModuleAndGL()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var partyLookup = new Mock<IPartyLookup>();
        var apReader = new Mock<IPurApLedgerReader>();
        var arReader = new Mock<ISalArLedgerReader>();

        // Customer sub-ledger reports $15,000 outstanding for Customer 101
        arReader.Setup(r => r.GetOutstandingAsync(1, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArPartyBalance> { new(101, 15000m) });

        partyLookup.Setup(p => p.GetPartyNamesAsync(1, It.IsAny<List<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, string> { { 101, "Acme Corp" } });

        // Control account (ControlType = 1: Accounts Receivable)
        db.FinAccounts.Add(new FinAccount
        {
            AccountNo = 20, CompanyNo = 1, AccountCode = "1200", AccountName = "Accounts Receivable",
            ControlType = 1, RootType = 1, NormalBalance = "dr", IsPostable = 1, IsActive = 1, IsDeleted = 0
        });

        // GL has posted $15,000 to Customer 101
        db.FinLedgers.Add(new FinLedger
        {
            LedgerNo = 1, CompanyNo = 1, BranchNo = 1, AccountNo = 20, PartyType = 1, PartyNo = 101,
            VoucherDate = new DateTime(2026, 1, 15), Debit = 15000m, Credit = 0m, IsDeleted = 0
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var subLedgerService = new FinSubLedgerService(db, ctx, partyLookup.Object, apReader.Object, arReader.Object);

        var recon = await subLedgerService.GetReconciliationAsync(1, new DateTime(2026, 1, 31));

        Assert.Equal(15000m, recon.SubLedgerTotal);
        Assert.Equal(15000m, recon.GlControlTotal);
        Assert.Equal(0m, recon.Difference); // Fully reconciled!
    }

    // In-memory DbContext for testing
    private class FinInMemoryDbContext : DbContext, IFinDbContext
    {
        public FinInMemoryDbContext(DbContextOptions<FinInMemoryDbContext> options) : base(options) { }

        public DbSet<FinAccount> FinAccounts => Set<FinAccount>();
        public DbSet<FinAccountBalance> FinAccountBalances => Set<FinAccountBalance>();
        public DbSet<FinGlMap> FinGlMaps => Set<FinGlMap>();
        public DbSet<FinVoucher> FinVouchers => Set<FinVoucher>();
        public DbSet<FinVoucherDtl> FinVoucherDtls => Set<FinVoucherDtl>();
        public DbSet<FinVoucherType> FinVoucherTypes => Set<FinVoucherType>();
        public DbSet<FinLedger> FinLedgers => Set<FinLedger>();
        public DbSet<FinBankAccount> FinBankAccounts => Set<FinBankAccount>();
        public DbSet<FinBankRecon> FinBankRecons => Set<FinBankRecon>();
        public DbSet<FinBankReconLine> FinBankReconLines => Set<FinBankReconLine>();
        public DbSet<EventOutbox> EventOutboxes => Set<EventOutbox>();

        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade IFinDbContext.Database => Database;
    }
}
