using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Hrm.Dto;
using AidlyErp.Domain.Hrm;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1009Service — Tax Slab Setup
// Port of Java Hrm1009Service (312 lines). Profile-based CRUD: tax slabs are
// grouped by (country_code, fiscal_year, taxpayer_class) into profiles.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1009Service
{
    Task<List<Hrm1009TaxSlabDto>> GetListAsync(CancellationToken ct = default);
    Task<Hrm1009TaxSlabDto> GetDetailAsync(long taxSlabNo, CancellationToken ct = default);
    Task<Hrm1009TaxSlabDto> SaveAsync(Hrm1009TaxSlabDto dto, CancellationToken ct = default);
    Task DeleteAsync(long taxSlabNo, CancellationToken ct = default);
}

public class Hrm1009Service : IHrm1009Service
{
    private readonly IApplicationDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private static readonly decimal Hundred = 100m;

    public Hrm1009Service(IApplicationDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    // ── reads ──────────────────────────────────────────────────────────────

    public async Task<List<Hrm1009TaxSlabDto>> GetListAsync(CancellationToken ct = default)
    {
        // Group all rows by profile key, preserving order.
        var rows = await _db.HrmTaxSlabs.AsNoTracking()
            .Where(x => x.IsDeleted == 0)
            .OrderBy(x => x.CountryCode).ThenByDescending(x => x.FiscalYear)
            .ThenBy(x => x.TaxpayerClass).ThenBy(x => x.SlabOrder).ThenBy(x => x.TaxSlabNo)
            .ToListAsync(ct);

        var grouped = new Dictionary<string, List<HrmTaxSlab>>();
        foreach (var row in rows)
        {
            var key = ProfileKey(row.CountryCode, row.FiscalYear, row.TaxpayerClass);
            if (!grouped.ContainsKey(key)) grouped[key] = new();
            grouped[key].Add(row);
        }

        return grouped.Values.Select(g => ToDto(g, false)).ToList();
    }

    public async Task<Hrm1009TaxSlabDto> GetDetailAsync(long taxSlabNo, CancellationToken ct = default)
    {
        var head = await LoadLiveAsync(taxSlabNo, ct);
        var profile = await LoadProfileAsync(head.CountryCode, head.FiscalYear, head.TaxpayerClass, ct);
        return ToDto(profile, true);
    }

    // ── write ──────────────────────────────────────────────────────────────

    public async Task<Hrm1009TaxSlabDto> SaveAsync(Hrm1009TaxSlabDto dto, CancellationToken ct = default)
    {
        return dto.TaxSlabNo.HasValue
            ? await UpdateAsync(dto.TaxSlabNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    public async Task DeleteAsync(long taxSlabNo, CancellationToken ct = default)
    {
        var head = await LoadLiveAsync(taxSlabNo, ct);
        var rows = await LoadProfileAsync(head.CountryCode, head.FiscalYear, head.TaxpayerClass, ct);
        var userNo = _ctx.CurrentUserNo();
        foreach (var row in rows) row.PerformSoftDelete(userNo);
        await _db.SaveChangesAsync(ct);
    }

    // ── insert / update ────────────────────────────────────────────────────

    private async Task<Hrm1009TaxSlabDto> InsertAsync(Hrm1009TaxSlabDto dto, CancellationToken ct)
    {
        var countryCode = NormalizeCountry(dto.CountryCode);
        var fiscalYear = NormalizeFiscalYear(dto.FiscalYear);
        var taxpayerClass = NormalizeTaxpayerClass(dto.TaxpayerClass);
        var slabs = NormalizeSlabs(dto.Slabs);

        if (await _db.HrmTaxSlabs.AnyAsync(x =>
            x.CountryCode == countryCode && x.FiscalYear == fiscalYear && x.TaxpayerClass == taxpayerClass && x.IsDeleted == 0, ct))
            throw new ValidationException("Tax slab profile already exists for the selected country, fiscal year and taxpayer class");

        await SaveRowsAsync(countryCode, fiscalYear, taxpayerClass, NormalizeFlag(dto.IsActive, 1), slabs, ct);
        var profile = await LoadProfileAsync(countryCode, fiscalYear, taxpayerClass, ct);
        return ToDto(profile, true);
    }

    private async Task<Hrm1009TaxSlabDto> UpdateAsync(long taxSlabNo, Hrm1009TaxSlabDto dto, CancellationToken ct)
    {
        var current = await LoadLiveAsync(taxSlabNo, ct);
        var existingRows = await LoadProfileAsync(current.CountryCode, current.FiscalYear, current.TaxpayerClass, ct);

        var countryCode = NormalizeCountry(dto.CountryCode);
        var fiscalYear = NormalizeFiscalYear(dto.FiscalYear);
        var taxpayerClass = NormalizeTaxpayerClass(dto.TaxpayerClass);
        var slabs = NormalizeSlabs(dto.Slabs);

        var profileChanged = current.CountryCode != countryCode
                          || current.FiscalYear != fiscalYear
                          || current.TaxpayerClass != taxpayerClass;

        if (profileChanged && await _db.HrmTaxSlabs.AnyAsync(x =>
            x.CountryCode == countryCode && x.FiscalYear == fiscalYear && x.TaxpayerClass == taxpayerClass && x.IsDeleted == 0, ct))
            throw new ValidationException("Another tax slab profile already exists for the selected country, fiscal year and taxpayer class");

        // Soft-delete old rows.
        var userNo = _ctx.CurrentUserNo();
        foreach (var row in existingRows) row.PerformSoftDelete(userNo);
        await _db.SaveChangesAsync(ct);

        // Save new rows.
        await SaveRowsAsync(countryCode, fiscalYear, taxpayerClass, NormalizeFlag(dto.IsActive, current.IsActive), slabs, ct);

        var profile = await LoadProfileAsync(countryCode, fiscalYear, taxpayerClass, ct);
        return ToDto(profile, true);
    }

    // ── save rows ──────────────────────────────────────────────────────────

    private async Task SaveRowsAsync(string countryCode, string fiscalYear, string taxpayerClass,
        short isActive, List<Hrm1009TaxSlabLineDto> slabs, CancellationToken ct)
    {
        foreach (var slab in slabs)
        {
            var row = new HrmTaxSlab
            {
                CountryCode = countryCode,
                FiscalYear = fiscalYear,
                TaxpayerClass = taxpayerClass,
                SlabOrder = slab.SlabOrder,
                FromAmount = slab.FromAmount,
                ToAmount = slab.ToAmount,
                RatePercent = slab.RatePercent,
                FixedAmount = slab.FixedAmount ?? 0m,
                IsActive = isActive,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.HrmTaxSlabs.Add(row);
        }
        await _db.SaveChangesAsync(ct);
    }

    // ── load helpers ───────────────────────────────────────────────────────

    private async Task<HrmTaxSlab> LoadLiveAsync(long taxSlabNo, CancellationToken ct) =>
        await _db.HrmTaxSlabs.AsNoTracking().FirstOrDefaultAsync(x => x.TaxSlabNo == taxSlabNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Tax slab row not found: taxSlabNo={taxSlabNo}");

    private async Task<List<HrmTaxSlab>> LoadProfileAsync(string countryCode, string fiscalYear, string taxpayerClass, CancellationToken ct)
    {
        var rows = await _db.HrmTaxSlabs.AsNoTracking()
            .Where(x => x.CountryCode == countryCode && x.FiscalYear == fiscalYear && x.TaxpayerClass == taxpayerClass && x.IsDeleted == 0)
            .OrderBy(x => x.SlabOrder)
            .ToListAsync(ct);
        if (rows.Count == 0) throw new NotFoundException("Tax slab profile not found");
        return rows;
    }

    // ── normalization / validation ─────────────────────────────────────────

    private static string NormalizeCountry(string? value)
    {
        var normalized = TrimRequired(value, "Country code").ToUpperInvariant();
        if (normalized.Length > 2) throw new ValidationException("Country code must not exceed 2 characters");
        return normalized;
    }

    private static string NormalizeFiscalYear(string? value)
    {
        var normalized = TrimRequired(value, "Fiscal year");
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^\d{4}-\d{4}$"))
            throw new ValidationException("Fiscal year must follow the format YYYY-YYYY");
        var parts = normalized.Split('-');
        int start = int.Parse(parts[0]), end = int.Parse(parts[1]);
        if (end != start + 1) throw new ValidationException("Fiscal year end must be the next year after the start year");
        return normalized;
    }

    private static string NormalizeTaxpayerClass(string? value)
    {
        var normalized = TrimRequired(value, "Taxpayer class").ToUpperInvariant();
        if (normalized.Length > 30) throw new ValidationException("Taxpayer class must not exceed 30 characters");
        return normalized;
    }

    private static List<Hrm1009TaxSlabLineDto> NormalizeSlabs(List<Hrm1009TaxSlabLineDto>? slabs)
    {
        if (slabs == null || slabs.Count == 0) throw new ValidationException("At least one tax slab row is required");

        var normalized = slabs
            .Where(s => s != null)
            .Select(NormalizeLine)
            .OrderBy(s => s.SlabOrder)
            .ToList();

        if (normalized.Count == 0) throw new ValidationException("At least one valid tax slab row is required");

        // Validate sequential ordering, non-overlapping ranges, first starts at 0.
        decimal? previousTo = null;
        int expectedOrder = 1;
        foreach (var line in normalized)
        {
            if (line.SlabOrder != expectedOrder)
                throw new ValidationException("Slab order must be sequential starting from 1");
            if (previousTo == null && line.FromAmount != 0)
                throw new ValidationException("The first tax slab must start from 0");
            if (previousTo != null && line.FromAmount < previousTo)
                throw new ValidationException("Tax slab ranges cannot overlap");
            if (previousTo == null && line.ToAmount == null && normalized.Count > 1)
                throw new ValidationException("Only the last tax slab can have an open-ended maximum amount");
            previousTo = line.ToAmount;
            expectedOrder++;
        }

        // Only the last slab can be open-ended.
        for (int i = 0; i < normalized.Count - 1; i++)
        {
            if (normalized[i].ToAmount == null)
                throw new ValidationException("Only the last tax slab can have an open-ended maximum amount");
        }

        return normalized;
    }

    private static Hrm1009TaxSlabLineDto NormalizeLine(Hrm1009TaxSlabLineDto line)
    {
        if (line.SlabOrder < 1) throw new ValidationException("Slab order must be 1 or greater");

        decimal fromAmount = line.FromAmount;
        decimal? toAmount = line.ToAmount;
        decimal ratePercent = line.RatePercent;
        decimal fixedAmount = line.FixedAmount ?? 0m;

        if (fromAmount < 0) throw new ValidationException("From amount cannot be negative");
        if (toAmount.HasValue && toAmount.Value < fromAmount) throw new ValidationException("To amount cannot be lower than from amount");
        if (ratePercent < 0 || ratePercent > Hundred) throw new ValidationException("Rate percent must be between 0 and 100");
        if (fixedAmount < 0) throw new ValidationException("Fixed amount cannot be negative");

        return new Hrm1009TaxSlabLineDto
        {
            TaxSlabNo = line.TaxSlabNo,
            SlabOrder = line.SlabOrder,
            FromAmount = fromAmount,
            ToAmount = toAmount,
            RatePercent = ratePercent,
            FixedAmount = fixedAmount,
            IsActive = NormalizeFlag(line.IsActive, 1),
            RowVersion = line.RowVersion
        };
    }

    // ── DTO mapping ────────────────────────────────────────────────────────

    private static Hrm1009TaxSlabDto ToDto(List<HrmTaxSlab> rows, bool includeSlabs)
    {
        var first = rows[0];
        var dto = new Hrm1009TaxSlabDto
        {
            TaxSlabNo = first.TaxSlabNo,
            CountryCode = first.CountryCode,
            FiscalYear = first.FiscalYear,
            TaxpayerClass = first.TaxpayerClass,
            SlabCount = rows.Count,
            IsActive = first.IsActive,
            RowVersion = first.RowVersion
        };
        if (includeSlabs)
            dto.Slabs = rows.Select(ToLineDto).ToList();
        return dto;
    }

    private static Hrm1009TaxSlabLineDto ToLineDto(HrmTaxSlab row) => new()
    {
        TaxSlabNo = row.TaxSlabNo,
        SlabOrder = row.SlabOrder,
        FromAmount = row.FromAmount,
        ToAmount = row.ToAmount,
        RatePercent = row.RatePercent,
        FixedAmount = row.FixedAmount,
        IsActive = row.IsActive,
        RowVersion = row.RowVersion
    };

    // ── utility ────────────────────────────────────────────────────────────

    private static string ProfileKey(string countryCode, string fiscalYear, string taxpayerClass) =>
        $"{countryCode}|{fiscalYear}|{taxpayerClass}";

    private static short NormalizeFlag(short value, short defaultValue)
    {
        if (value == 0 || value == 1) return value;
        throw new ValidationException("Flag values must be 0 or 1");
    }

    private static string TrimRequired(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{label} is required");
        return value.Trim();
    }
}
