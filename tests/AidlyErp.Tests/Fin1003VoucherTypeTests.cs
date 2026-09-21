using AidlyErp.Fin.Application.Services;
using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Core;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AidlyErp.Tests;

public class Fin1003VoucherTypeTests
{
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

    [Fact]
    public async Task GetLookupsAsync_ReturnsOnlyPostableActiveAccounts()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 1, CompanyNo = 1, AccountCode = "1010", AccountName = "Cash", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 2, CompanyNo = 1, AccountCode = "1020", AccountName = "Bank", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 3, CompanyNo = 1, AccountCode = "1000", AccountName = "Current Assets (Group)", IsPostable = 0, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 4, CompanyNo = 1, AccountCode = "1030", AccountName = "Inactive Bank", IsPostable = 1, IsActive = 0, IsDeleted = 0 },
            new FinAccount { AccountNo = 5, CompanyNo = 2, AccountCode = "1010", AccountName = "Other Company Cash", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );
        await db.SaveChangesAsync();

        var lookups = await service.GetLookupsAsync();

        Assert.NotNull(lookups);
        Assert.Equal(2, lookups.Accounts.Count);
        Assert.Contains(lookups.Accounts, a => a.AccountCode == "1010");
        Assert.Contains(lookups.Accounts, a => a.AccountCode == "1020");
    }

    [Fact]
    public async Task SaveAsync_NewVoucherType_PersistsFieldsAndDefaultAccounts()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 10, CompanyNo = 1, AccountCode = "5010", AccountName = "Office Supplies", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 20, CompanyNo = 1, AccountCode = "1010", AccountName = "Petty Cash", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );
        await db.SaveChangesAsync();

        var newDto = new Fin1003VoucherTypeDto
        {
            VoucherTypeId = "EXP",
            TypeName = "Expense Voucher",
            BaseKind = 2, // Payment
            NumberPrefix = "EXP",
            DefaultDrAccountNo = 10,
            DefaultCrAccountNo = 20,
            OrderSl = 5,
            IsActive = 1
        };

        var saved = await service.SaveAsync(newDto);

        Assert.NotNull(saved);
        Assert.True(saved.VoucherTypeNo > 0);
        Assert.Equal("EXP", saved.VoucherTypeCode);
        Assert.Equal("Expense Voucher", saved.VoucherTypeName);
        Assert.Equal((short)2, saved.BaseKind);
        Assert.Equal("EXP", saved.Prefix);
        Assert.Equal(10, saved.DefaultDrAccountNo);
        Assert.Equal(20, saved.DefaultCrAccountNo);
        Assert.Equal("5010 - Office Supplies", saved.DefaultDrAccountName);
        Assert.Equal("1010 - Petty Cash", saved.DefaultCrAccountName);
    }

    [Fact]
    public async Task SaveAsync_DuplicateCode_ThrowsValidationException()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        db.FinVoucherTypes.Add(new FinVoucherType
        {
            VoucherTypeNo = 1,
            CompanyNo = 1,
            VoucherTypeCode = "JV",
            VoucherTypeName = "Journal Voucher",
            BaseKind = 1,
            IsActive = 1,
            IsDeleted = 0
        });
        await db.SaveChangesAsync();

        var duplicateDto = new Fin1003VoucherTypeDto
        {
            VoucherTypeId = "JV",
            TypeName = "Another Journal",
            BaseKind = 1
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(duplicateDto));
    }

    [Fact]
    public async Task SaveAsync_InvalidBaseKind_ThrowsValidationException()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        var invalidDto = new Fin1003VoucherTypeDto
        {
            VoucherTypeId = "TEST",
            TypeName = "Test Voucher",
            BaseKind = 9 // Invalid: must be 1..8
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.SaveAsync(invalidDto));
    }

    [Fact]
    public async Task SaveAsync_Update_UpdatesDefaultAccountsAndDetails()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 10, CompanyNo = 1, AccountCode = "1010", AccountName = "Cash", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 20, CompanyNo = 1, AccountCode = "1020", AccountName = "Main Bank", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );

        var existing = new FinVoucherType
        {
            VoucherTypeNo = 100,
            CompanyNo = 1,
            VoucherTypeCode = "PV",
            VoucherTypeName = "Payment Voucher",
            BaseKind = 2,
            Prefix = "PV",
            DefaultCrAccountNo = 10,
            IsSystem = 0,
            OrderSl = 2,
            IsActive = 1,
            IsDeleted = 0
        };
        db.FinVoucherTypes.Add(existing);
        await db.SaveChangesAsync();

        var updateDto = new Fin1003VoucherTypeDto
        {
            VoucherTypeNo = 100,
            VoucherTypeId = "PV",
            TypeName = "Payment Voucher (Bank)",
            BaseKind = 2,
            Prefix = "PVB",
            DefaultCrAccountNo = 20, // Changed to Bank
            OrderSl = 3,
            IsActive = 1
        };

        var updated = await service.SaveAsync(updateDto);

        Assert.Equal("Payment Voucher (Bank)", updated.VoucherTypeName);
        Assert.Equal("PVB", updated.Prefix);
        Assert.Equal(20, updated.DefaultCrAccountNo);
        Assert.Equal("1020 - Main Bank", updated.DefaultCrAccountName);
        Assert.Equal(3, updated.OrderSl);
    }

    [Fact]
    public async Task SaveAsync_PartialUpdate_UpdatesOnlySuppliedFields()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        db.FinAccounts.AddRange(
            new FinAccount { AccountNo = 10, CompanyNo = 1, AccountCode = "1010", AccountName = "Cash", IsPostable = 1, IsActive = 1, IsDeleted = 0 },
            new FinAccount { AccountNo = 20, CompanyNo = 1, AccountCode = "1020", AccountName = "Main Bank", IsPostable = 1, IsActive = 1, IsDeleted = 0 }
        );

        var existing = new FinVoucherType
        {
            VoucherTypeNo = 200,
            CompanyNo = 1,
            VoucherTypeCode = "JV",
            VoucherTypeName = "Journal Voucher",
            BaseKind = 1,
            Prefix = "JV",
            DefaultDrAccountNo = 10,
            DefaultCrAccountNo = null,
            IsSystem = 1,
            OrderSl = 1,
            RequiresApproval = 0,
            IsAutoNumbered = 1,
            IsActive = 1,
            IsDeleted = 0
        };
        db.FinVoucherTypes.Add(existing);
        await db.SaveChangesAsync();

        // Send ONLY DefaultDrAccountNo (0 to clear) and DefaultCrAccountNo (20 to update)
        var partialDiffDto = new Fin1003VoucherTypeDto
        {
            VoucherTypeNo = 200,
            DefaultDrAccountNo = 0, // explicitly cleared
            DefaultCrAccountNo = 20 // updated to Bank
        };

        var updated = await service.SaveAsync(partialDiffDto);

        // Verify that changed fields were updated
        Assert.Null(updated.DefaultDrAccountNo);
        Assert.Equal(20, updated.DefaultCrAccountNo);
        Assert.Equal("1020 - Main Bank", updated.DefaultCrAccountName);

        // Verify that omitted fields were NOT wiped or overwritten
        Assert.Equal("JV", updated.VoucherTypeCode);
        Assert.Equal("Journal Voucher", updated.VoucherTypeName);
        Assert.Equal((short)1, updated.BaseKind);
        Assert.Equal("JV", updated.Prefix);
        Assert.Equal((short)1, updated.IsSystem);
        Assert.Equal(1, updated.OrderSl);
        Assert.Equal((short)1, updated.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_SystemType_ThrowsValidationException()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        db.FinVoucherTypes.Add(new FinVoucherType
        {
            VoucherTypeNo = 1,
            CompanyNo = 1,
            VoucherTypeCode = "JV",
            VoucherTypeName = "Journal Voucher",
            BaseKind = 1,
            IsSystem = 1, // System type
            IsActive = 1,
            IsDeleted = 0
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.DeleteAsync(1));
        Assert.Contains("System voucher types cannot be deleted", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_TypeInUseByVouchers_ThrowsValidationException()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        db.FinVoucherTypes.Add(new FinVoucherType
        {
            VoucherTypeNo = 2,
            CompanyNo = 1,
            VoucherTypeCode = "CUSTOM",
            VoucherTypeName = "Custom Type",
            BaseKind = 1,
            IsSystem = 0,
            IsActive = 1,
            IsDeleted = 0
        });

        db.FinVouchers.Add(new FinVoucher
        {
            VoucherNo = 500,
            CompanyNo = 1,
            BranchNo = 1,
            VoucherTypeNo = 2,
            VoucherId = "CUSTOM000001",
            VoucherDate = DateTime.UtcNow,
            IsDeleted = 0
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.DeleteAsync(2));
        Assert.Contains("vouchers already use this voucher type", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_CustomUnusedType_SoftDeletesSuccessfully()
    {
        var db = CreateDb();
        var ctx = CreateContext();
        var service = new Fin1003Service(db, ctx);

        db.FinVoucherTypes.Add(new FinVoucherType
        {
            VoucherTypeNo = 3,
            CompanyNo = 1,
            VoucherTypeCode = "TEMP",
            VoucherTypeName = "Temporary Type",
            BaseKind = 1,
            IsSystem = 0,
            IsActive = 1,
            IsDeleted = 0
        });
        await db.SaveChangesAsync();

        await service.DeleteAsync(3);

        var deleted = await db.FinVoucherTypes.FindAsync(3L);
        Assert.NotNull(deleted);
        Assert.Equal((short)1, deleted.IsDeleted);
        Assert.Equal((short)0, deleted.IsActive);
    }

    [Fact(Skip = "Requires live running API server at localhost:5275")]
    public async Task LiveApi_VoucherTypes_And_Lookups_Return200()
    {
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        config.Setup(c => c["Aidly:Jwt:Secret"]).Returns("xww3jckXeNaHYDG9n5gUJllRuQV3HrAbMEGEtLSGoZF");
        config.Setup(c => c["Aidly:Jwt:Issuer"]).Returns("AidlyErp");
        config.Setup(c => c["Aidly:Jwt:Audience"]).Returns("AidlyErpClients");
        config.Setup(c => c["Aidly:Jwt:AccessTokenExpiration"]).Returns("3600000");

        var jwt = new AidlyErp.Shared.Infrastructure.Security.JwtTokenService(
            config.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<AidlyErp.Shared.Infrastructure.Security.JwtTokenService>>().Object);

        var permBits = new Dictionary<string, int> { ["FIN_1003"] = 95, ["FIN_1101"] = 95 };
        var token = jwt.GenerateAccessToken(
            new AidlyErp.Shared.Contracts.AidlyUserDetails(1, 2, 1, AccessScope.Company, "asibul", "asibul"),
            null,
            new long[] { 1 },
            1,
            permBits
        );

        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5275") };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Company-No", "2");
        client.DefaultRequestHeaders.Add("X-Branch-No", "1");
        client.DefaultRequestHeaders.Add("X-Form-Id", "FIN_1003");
        client.DefaultRequestHeaders.Add("X-User-No", "1");

        var vtypesRes = await client.GetAsync("/api/v1/fin/forms/fin1003/voucher-types");
        Assert.True(vtypesRes.IsSuccessStatusCode, $"voucher-types returned {(int)vtypesRes.StatusCode} {await vtypesRes.Content.ReadAsStringAsync()}");

        var lookupsRes = await client.GetAsync("/api/v1/fin/forms/fin1003/lookups");
        Assert.True(lookupsRes.IsSuccessStatusCode, $"lookups returned {(int)lookupsRes.StatusCode} {await lookupsRes.Content.ReadAsStringAsync()}");

        // Test Live PUT Partial Update with boolean is_active: false
        var content = new StringContent("{\"is_active\":false,\"voucher_type_no\":2,\"row_version\":1}", System.Text.Encoding.UTF8, "application/json");
        var putRes = await client.PutAsync("/api/v1/fin/forms/fin1003/voucher-types/2", content);
        Assert.True(putRes.IsSuccessStatusCode, $"PUT returned {(int)putRes.StatusCode} {await putRes.Content.ReadAsStringAsync()}");
    }
}
