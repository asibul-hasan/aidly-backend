using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Security;

namespace AidlyErp.Application.Hrm.Services;

/// <summary>
/// Cross-cutting validation helpers shared across HRM setup services.
/// Port of Java's normalizeFlag, trimRequired, resolveBranch patterns.
/// </summary>
internal static class HrmValidation
{
    public static string TrimRequired(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{label} is required");
        var trimmed = value.Trim();
        if (trimmed.Length == 0) throw new ValidationException($"{label} cannot be blank");
        return trimmed;
    }

    public static short NormalizeFlag(short value, short defaultValue, string fieldName)
    {
        if (value == defaultValue) return value;
        if (value is 0 or 1) return value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }

    public static short? NormalizeFlagNullable(short? value, short? defaultValue, string fieldName)
    {
        if (!value.HasValue) return defaultValue;
        if (value.Value is 0 or 1) return value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }

    public static long ResolveBranch(long? branchNoFromDto, ICompanyBranchContext ctx)
    {
        var branchNo = branchNoFromDto ?? ctx.BranchNo;
        if (branchNo == null) throw new ValidationException("Branch is required");
        return branchNo.Value;
    }

    public static void NonNegative(decimal value, string label)
    {
        if (value < 0) throw new ValidationException($"{label} cannot be negative");
    }

    public static void NonNegative(int value, string label)
    {
        if (value < 0) throw new ValidationException($"{label} cannot be negative");
    }

    public static void RangeCheck(short value, short min, short max, string label)
    {
        if (value < min || value > max) throw new ValidationException($"{label} must be between {min} and {max}");
    }
}
