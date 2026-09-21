using AidlyErp.Pur.Application.Services;
using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Pur.Application.Dto;
using AidlyErp.Pur.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Sys.Contracts;
using AidlyErp.Fin.Contracts;
using AidlyErp.Inv.Contracts;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AidlyErp.Tests;

/// <summary>
/// Regression tests for PUR module: cross-tenant isolation.
/// Verifies that PUR service methods reject access to another company's data.
/// </summary>
public class PurTenantIsolationTests
{
    private static IPurDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PurInMemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PurInMemoryDbContext(options);
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
    public async Task GetDetailAsync_Throws_ForOtherCompanyInvoice()
    {
        var db = CreateInMemoryDb();
        var ctxA = CreateContext(companyNo: 100);

        db.PurInvoices.Add(new PurInvoice
        {
            InvoiceNo = 1, CompanyNo = 200, BranchNo = 1,
            InvoiceId = "PINV-000001", SupplierNo = 1, WarehouseNo = 1,
            InvoiceDate = DateTime.UtcNow, Status = 1, IsActive = 1, IsDeleted = 0
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new Pur1102Service(db, ctxA,
            Mock.Of<IPurApLedgerService>(), Mock.Of<IInvStockPostingService>(),
            Mock.Of<IVatTaxLookup>(), Mock.Of<IUnitOfWork<IPurDbContext>>(),
            Mock.Of<IDocSequenceGenerator>(), Mock.Of<IFinCalendar>(),
            Mock.Of<IApprovalService>(), Mock.Of<IInvCatalog>());

        await Assert.ThrowsAnyAsync<Exception>(() => service.GetDetailAsync(1));
    }

    [Fact]
    public async Task GetDetailAsync_Throws_ForOtherCompanyPayment()
    {
        var db = CreateInMemoryDb();
        var ctxA = CreateContext(companyNo: 100);

        db.PurPayments.Add(new PurPayment
        {
            PaymentNo = 1, CompanyNo = 200, BranchNo = 1,
            PaymentId = "PAY-000001", SupplierNo = 1,
            PaymentDate = DateTime.UtcNow, Amount = 1000,
            Status = 1, IsActive = 1, IsDeleted = 0
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new Pur1104Service(db, ctxA,
            Mock.Of<IPurApLedgerService>(), Mock.Of<IUnitOfWork<IPurDbContext>>(),
            Mock.Of<IDocSequenceGenerator>(), Mock.Of<IFinCalendar>());

        await Assert.ThrowsAnyAsync<Exception>(() => service.GetDetailAsync(1));
    }

    [Fact]
    public async Task GetDetailAsync_Throws_ForOtherCompanySupplier()
    {
        var db = CreateInMemoryDb();
        var ctxA = CreateContext(companyNo: 100);

        db.PurSuppliers.Add(new PurSupplier
        {
            SupplierNo = 1, CompanyNo = 200,
            SupplierId = "SUP-000001", SupplierName = "Test Supplier",
            IsActive = 1, IsDeleted = 0
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new Pur1001Service(db, ctxA);

        await Assert.ThrowsAnyAsync<Exception>(() => service.GetDetailAsync(1));
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsData_ForOwnCompanyInvoice()
    {
        var db = CreateInMemoryDb();
        var ctxA = CreateContext(companyNo: 100);

        db.PurInvoices.Add(new PurInvoice
        {
            InvoiceNo = 2, CompanyNo = 100, BranchNo = 1,
            InvoiceId = "PINV-000002", SupplierNo = 1, WarehouseNo = 1,
            InvoiceDate = DateTime.UtcNow, Status = 1, IsActive = 1, IsDeleted = 0
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new Pur1102Service(db, ctxA,
            Mock.Of<IPurApLedgerService>(), Mock.Of<IInvStockPostingService>(),
            Mock.Of<IVatTaxLookup>(), Mock.Of<IUnitOfWork<IPurDbContext>>(),
            Mock.Of<IDocSequenceGenerator>(), Mock.Of<IFinCalendar>(),
            Mock.Of<IApprovalService>(), Mock.Of<IInvCatalog>());

        var result = await service.GetDetailAsync(2);

        Assert.NotNull(result);
        Assert.Equal(2, result.InvoiceNo);
    }

    // In-memory DbContext for testing
    private class PurInMemoryDbContext : DbContext, IPurDbContext
    {
        public PurInMemoryDbContext(DbContextOptions<PurInMemoryDbContext> options) : base(options) { }

        public DbSet<PurSupplier> PurSuppliers => Set<PurSupplier>();
        public DbSet<PurSupplierProduct> PurSupplierProducts => Set<PurSupplierProduct>();
        public DbSet<PurSupplierLedger> PurSupplierLedgers => Set<PurSupplierLedger>();
        public DbSet<PurOrder> PurOrders => Set<PurOrder>();
        public DbSet<PurOrderDtl> PurOrderDtls => Set<PurOrderDtl>();
        public DbSet<PurReceipt> PurReceipts => Set<PurReceipt>();
        public DbSet<PurReceiptDtl> PurReceiptDtls => Set<PurReceiptDtl>();
        public DbSet<PurInvoice> PurInvoices => Set<PurInvoice>();
        public DbSet<PurInvoiceDtl> PurInvoiceDtls => Set<PurInvoiceDtl>();
        public DbSet<PurReturn> PurReturns => Set<PurReturn>();
        public DbSet<PurReturnDtl> PurReturnDtls => Set<PurReturnDtl>();
        public DbSet<PurPayment> PurPayments => Set<PurPayment>();
        public DbSet<PurPaymentAlloc> PurPaymentAllocs => Set<PurPaymentAlloc>();
        public DbSet<PurLandedCost> PurLandedCosts => Set<PurLandedCost>();
        public DbSet<PurLandedCostAlloc> PurLandedCostAllocs => Set<PurLandedCostAlloc>();
        public DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes => Set<AidlyErp.Shared.Core.EventOutbox>();

        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade IPurDbContext.Database => Database;
    }
}
