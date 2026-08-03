using AidlyErp.Fin.Infrastructure;
using AidlyErp.Hrm.Infrastructure;
using AidlyErp.Inv.Infrastructure;
using AidlyErp.Pur.Infrastructure;
using AidlyErp.Sal.Infrastructure;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace AidlyErp.Architecture.Tests;

/// <summary>
/// Compares what EF believes it can read against what the database actually has.
///
/// <para>Every column mismatch found so far compiled cleanly and only failed at runtime with
/// <c>42703: column ... does not exist</c> — a class of defect that static analysis and the build
/// cannot catch. Both sides here are authoritative: the mapped columns come from EF's own model,
/// the real ones from <c>information_schema</c>. No SQL text parsing, so no false positives.</para>
///
/// <para>Skipped when no connection string is configured, so it never breaks an offline build.</para>
/// </summary>
public class SchemaDriftTests
{
    private readonly ITestOutputHelper _output;

    public SchemaDriftTests(ITestOutputHelper output) => _output = output;

    private static string? ConnectionString()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return config.GetConnectionString("DefaultConnection")
               ?? Environment.GetEnvironmentVariable("AIDLY_CONNECTION");
    }

    private static DbContextOptions<T> Options<T>(string cs) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql(cs).Options;

    [Fact]
    public async Task Mapped_columns_all_exist_in_the_database()
    {
        var cs = ConnectionString();
        if (string.IsNullOrWhiteSpace(cs) || cs.Contains("__SET_VIA_ENV__"))
        {
            _output.WriteLine("No connection string configured — skipping.");
            return;
        }

        // Real columns and their nullability, straight from the database.
        var actual = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
        await using (var conn = new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT table_name, column_name, is_nullable FROM information_schema.columns "
                + "WHERE table_schema = 'public'", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = reader.GetString(0);
                if (!actual.TryGetValue(table, out var set))
                {
                    set = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    actual[table] = set;
                }

                set[reader.GetString(1)] = reader.GetString(2) == "YES";
            }
        }

        // Unauthenticated context: query filters go inert, so the full model is inspected.
        var tenant = new CompanyBranchContext();
        var contexts = new DbContext[]
        {
            new SysDbContext(Options<SysDbContext>(cs), tenant),
            new HrmDbContext(Options<HrmDbContext>(cs), tenant),
            new FinDbContext(Options<FinDbContext>(cs), tenant),
            new InvDbContext(Options<InvDbContext>(cs), tenant),
            new PurDbContext(Options<PurDbContext>(cs), tenant),
            new SalDbContext(Options<SalDbContext>(cs), tenant),
        };

        var drift = new List<string>();
        var nullability = new List<string>();
        var missingTables = new List<string>();

        foreach (var ctx in contexts)
        {
            foreach (var entity in ctx.Model.GetEntityTypes())
            {
                var table = entity.GetTableName();
                if (table is null) continue;

                if (!actual.TryGetValue(table, out var real))
                {
                    missingTables.Add($"{entity.ClrType.Name} → table '{table}'");
                    continue;
                }

                foreach (var prop in entity.GetProperties())
                {
                    var col = prop.GetColumnName();
                    if (col is null) continue;

                    if (!real.TryGetValue(col, out var dbNullable))
                    {
                        drift.Add($"{table}.{col}  ({entity.ClrType.Name}.{prop.Name})");
                        continue;
                    }

                    // A non-nullable CLR property over a nullable column throws
                    // InvalidCastException ("Column 'x' is null") the first time a row actually
                    // holds a null — invisible to the build and to an existence-only check.
                    if (dbNullable && !prop.IsNullable)
                    {
                        nullability.Add($"{table}.{col}  ({entity.ClrType.Name}.{prop.Name}) "
                                        + "is NOT NULL in code but NULLABLE in the database");
                    }
                }
            }

            await ctx.DisposeAsync();
        }

        foreach (var m in missingTables.Distinct().OrderBy(x => x)) _output.WriteLine($"MISSING TABLE  {m}");
        foreach (var d in drift.Distinct().OrderBy(x => x)) _output.WriteLine($"MISSING COLUMN {d}");
        foreach (var n in nullability.Distinct().OrderBy(x => x)) _output.WriteLine($"NULLABILITY    {n}");

        Assert.True(drift.Count == 0 && missingTables.Count == 0 && nullability.Count == 0,
            $"{drift.Distinct().Count()} mapped columns and {missingTables.Distinct().Count()} tables "
            + $"do not exist, and {nullability.Distinct().Count()} columns disagree on nullability. "
            + "See test output for the full list.");
    }
}
