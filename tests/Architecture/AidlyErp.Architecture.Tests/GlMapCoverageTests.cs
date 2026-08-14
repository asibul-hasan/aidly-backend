using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace AidlyErp.Architecture.Tests;

/// <summary>
/// Every GL event a module emits must have <c>fin_gl_map</c> rows to resolve its legs against.
///
/// <para>The posting engine never guesses an account: an unmapped leg parks the event as Failed
/// and the transaction silently never reaches the ledger. Nothing throws, no test fails, and the
/// only symptom is a general ledger that quietly disagrees with the sub-ledgers. An audit on
/// 2026-08-14 found eight event types in this state, including every purchase-side reversal.</para>
///
/// <para>The emitted list is maintained by hand because the alternative — scanning source for
/// string literals — breaks on any refactor and gives false confidence. When you add an event
/// type, add it here; the test then tells you the mapping is missing before a user finds out.</para>
///
/// <para>Skipped when no connection string is configured, so it never breaks an offline build.</para>
/// </summary>
public class GlMapCoverageTests
{
    private readonly ITestOutputHelper _output;

    public GlMapCoverageTests(ITestOutputHelper output) => _output = output;

    /// <summary>Event types emitted to <c>sys_event_outbox</c> for the FIN posting engine to drain.</summary>
    private static readonly string[] EmittedEventTypes =
    [
        // SAL
        "SalesInvoicePosted", "SalesInvoiceReversed",
        "SalesReturnPosted", "SalesReturnReversed",
        "CustomerReceiptPosted", "CustomerReceiptReversed",
        "PosSessionClosePosted",
        // PUR
        "PurchaseInvoicePosted", "PurchaseInvoiceReversed",
        "PurchaseReturnPosted", "PurchaseReturnReversed",
        "GoodsReceiptPosted", "GoodsReceiptReversed",
        "SupplierPaymentPosted", "SupplierPaymentReversed",
        "LandedCostApplied",
        // INV
        "StockAdjustmentPosted", "StockAdjustmentReversed",
        "OpeningStockPosted",
        // HRM
        "PayrollPosted", "FinalSettlementPosted",
    ];

    private static string? ConnectionString()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return config.GetConnectionString("DefaultConnection")
               ?? Environment.GetEnvironmentVariable("AIDLY_CONNECTION");
    }

    [Fact]
    public async Task Every_emitted_event_type_has_a_gl_mapping()
    {
        var cs = ConnectionString();
        if (string.IsNullOrWhiteSpace(cs) || cs.Contains("__SET_VIA_ENV__"))
        {
            _output.WriteLine("No connection string configured — skipping.");
            return;
        }

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        // Checked per company: a mapping seeded for one tenant says nothing about another, and a
        // company whose events all park is exactly the failure this is meant to catch.
        var mapped = new Dictionary<long, HashSet<string>>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT company_no, event_type FROM fin_gl_map WHERE is_deleted = 0", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                long company = r.GetInt64(0);
                if (!mapped.TryGetValue(company, out var set))
                    mapped[company] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(r.GetString(1));
            }
        }

        if (mapped.Count == 0)
        {
            _output.WriteLine("fin_gl_map is empty — no company is configured for GL posting yet.");
            return;
        }

        var failures = new List<string>();
        foreach (var (company, set) in mapped.OrderBy(x => x.Key))
        {
            var missing = EmittedEventTypes.Where(e => !set.Contains(e)).OrderBy(e => e).ToList();
            if (missing.Count > 0)
                failures.Add($"company {company} has no mapping for: {string.Join(", ", missing)}");
        }

        Assert.True(failures.Count == 0,
            "Unmapped GL events park as Failed and never reach the ledger:\n  " +
            string.Join("\n  ", failures));
    }
}
