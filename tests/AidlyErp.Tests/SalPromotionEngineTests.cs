using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sal.Application.Services;
using AidlyErp.Sal.Domain;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AidlyErp.Tests;

/// <summary>
/// The promotion engine decides what a customer actually pays, so the cases that matter are the
/// ones where a rule could quietly give away more than intended — or fire when it should not.
/// </summary>
public class SalPromotionEngineTests
{
    private const long Company = 7, Branch = 3, User = 42;

    // ── firing ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task LinePercent_DiscountsMatchingLine()
    {
        var db = CreateDb();
        AddPromotion(db, promoType: 1, scope: 4, discountPct: 10m);
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, productNo: 5, qty: 2, net: 200m) }, null, 200m);

        Assert.Equal(20m, outcome.LineHits[0].Discount);
    }

    [Fact]
    public async Task LinePercent_IsCappedByMaxDiscount()
    {
        var db = CreateDb();
        // 50% of 1000 is 500, but the rule refuses to give away more than 100.
        AddPromotion(db, promoType: 1, scope: 4, discountPct: 50m, maxDiscount: 100m);
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 1, 1000m) }, null, 1000m);

        Assert.Equal(100m, outcome.LineHits[0].Discount);
    }

    [Fact]
    public async Task LineDiscount_NeverExceedsTheLineValue()
    {
        var db = CreateDb();
        AddPromotion(db, promoType: 2, scope: 4, discountAmount: 500m);
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 1, 120m) }, null, 120m);

        // Giving 500 off a 120 line would hand back money that was never taken.
        Assert.Equal(120m, outcome.LineHits[0].Discount);
    }

    [Fact]
    public async Task BillPercent_DiscountsTheCart()
    {
        var db = CreateDb();
        AddPromotion(db, promoType: 3, scope: 4, discountPct: 5m);
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 1, 1000m) }, null, 1000m);

        Assert.Equal(50m, outcome.BillDiscount);
        Assert.NotNull(outcome.BillPromotionNo);
    }

    [Fact]
    public async Task BuyXGetY_GivesWholeMultiplesOnly()
    {
        var db = CreateDb();
        AddPromotion(db, promoType: 5, scope: 4, buyQty: 2m, getQty: 1m);
        await db.SaveChangesAsync();

        // Five qualifying units earn two frees, not two and a half.
        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 5, 500m) }, null, 500m);

        Assert.Single(outcome.FreeLines);
        Assert.Equal(2m, outcome.FreeLines[0].Qty);
    }

    [Fact]
    public async Task BuyXGetY_DoesNotFireBelowTheThreshold()
    {
        var db = CreateDb();
        AddPromotion(db, promoType: 5, scope: 4, buyQty: 3m, getQty: 1m);
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 2, 200m) }, null, 200m);

        Assert.Empty(outcome.FreeLines);
    }

    // ── not firing ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UnsupportedTypes_AreSkippedRatherThanApproximated()
    {
        var db = CreateDb();
        // 6=QtyBreak, 7=Coupon, 8=Bundle — schema accepts them, engine must not guess.
        AddPromotion(db, promoType: 6, scope: 4, discountPct: 50m);
        AddPromotion(db, promoType: 7, scope: 4, discountPct: 50m);
        AddPromotion(db, promoType: 8, scope: 4, discountPct: 50m);
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 1, 100m) }, null, 100m);

        Assert.Empty(outcome.LineHits);
        Assert.Equal(0m, outcome.BillDiscount);
    }

    [Fact]
    public async Task ExpiredPromotion_DoesNotFire()
    {
        var db = CreateDb();
        var promo = AddPromotion(db, promoType: 1, scope: 4, discountPct: 10m);
        promo.StartDate = DateTime.Today.AddDays(-30);
        promo.EndDate = DateTime.Today.AddDays(-1);
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 1, 100m) }, null, 100m);

        Assert.Empty(outcome.LineHits);
    }

    [Fact]
    public async Task ExhaustedUsageLimit_DoesNotFire()
    {
        var db = CreateDb();
        var promo = AddPromotion(db, promoType: 1, scope: 4, discountPct: 10m);
        promo.UsageLimit = 5;
        promo.UsedCount = 5;
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 1, 100m) }, null, 100m);

        Assert.Empty(outcome.LineHits);
    }

    [Fact]
    public async Task MinimumQuantity_GatesTheLine()
    {
        var db = CreateDb();
        var promo = AddPromotion(db, promoType: 1, scope: 4, discountPct: 10m);
        promo.MinQty = 3m;
        await db.SaveChangesAsync();

        var below = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 2, 200m) }, null, 200m);
        Assert.Empty(below.LineHits);

        var atThreshold = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 3, 300m) }, null, 300m);
        Assert.Single(atThreshold.LineHits);
    }

    [Fact]
    public async Task ProductScope_OnlyTouchesItsOwnProduct()
    {
        var db = CreateDb();
        var promo = AddPromotion(db, promoType: 1, scope: 1, discountPct: 10m);
        await db.SaveChangesAsync();

        db.SalPromotionDtls.Add(new SalPromotionDtl
        {
            PromotionNo = promo.PromotionNo, TargetRole = 1, ProductNo = 5
        });
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(
            new[] { Line(0, productNo: 5, qty: 1, net: 100m), Line(1, productNo: 9, qty: 1, net: 100m) },
            null, 200m);

        Assert.True(outcome.LineHits.ContainsKey(0));
        Assert.False(outcome.LineHits.ContainsKey(1));
    }

    [Fact]
    public async Task CategoryScope_TouchesOnlyItsCategory()
    {
        var db = CreateDb();
        var promo = AddPromotion(db, promoType: 1, scope: 2, discountPct: 10m);
        await db.SaveChangesAsync();

        db.SalPromotionDtls.Add(new SalPromotionDtl
        {
            PromotionNo = promo.PromotionNo, TargetRole = 1, CategoryNo = 77
        });
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[]
        {
            Line(0, productNo: 5, qty: 1, net: 100m, categoryNo: 77),
            Line(1, productNo: 9, qty: 1, net: 100m, categoryNo: 88),
        }, null, 200m);

        Assert.True(outcome.LineHits.ContainsKey(0));
        Assert.False(outcome.LineHits.ContainsKey(1));
    }

    [Fact]
    public async Task BrandScope_TouchesOnlyItsBrand()
    {
        var db = CreateDb();
        var promo = AddPromotion(db, promoType: 1, scope: 3, discountPct: 10m);
        await db.SaveChangesAsync();

        db.SalPromotionDtls.Add(new SalPromotionDtl
        {
            PromotionNo = promo.PromotionNo, TargetRole = 1, BrandNo = 4
        });
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[]
        {
            Line(0, productNo: 5, qty: 1, net: 100m, brandNo: 4),
            Line(1, productNo: 9, qty: 1, net: 100m, brandNo: 6),
        }, null, 200m);

        Assert.True(outcome.LineHits.ContainsKey(0));
        Assert.False(outcome.LineHits.ContainsKey(1));
    }

    // ── stacking ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task NonStackablePromotion_ClosesTheLineToOthers()
    {
        var db = CreateDb();
        // Higher priority runs first and, being non-stackable, blocks the second.
        var first = AddPromotion(db, promoType: 1, scope: 4, discountPct: 10m);
        first.Priority = 10;
        first.IsStackable = 0;

        var second = AddPromotion(db, promoType: 1, scope: 4, discountPct: 25m);
        second.Priority = 1;
        second.IsStackable = 1;
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 1, 100m) }, null, 100m);

        Assert.Equal(10m, outcome.LineHits[0].Discount);
    }

    [Fact]
    public async Task StackablePromotions_Accumulate()
    {
        var db = CreateDb();
        var first = AddPromotion(db, promoType: 1, scope: 4, discountPct: 10m);
        first.Priority = 10;
        first.IsStackable = 1;

        var second = AddPromotion(db, promoType: 2, scope: 4, discountAmount: 5m);
        second.Priority = 1;
        second.IsStackable = 1;
        await db.SaveChangesAsync();

        var outcome = await Engine(db).EvaluateAsync(new[] { Line(0, 5, 1, 100m) }, null, 100m);

        Assert.Equal(15m, outcome.LineHits[0].Discount);
    }

    // ── usage ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordUsage_IncrementsOnlyOnce_PerPromotion()
    {
        var db = CreateDb();
        var promo = AddPromotion(db, promoType: 1, scope: 4, discountPct: 10m);
        await db.SaveChangesAsync();

        // The same promotion appearing on several lines is still one use of the sale.
        await Engine(db).RecordUsageAsync(new[] { promo.PromotionNo, promo.PromotionNo });

        var reloaded = await db.SalPromotions.FindAsync(promo.PromotionNo);
        Assert.Equal(1, reloaded!.UsedCount);
    }

    // ── scaffolding ──────────────────────────────────────────────────────────

    private static PromotionCandidateLine Line(int index, long productNo, decimal qty, decimal net,
                                               long? categoryNo = null, long? brandNo = null) =>
        new(index, productNo, VariantNo: null, UomNo: 1, categoryNo, brandNo, qty, net);

    private static SalPromotion AddPromotion(SalInMemoryDbContext db, short promoType, short scope,
                                             decimal? discountPct = null, decimal? discountAmount = null,
                                             decimal? maxDiscount = null,
                                             decimal? buyQty = null, decimal? getQty = null)
    {
        var promo = new SalPromotion
        {
            CompanyNo = Company,
            BranchNo = null,
            PromotionId = $"P{Guid.NewGuid():N}".Substring(0, 12),
            PromotionName = "Test promotion",
            PromoType = promoType,
            ScopeType = scope,
            DiscountPct = discountPct,
            DiscountAmount = discountAmount,
            MaxDiscountAmount = maxDiscount,
            BuyQty = buyQty,
            GetQty = getQty,
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(30),
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = User,
            CreatedAt = DateTime.UtcNow
        };
        db.SalPromotions.Add(promo);
        return promo;
    }

    private static ISalPromotionEngine Engine(SalInMemoryDbContext db)
    {
        var ctx = new Mock<ICompanyBranchContext>();
        ctx.Setup(c => c.CompanyNo).Returns(Company);
        ctx.Setup(c => c.BranchNo).Returns(Branch);
        ctx.Setup(c => c.UserNo).Returns(User);
        return new SalPromotionEngine(db, ctx.Object);
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
