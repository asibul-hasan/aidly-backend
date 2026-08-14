using AidlyErp.Fin.Application.Services;
using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Fin.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AidlyErp.Tests;

/// <summary>
/// Regression tests for Sprint 3 Group 1: cross-tenant isolation.
/// Verifies that FIN service methods reject access to another company's data.
/// </summary>
public class FinTenantIsolationTests
{
    private static IFinDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<FinInMemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FinInMemoryDbContext(options);
    }

    private static ICompanyBranchContext CreateContext(long companyNo, long branchNo = 1, long userNo = 1)
    {
        var ctx = new Mock<ICompanyBranchContext>();
        ctx.Setup(c => c.CompanyNo).Returns(companyNo);
        ctx.Setup(c => c.BranchNo).Returns(branchNo);
        ctx.Setup(c => c.UserNo).Returns(userNo);
        return ctx.Object;
    }

    [Fact]
    public async Task GetDetailAsync_ThrowsNotFound_ForOtherCompanyVoucher()
    {
        // Arrange: seed a voucher for Company B
        var db = CreateInMemoryDb();
        var ctxA = CreateContext(companyNo: 100); // Company A
        var ctxB = CreateContext(companyNo: 200); // Company B

        db.FinVouchers.Add(new FinVoucher
        {
            VoucherNo = 1,
            CompanyNo = 200, // Company B
            BranchNo = 1,
            VoucherId = "JV-000001",
            VoucherTypeNo = 1,
            VoucherDate = DateTime.UtcNow,
            FinYearNo = 1,
            FinPeriodNo = 1,
            Status = 1,
            IsActive = 1,
            IsDeleted = 0
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var calendar = new Mock<IFinCalendar>();
        var approvalService = new Mock<IApprovalService>();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();
        var docSeq = new Mock<IDocSequenceGenerator>();
        var currencyLookup = new Mock<ICurrencyLookup>();

        var service = new Fin1101Service(db, ctxA, approvalService.Object, calendar.Object, uow.Object, docSeq.Object, currencyLookup.Object);

        // Act & Assert: Company A cannot see Company B's voucher
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailAsync(1));
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsData_ForOwnCompanyVoucher()
    {
        // Arrange: seed a voucher for Company A
        var db = CreateInMemoryDb();
        var ctxA = CreateContext(companyNo: 100);

        db.FinVouchers.Add(new FinVoucher
        {
            VoucherNo = 2,
            CompanyNo = 100, // Company A
            BranchNo = 1,
            VoucherId = "JV-000002",
            VoucherTypeNo = 1,
            VoucherDate = DateTime.UtcNow,
            FinYearNo = 1,
            FinPeriodNo = 1,
            Status = 1,
            IsActive = 1,
            IsDeleted = 0
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var calendar = new Mock<IFinCalendar>();
        var approvalService = new Mock<IApprovalService>();
        var uow = new Mock<IUnitOfWork<IFinDbContext>>();
        var docSeq = new Mock<IDocSequenceGenerator>();
        var currencyLookup = new Mock<ICurrencyLookup>();

        var service = new Fin1101Service(db, ctxA, approvalService.Object, calendar.Object, uow.Object, docSeq.Object, currencyLookup.Object);

        // Act: Company A can see its own voucher
        var result = await service.GetDetailAsync(2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.VoucherNo);
    }

    // In-memory DbContext for testing
    private class FinInMemoryDbContext : DbContext, IFinDbContext
    {
        public FinInMemoryDbContext(DbContextOptions<FinInMemoryDbContext> options) : base(options) { }

        public DbSet<FinAccount> FinAccounts => Set<FinAccount>();
        public DbSet<FinAccountGroup> FinAccountGroups => Set<FinAccountGroup>();
        public DbSet<FinAccountBalance> FinAccountBalances => Set<FinAccountBalance>();
        public DbSet<FinGlMap> FinGlMaps => Set<FinGlMap>();
        public DbSet<FinVoucher> FinVouchers => Set<FinVoucher>();
        public DbSet<FinVoucherDtl> FinVoucherDtls => Set<FinVoucherDtl>();
        public DbSet<FinVoucherType> FinVoucherTypes => Set<FinVoucherType>();
        public DbSet<FinLedger> FinLedgers => Set<FinLedger>();
        public DbSet<FinBankAccount> FinBankAccounts => Set<FinBankAccount>();
        public DbSet<FinBankRecon> FinBankRecons => Set<FinBankRecon>();
        public DbSet<FinBankReconLine> FinBankReconLines => Set<FinBankReconLine>();
        public DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes => Set<AidlyErp.Shared.Core.EventOutbox>();

        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade IFinDbContext.Database => Database;
    }
}
