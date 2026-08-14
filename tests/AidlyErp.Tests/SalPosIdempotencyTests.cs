using AidlyErp.Sal.Application.Dto;
using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sal.Application.Services;
using AidlyErp.Sal.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AidlyErp.Tests;

/// <summary>
/// The POS guarantees that make a till safe to use:
///
///  • a sale cannot be rung up without an open drawer (decision D3 — block, never auto-open);
///  • replaying the same <c>client_uuid</c> returns the original sale rather than selling twice;
///  • the drawer close reconciles counted cash against the tenders actually recorded.
///
/// Uses the in-memory provider, matching the other suites here. Note that in-memory does not
/// enforce the real unique index on <c>client_uuid</c> — the read-before-insert path is what is
/// covered below; the index itself is verified by SchemaDriftTests against the live database.
/// </summary>
public class SalPosIdempotencyTests
{
    private const long Company = 7, Branch = 3, User = 42, Terminal = 11, Warehouse = 5;

    // ── the D3 guarantee ─────────────────────────────────────────────────────

    [Fact]
    public async Task RequireOpenSession_Throws_WhenNoSessionIsOpen()
    {
        var db = CreateDb();
        db.SalPosTerminals.Add(NewTerminal());
        await db.SaveChangesAsync();

        var pos = CreatePosService(db);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => pos.RequireOpenSessionAsync(Terminal));

