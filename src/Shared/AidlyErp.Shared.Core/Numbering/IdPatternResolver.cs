using System.Text;
using System.Text.RegularExpressions;
using AidlyErp.Shared.Core.Exceptions;

namespace AidlyErp.Shared.Core.Numbering;

/// <summary>
/// Compiles a document-number pattern such as <c>INV-{FY_YY_YY}-{SEQ:6}</c> into a finished
/// number. Ported from the Java <c>IdPatternResolver</c>, token for token, so patterns already
/// configured against that backend keep producing identical numbers.
///
/// <para>Every token a pattern uses must have its value present in the context. A missing value
/// throws rather than silently emitting a blank, because a document number with a hole in it is
/// worse than a refusal to generate one — it looks valid and collides later.</para>
/// </summary>
public static class IdPatternResolver
{
    /// <summary>Everything a pattern might need. All optional; a token whose value is absent throws.</summary>
    public sealed record Ctx(
        DateOnly? DocDate = null,
        string? BranchCode = null,
        string? CompanyCode = null,
        string? DocSubType = null,
        int? FiscalYearStart = null,
        int? FiscalYearEnd = null,
        long? SequenceValue = null);

    private static readonly Regex TokenRe = new(@"\{([A-Z_]+)(?::([^}]+))?\}", RegexOptions.Compiled);

    private static readonly HashSet<string> KnownTokens = new(StringComparer.Ordinal)
    {
        "DOC_TYPE", "BRANCH", "COMPANY",
        "FY_YY", "FY_YY_YY", "FY_YYYY",
        "YY", "YYYY", "MM",
        "SEQ"
    };

    /// <summary>Every token the setup form offers, for its tooltip.</summary>
    public static IReadOnlyList<string> SupportedTokens { get; } = new[]
    {
        "{DOC_TYPE}", "{BRANCH}", "{COMPANY}",
        "{FY_YY}", "{FY_YY_YY}", "{FY_YYYY}",
        "{YY}", "{YYYY}", "{MM}",
        "{SEQ:n}"
    };

    /// <summary>
    /// Rejects a pattern the setup form should not save. Exactly one <c>{SEQ:n}</c> is required:
    /// none means every document gets the same number, more than one means the counter appears
    /// twice in a single id.
    /// </summary>
    public static void Validate(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ValidationException("Pattern is required");

        int seqCount = 0;
        foreach (Match m in TokenRe.Matches(pattern))
        {
            string name = m.Groups[1].Value;
            string? arg = m.Groups[2].Success ? m.Groups[2].Value : null;

            if (!KnownTokens.Contains(name))
                throw new ValidationException($"Unknown token: {{{name}}}");

            if (name == "SEQ")
            {
                seqCount++;
                int digits = ParseSeqDigits(arg);
                if (digits < 1 || digits > 12)
                    throw new ValidationException($"{{SEQ:n}} width must be 1–12 (got {arg})");
            }
            else if (arg is not null)
            {
                throw new ValidationException($"Token {{{name}}} does not take an argument");
            }
        }

        if (seqCount == 0)
            throw new ValidationException("Pattern must contain exactly one {SEQ:n} token");
        if (seqCount > 1)
            throw new ValidationException($"Pattern must contain exactly one {{SEQ:n}} token (found {seqCount})");
    }

    /// <summary>Compiles <paramref name="pattern"/> against <paramref name="ctx"/>.</summary>
    public static string Resolve(string pattern, Ctx ctx)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(ctx);

        var outp = new StringBuilder();
        int last = 0;
        foreach (Match m in TokenRe.Matches(pattern))
        {
            outp.Append(pattern, last, m.Index - last);
            outp.Append(ResolveToken(m.Groups[1].Value,
                                     m.Groups[2].Success ? m.Groups[2].Value : null, ctx));
            last = m.Index + m.Length;
        }
        outp.Append(pattern, last, pattern.Length - last);
        return outp.ToString();
    }

    /// <summary>
    /// The tokens a pattern actually uses, in order. The setup form warns with this when a
    /// pattern needs context the company has not configured — a fiscal-year token with no
    /// fiscal year, for instance.
    /// </summary>
    public static IReadOnlyList<string> TokensUsed(string? pattern)
    {
        var found = new List<string>();
        if (string.IsNullOrEmpty(pattern)) return found;

        foreach (Match m in TokenRe.Matches(pattern))
        {
            var name = m.Groups[1].Value;
            if (!found.Contains(name)) found.Add(name);
        }
        return found;
    }

    private static string ResolveToken(string name, string? arg, Ctx ctx) => name switch
    {
        "DOC_TYPE" => Require(ctx.DocSubType, "{DOC_TYPE}"),
        "BRANCH" => Require(ctx.BranchCode, "{BRANCH}"),
        "COMPANY" => Require(ctx.CompanyCode, "{COMPANY}"),
        "FY_YY" => Two(RequireFyStart(ctx)),
        "FY_YY_YY" => Two(RequireFyStart(ctx)) + "-" + Two(RequireFyEnd(ctx)),
        "FY_YYYY" => RequireFyStart(ctx).ToString(),
        "YY" => Two(RequireDate(ctx).Year),
        "YYYY" => RequireDate(ctx).Year.ToString(),
        "MM" => RequireDate(ctx).Month.ToString("00"),
        "SEQ" => PadSeq(ctx, ParseSeqDigits(arg)),
        _ => throw new ValidationException($"Unknown token: {{{name}}}")
    };

    private static int ParseSeqDigits(string? arg)
    {
        if (arg is null) throw new ValidationException("{SEQ} requires a width, e.g. {SEQ:4}");
        if (!int.TryParse(arg.Trim(), out int digits))
            throw new ValidationException($"{{SEQ:n}} width must be a number (got '{arg}')");
        return digits;
    }

    private static string PadSeq(Ctx ctx, int width)
    {
        if (ctx.SequenceValue is null)
            throw new ValidationException("{SEQ:n} requires a sequence value in context");
        return ctx.SequenceValue.Value.ToString(new string('0', width));
    }

    private static DateOnly RequireDate(Ctx ctx) =>
        ctx.DocDate ?? throw new ValidationException("Date tokens require a document date in context");

    private static int RequireFyStart(Ctx ctx) =>
        ctx.FiscalYearStart ?? throw new ValidationException("Fiscal-year tokens require an active fiscal year in context");

    private static int RequireFyEnd(Ctx ctx) =>
        ctx.FiscalYearEnd ?? throw new ValidationException("Fiscal-year tokens require an active fiscal year in context");

    private static string Require(string? value, string token) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ValidationException($"Token {token} requires a value in context")
            : value;

    /// <summary>Last two digits of a year — 2026 becomes "26".</summary>
    private static string Two(int year) => (year % 100).ToString("00");
}
