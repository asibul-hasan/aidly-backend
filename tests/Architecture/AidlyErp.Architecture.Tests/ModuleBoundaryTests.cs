using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace AidlyErp.Architecture.Tests;

/// <summary>
/// Executable form of the module-isolation rules. These are the guard rails that stop the
/// modular monolith decaying back into a big ball of mud — a Sales handler quietly reaching for an
/// Inventory table is a compile-time impossibility today, and these tests keep it that way as
/// project references get edited.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly string[] Modules = ["Sys", "Hrm", "Fin", "Inv", "Pur", "Sal"];

    private static Assembly Load(string name) => Assembly.Load($"AidlyErp.{name}");

    public static TheoryData<string, string> ModulePairs()
    {
        var data = new TheoryData<string, string>();
        foreach (var from in Modules)
        {
            foreach (var to in Modules.Where(m => m != from))
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    public static TheoryData<string> AllModules()
    {
        var data = new TheoryData<string>();
        foreach (var m in Modules) data.Add(m);
        return data;
    }

    /// <summary>
    /// Rule 1 — No direct references. A module's Application layer may not touch another module's
    /// Domain, Application or Infrastructure. Only <c>.Contracts</c> is allowed, and that is
    /// covered by the next test.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Application_does_not_reference_another_modules_internals(string from, string to)
    {
        var result = Types.InAssembly(Load($"{from}.Application"))
            .Should()
            .NotHaveDependencyOnAny(
                $"AidlyErp.{to}.Domain",
                $"AidlyErp.{to}.Application",
                $"AidlyErp.{to}.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result, $"{from}.Application must not reach into {to}'s internals"));
    }

    /// <summary>Rule 1, applied to the Domain layer, which must not know any other module at all.</summary>
    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Domain_does_not_reference_another_module(string from, string to)
    {
        var result = Types.InAssembly(Load($"{from}.Domain"))
            .Should()
            .NotHaveDependencyOn($"AidlyErp.{to}")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result, $"{from}.Domain must not depend on {to}"));
    }

    /// <summary>
    /// Rule 2 — Contracts only. A module's public face may not drag its own internals along with
    /// it, or consumers end up transitively coupled to the schema anyway.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllModules))]
    public void Contracts_do_not_expose_module_internals(string module)
    {
        var result = Types.InAssembly(Load($"{module}.Contracts"))
            .Should()
            .NotHaveDependencyOnAny(
                $"AidlyErp.{module}.Domain",
                $"AidlyErp.{module}.Application",
                $"AidlyErp.{module}.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            Describe(result, $"{module}.Contracts must stay free of {module}'s internals"));
    }

    /// <summary>
    /// Clean Architecture within a module: Domain is the centre and depends on nothing but the
    /// shared kernel; Application never reaches for Infrastructure.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllModules))]
    public void Domain_does_not_depend_on_infrastructure_or_ef(string module)
    {
        var result = Types.InAssembly(Load($"{module}.Domain"))
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                $"AidlyErp.{module}.Infrastructure",
                $"AidlyErp.{module}.Application")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result, $"{module}.Domain must have no persistence dependency"));
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Application_does_not_depend_on_its_own_infrastructure(string module)
    {
        var result = Types.InAssembly(Load($"{module}.Application"))
            .Should()
            .NotHaveDependencyOn($"AidlyErp.{module}.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            Describe(result, $"{module}.Application must depend on abstractions, not {module}.Infrastructure"));
    }

    /// <summary>The shared kernel is upstream of everything, so it may not know any module.</summary>
    [Theory]
    [MemberData(nameof(AllModules))]
    public void Shared_does_not_depend_on_any_module(string module)
    {
        foreach (var shared in new[] { "Shared.Core", "Shared.Contracts", "Shared.Infrastructure" })
        {
            var result = Types.InAssembly(Load(shared))
                .Should()
                .NotHaveDependencyOn($"AidlyErp.{module}")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result, $"{shared} must not depend on the {module} module"));
        }
    }

    private static string Describe(TestResult result, string rule)
    {
        var offenders = result.FailingTypeNames is null
            ? "(none reported)"
            : string.Join(", ", result.FailingTypeNames);

        return $"{rule}. Offending types: {offenders}";
    }
}
