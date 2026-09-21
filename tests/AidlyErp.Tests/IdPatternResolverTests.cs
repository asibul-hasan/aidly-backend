using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Numbering;
using Xunit;

namespace AidlyErp.Tests;

/// <summary>
/// Document numbers are permanent and externally visible — a wrong one is discovered by an
/// auditor, not by a test run. These pin the exact output of every token so the .NET port keeps
/// producing what the Java implementation produced for patterns already in use.
/// </summary>
public class IdPatternResolverTests
{
    private static IdPatternResolver.Ctx FullCtx() => new(
        DocDate: new DateOnly(2026, 8, 14),
        BranchCode: "DHK",
        CompanyCode: "AID",
        DocSubType: "INV",
        FiscalYearStart: 2026,
        FiscalYearEnd: 2027,
        SequenceValue: 42);

    [Theory]
    [InlineData("{SEQ:6}", "000042")]
    [InlineData("INV-{SEQ:4}", "INV-0042")]
    [InlineData("{DOC_TYPE}-{SEQ:3}", "INV-042")]
    [InlineData("{BRANCH}/{SEQ:2}", "DHK/42")]
    [InlineData("{COMPANY}-{SEQ:1}", "AID-42")]
    [InlineData("{YY}{MM}-{SEQ:4}", "2608-0042")]
    [InlineData("{YYYY}/{SEQ:4}", "2026/0042")]
    [InlineData("{FY_YY}-{SEQ:4}", "26-0042")]
    [InlineData("{FY_YY_YY}-{SEQ:4}", "26-27-0042")]
    [InlineData("{FY_YYYY}-{SEQ:4}", "2026-0042")]
    public void Resolves_every_token(string pattern, string expected) =>
        Assert.Equal(expected, IdPatternResolver.Resolve(pattern, FullCtx()));

    [Fact]
    public void Sequence_wider_than_the_value_pads_left()
    {
        var ctx = FullCtx() with { SequenceValue = 7 };
        Assert.Equal("0000007", IdPatternResolver.Resolve("{SEQ:7}", ctx));
    }

    [Fact]
    public void Sequence_longer_than_its_width_is_not_truncated()
    {
        // Better a number one digit too wide than two documents sharing an id.
        var ctx = FullCtx() with { SequenceValue = 1234567 };
        Assert.Equal("1234567", IdPatternResolver.Resolve("{SEQ:4}", ctx));
    }

    [Fact]
    public void Literal_text_around_tokens_is_preserved() =>
        Assert.Equal("A/26-27/DHK-000042/X",
            IdPatternResolver.Resolve("A/{FY_YY_YY}/{BRANCH}-{SEQ:6}/X", FullCtx()));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_pattern_is_rejected(string? pattern) =>
        Assert.Throws<ValidationException>(() => IdPatternResolver.Validate(pattern));

    [Fact]
    public void Pattern_without_a_sequence_is_rejected() =>
        // Every document would otherwise receive the same number.
        Assert.Throws<ValidationException>(() => IdPatternResolver.Validate("INV-{YYYY}"));

    [Fact]
    public void Pattern_with_two_sequences_is_rejected() =>
        Assert.Throws<ValidationException>(() => IdPatternResolver.Validate("{SEQ:4}-{SEQ:4}"));

    [Fact]
    public void Unknown_token_is_rejected() =>
        Assert.Throws<ValidationException>(() => IdPatternResolver.Validate("{NOPE}-{SEQ:4}"));

    [Theory]
    [InlineData("{SEQ:0}")]
    [InlineData("{SEQ:13}")]
    [InlineData("{SEQ:x}")]
    public void Bad_sequence_width_is_rejected(string pattern) =>
        Assert.Throws<ValidationException>(() => IdPatternResolver.Validate(pattern));

    [Fact]
    public void Token_that_takes_no_argument_is_rejected() =>
        Assert.Throws<ValidationException>(() => IdPatternResolver.Validate("{BRANCH:2}-{SEQ:4}"));

    [Fact]
    public void Missing_context_throws_rather_than_emitting_a_hole()
    {
        var noFy = FullCtx() with { FiscalYearStart = null };
        Assert.Throws<ValidationException>(() => IdPatternResolver.Resolve("{FY_YY}-{SEQ:4}", noFy));

        var noDate = FullCtx() with { DocDate = null };
        Assert.Throws<ValidationException>(() => IdPatternResolver.Resolve("{YYYY}-{SEQ:4}", noDate));

        var noBranch = FullCtx() with { BranchCode = null };
        Assert.Throws<ValidationException>(() => IdPatternResolver.Resolve("{BRANCH}-{SEQ:4}", noBranch));

        var noSeq = FullCtx() with { SequenceValue = null };
        Assert.Throws<ValidationException>(() => IdPatternResolver.Resolve("{SEQ:4}", noSeq));
    }

    [Fact]
    public void Tokens_used_reports_what_the_pattern_needs()
    {
        var used = IdPatternResolver.TokensUsed("{BRANCH}/{FY_YY}-{SEQ:4}");
        Assert.Equal(new[] { "BRANCH", "FY_YY", "SEQ" }, used);
    }

    [Fact]
    public void Tokens_used_on_a_plain_pattern_is_empty() =>
        Assert.Empty(IdPatternResolver.TokensUsed("no tokens here"));
}