        Assert.Contains("open a session", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenSession_Throws_WhenTerminalAlreadyHasOne()
    {
        var db = CreateDb();
        db.SalPosTerminals.Add(NewTerminal());
        await db.SaveChangesAsync();

        var pos = CreatePosService(db);
        await pos.OpenAsync(Terminal, openingFloat: 5000m);

        await Assert.ThrowsAsync<ValidationException>(() => pos.OpenAsync(Terminal, openingFloat: 5000m));
    }

    [Fact]
    public async Task OpenSession_RecordsCashierAndFloat()
    {
        var db = CreateDb();
        db.SalPosTerminals.Add(NewTerminal());
        await db.SaveChangesAsync();

        var session = await CreatePosService(db).OpenAsync(Terminal, openingFloat: 5000m);

        Assert.Equal(User, session.CashierUserNo);
        Assert.Equal(5000m, session.OpeningFloat);
        Assert.Equal(1, session.Status);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
    }

    // ── the idempotency guarantee ────────────────────────────────────────────

    [Fact]
    public async Task ConfirmSale_Replay_ReturnsTheOriginalSale_AndDoesNotSellTwice()
    {
        var db = CreateDb();
        db.SalPosTerminals.Add(NewTerminal());
        await db.SaveChangesAsync();

        var pos = CreatePosService(db);
        var session = await pos.OpenAsync(Terminal, openingFloat: 0m);

        var uuid = Guid.NewGuid();
        var original = new SalInvoice
        {
            CompanyNo = Company,
            BranchNo = Branch,
            ClientUuid = uuid,
            InvoiceId = "POS-000001",
            InvoiceDate = DateTime.UtcNow.Date,
            InvoiceTime = DateTime.UtcNow,
            SaleType = 1,
            CustomerNo = 1,
            WarehouseNo = Warehouse,
            TerminalNo = Terminal,
            PosSessionNo = session.SessionNo,
            Status = 2,
            GrandTotal = 250m,
            PaidAmount = 250m,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = User,
            CreatedAt = DateTime.UtcNow
        };
        db.SalInvoices.Add(original);
        await db.SaveChangesAsync();

        var sales = CreateSalesService(db, pos);

        var replay = await sales.ConfirmPosSaleAsync(new SalPosSaleRequestDto
        {
            ClientUuid = uuid,
            TerminalNo = Terminal,
            CustomerNo = 1,
            Lines = { new SalPriceLineRequestDto { ProductNo = 99, Qty = 1 } },
            Tenders = { new SalTenderDto { PaymentMethod = 1, Amount = 250m } }
        });

        // The replay resolves to the original document, and no second sale exists.
        Assert.Equal(original.InvoiceNo, replay.InvoiceNo);
        Assert.Equal(1, await db.SalInvoices.CountAsync(i => i.ClientUuid == uuid));
    }

    [Fact]
    public async Task ConfirmSale_Throws_WhenClientUuidMissing()
    {
        var db = CreateDb();
        var sales = CreateSalesService(db, CreatePosService(db));

        await Assert.ThrowsAsync<ValidationException>(() => sales.ConfirmPosSaleAsync(new SalPosSaleRequestDto
        {
            ClientUuid = Guid.Empty,
            TerminalNo = Terminal,
            CustomerNo = 1,
            Tenders = { new SalTenderDto { PaymentMethod = 1, Amount = 10m } }
        }));
    }

    // ── the drawer guarantee ─────────────────────────────────────────────────

    [Fact]
    public async Task CloseSession_ComputesVariance_AgainstRecordedTenders()
    {
        var db = CreateDb();
        db.SalPosTerminals.Add(NewTerminal());
        await db.SaveChangesAsync();

        var pos = CreatePosService(db);
        var session = await pos.OpenAsync(Terminal, openingFloat: 1000m);

        // One confirmed sale: 800 cash + 200 card.
        var invoice = new SalInvoice
        {
            CompanyNo = Company,
            BranchNo = Branch,
            ClientUuid = Guid.NewGuid(),
            InvoiceId = "POS-000001",
            InvoiceDate = DateTime.UtcNow.Date,
            InvoiceTime = DateTime.UtcNow,
            SaleType = 1,
            CustomerNo = 1,
            WarehouseNo = Warehouse,
            PosSessionNo = session.SessionNo,
            Status = 2,
            GrandTotal = 1000m,
            PaidAmount = 1000m,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = User,
            CreatedAt = DateTime.UtcNow
        };
        db.SalInvoices.Add(invoice);
        await db.SaveChangesAsync();

        db.SalInvoicePayments.AddRange(
            new SalInvoicePayment { InvoiceNo = invoice.InvoiceNo, LineNo = 1, PaymentMethod = 1, Amount = 800m },
            new SalInvoicePayment { InvoiceNo = invoice.InvoiceNo, LineNo = 2, PaymentMethod = 2, Amount = 200m });
        await db.SaveChangesAsync();

        // Drawer counted 50 short of the 1800 it should hold (1000 float + 800 cash taken).
        var closed = await pos.CloseAsync(new SalCloseSessionRequestDto
        {
            SessionNo = session.SessionNo,
            CountedCash = 1750m,
            VarianceApproved = true,
            Remarks = "till short, supervisor notified"
        });

        Assert.Equal(800m, closed.ExpectedCash);
        Assert.Equal(-50m, closed.CashVariance);
        Assert.Equal(1000m, closed.TotalSales);
        Assert.Equal(1, closed.InvoiceCount);
        Assert.Equal(3, closed.Status);

        // Card takings are not cash and must not muddy the drawer count.
        var summaryEvent = await db.EventOutboxes.SingleAsync(e => e.EventType == "PosSessionClosePosted");
        Assert.Contains("CASH_OVER_SHORT", summaryEvent.Payload);
    }

    [Fact]
    public async Task CloseSession_Refuses_WhenDrawerIsOutAndNobodySignedOff()
    {
        var (db, pos, session) = await SessionWithCashSale(cashTaken: 800m, openingFloat: 1000m);

        // 1750 counted against 1800 expected — a real discrepancy, not rounding.
        var ex = await Assert.ThrowsAsync<ValidationException>(() => pos.CloseAsync(new SalCloseSessionRequestDto
        {
            SessionNo = session.SessionNo,
            CountedCash = 1750m,
            VarianceApproved = false,
            Remarks = "short"
        }));

        Assert.Contains("approve the variance", ex.Message, StringComparison.OrdinalIgnoreCase);

        // And the session is still open, so the discrepancy cannot be walked away from.
        var stillOpen = await db.SalPosSessions.FindAsync(session.SessionNo);
        Assert.Equal(1, stillOpen!.Status);
    }

    [Fact]
    public async Task CloseSession_Refuses_WhenVarianceHasNoExplanation()
    {
        var (_, pos, session) = await SessionWithCashSale(cashTaken: 800m, openingFloat: 1000m);

        await Assert.ThrowsAsync<ValidationException>(() => pos.CloseAsync(new SalCloseSessionRequestDto
        {
            SessionNo = session.SessionNo,
            CountedCash = 1750m,
            VarianceApproved = true,
            Remarks = "   "
        }));
    }

    [Fact]
    public async Task CloseSession_NeedsNoSignOff_WhenDrawerReconciles()
    {
        var (_, pos, session) = await SessionWithCashSale(cashTaken: 800m, openingFloat: 1000m);

        var closed = await pos.CloseAsync(new SalCloseSessionRequestDto
        {
            SessionNo = session.SessionNo,
            CountedCash = 1800m
        });

        Assert.Equal(0m, closed.CashVariance);
        Assert.Equal(3, closed.Status);
    }

    [Fact]
    public async Task SessionSummary_ReportsExpectedDrawer_AsFloatPlusCash()
    {
        var db = CreateDb();
        db.SalPosTerminals.Add(NewTerminal());
        await db.SaveChangesAsync();

        var pos = CreatePosService(db);
        var session = await pos.OpenAsync(Terminal, openingFloat: 1000m);

        var invoice = new SalInvoice
        {
            CompanyNo = Company,
            BranchNo = Branch,
            ClientUuid = Guid.NewGuid(),
            InvoiceId = "POS-000002",
            InvoiceDate = DateTime.UtcNow.Date,
            InvoiceTime = DateTime.UtcNow,
            SaleType = 1,
            CustomerNo = 1,
            WarehouseNo = Warehouse,
            PosSessionNo = session.SessionNo,
            Status = 2,
            GrandTotal = 300m,
            PaidAmount = 300m,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = User,
            CreatedAt = DateTime.UtcNow
        };
        db.SalInvoices.Add(invoice);
        await db.SaveChangesAsync();

        db.SalInvoicePayments.Add(new SalInvoicePayment
        {
            InvoiceNo = invoice.InvoiceNo, LineNo = 1, PaymentMethod = 1, Amount = 300m
        });
        await db.SaveChangesAsync();

        var summary = await pos.SummaryAsync(session.SessionNo);

        Assert.Equal(300m, summary.ExpectedCash);
        Assert.Equal(1300m, summary.ExpectedDrawer);
        Assert.Equal(1, summary.InvoiceCount);
        Assert.Single(summary.Tenders);
        Assert.Equal("Cash", summary.Tenders[0].MethodName);
    }

    // ── scaffolding ──────────────────────────────────────────────────────────

    /// <summary>An open session with one confirmed cash sale already rung up against it.</summary>
    private static async Task<(SalInMemoryDbContext Db, ISalPosService Pos, SalPosSessionDto Session)>
        SessionWithCashSale(decimal cashTaken, decimal openingFloat)
    {
        var db = CreateDb();
        db.SalPosTerminals.Add(NewTerminal());
        await db.SaveChangesAsync();

        var pos = CreatePosService(db);
        var session = await pos.OpenAsync(Terminal, openingFloat);

        var invoice = new SalInvoice
        {
            CompanyNo = Company,
            BranchNo = Branch,
            ClientUuid = Guid.NewGuid(),
            InvoiceId = "POS-000001",
            InvoiceDate = DateTime.UtcNow.Date,
            InvoiceTime = DateTime.UtcNow,
            SaleType = 1,
            CustomerNo = 1,
            WarehouseNo = Warehouse,
            PosSessionNo = session.SessionNo,
            Status = 2,
            GrandTotal = cashTaken,
            PaidAmount = cashTaken,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = User,
            CreatedAt = DateTime.UtcNow
        };
        db.SalInvoices.Add(invoice);
        await db.SaveChangesAsync();

        db.SalInvoicePayments.Add(new SalInvoicePayment
        {
            InvoiceNo = invoice.InvoiceNo, LineNo = 1, PaymentMethod = 1, Amount = cashTaken
        });
        await db.SaveChangesAsync();

        return (db, pos, session);
    }

    private static SalPosTerminal NewTerminal() => new()
    {
        TerminalNo = Terminal,
        CompanyNo = Company,
        BranchNo = Branch,
        WarehouseNo = Warehouse,
        TerminalId = "T1",
        TerminalName = "Front till",
        ReceiptPrefix = "POS-",
        IsActive = 1,
        IsDeleted = 0,
        CreatedBy = User,
        CreatedAt = DateTime.UtcNow
    };

    private static ISalPosService CreatePosService(SalInMemoryDbContext db)
    {
        var docSeq = new Mock<IDocSequenceGenerator>();
        docSeq.Setup(d => d.NextAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<string>(),
                                      It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("SES000001");

        return new SalPosService(db, CreateContext(), docSeq.Object);
    }

    /// <summary>
    /// Only the collaborators the replay path actually reaches are real; a replay short-circuits
    /// before pricing or posting, which is the point of the test.
    /// </summary>
    private static ISal1001Service CreateSalesService(SalInMemoryDbContext db, ISalPosService pos) =>
        new Sal1001Service(
            db,
            CreateContext(),
            Mock.Of<ISalArLedgerService>(),
            Mock.Of<IApprovalService>(),
            Mock.Of<IVatTaxLookup>(),
            Mock.Of<IInvLookup>(),
            Mock.Of<AidlyErp.Inv.Contracts.IInvCatalog>(),
            Mock.Of<AidlyErp.Inv.Contracts.IInvStockPostingService>(),
            Mock.Of<IFinCalendar>(),
            Mock.Of<ISalPricingService>(),
            pos,
            Mock.Of<ISalPromotionEngine>());

    private static ICompanyBranchContext CreateContext()
    {
        var ctx = new Mock<ICompanyBranchContext>();
        ctx.Setup(c => c.CompanyNo).Returns(Company);
        ctx.Setup(c => c.BranchNo).Returns(Branch);
        ctx.Setup(c => c.UserNo).Returns(User);
        return ctx.Object;
    }

    private static SalInMemoryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SalInMemoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SalInMemoryDbContext(options);
    }

    private class SalInMemoryDbContext : DbContext, ISalDbContext
    {
        public SalInMemoryDbContext(DbContextOptions<SalInMemoryDbContext> options) : base(options) { }

        public DbSet<SalCustomer> SalCustomers => Set<SalCustomer>();
        public DbSet<SalCustomerLedger> SalCustomerLedgers => Set<SalCustomerLedger>();
        public DbSet<SalInvoice> SalInvoices => Set<SalInvoice>();
        public DbSet<SalInvoiceDtl> SalInvoiceDtls => Set<SalInvoiceDtl>();
        public DbSet<SalReceipt> SalReceipts => Set<SalReceipt>();
        public DbSet<SalReceiptAlloc> SalReceiptAllocs => Set<SalReceiptAlloc>();
        public DbSet<SalReturn> SalReturns => Set<SalReturn>();
        public DbSet<SalReturnDtl> SalReturnDtls => Set<SalReturnDtl>();
        public DbSet<SalPosTerminal> SalPosTerminals => Set<SalPosTerminal>();
        public DbSet<SalPosSession> SalPosSessions => Set<SalPosSession>();
        public DbSet<SalInvoicePayment> SalInvoicePayments => Set<SalInvoicePayment>();
        public DbSet<SalCustomerGroup> SalCustomerGroups => Set<SalCustomerGroup>();
        public DbSet<SalPromotion> SalPromotions => Set<SalPromotion>();
        public DbSet<SalPromotionDtl> SalPromotionDtls => Set<SalPromotionDtl>();
        public DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes => Set<AidlyErp.Shared.Core.EventOutbox>();

        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade ISalDbContext.Database => Database;
    }
}
