using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Hrm.Application.Dto;
using AidlyErp.Sys.Domain;

namespace AidlyErp.Hrm.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1008Service — HR Policy / Settings
// Full port of Java Hrm1008Service (141 lines). Reads/writes the hr.* keys in
// sys_setting (group HR) for the active company. Curated settings form (one
// record per company). Missing keys fall back to BD-pack defaults.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1008Service
{
    Task<Hrm1008SettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<Hrm1008SettingsDto> SaveSettingsAsync(Hrm1008SettingsDto dto, CancellationToken ct = default);
}

public class Hrm1008Service : IHrm1008Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const string Group = "HR";

    // Setting keys
    private const string KCountry = "hr.country_code";
    private const string KCurrency = "hr.currency_code";
    private const string KWeekend = "hr.weekend_days";
    private const string KFY = "hr.fiscal_year_start";
    private const string KOT = "hr.overtime_multiplier";
    private const string KOTFormula = "hr.overtime_formula";
    private const string KDayFormula = "hr.daily_rate_formula";
    private const string KLwpFormula = "hr.lwp_formula";
    private const string KTax = "hr.tax_regime";
    private const string KPF = "hr.enable_provident_fund";
    private const string KGrat = "hr.enable_gratuity";
    private const string KFest = "hr.enable_festival_bonus";
    private const string KMat = "hr.maternity_weeks";

    public Hrm1008Service(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<Hrm1008SettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        long companyNo = Company();
        return new Hrm1008SettingsDto
        {
            CountryCode = await GetStrAsync(companyNo, KCountry, "BD", ct),
            CurrencyCode = await GetStrAsync(companyNo, KCurrency, "BDT", ct),
            WeekendDays = await GetStrAsync(companyNo, KWeekend, "FRI,SAT", ct),
            FiscalYearStart = await GetStrAsync(companyNo, KFY, "07-01", ct),
            OvertimeMultiplier = await GetDecimalAsync(companyNo, KOT, 2.0m, ct),
            OvertimeFormula = await GetStrAsync(companyNo, KOTFormula, "BASIC_SALARY / 104", ct),
            DailyRateFormula = await GetStrAsync(companyNo, KDayFormula, "GROSS_SALARY / CALENDAR_DAYS", ct),
            LwpFormula = await GetStrAsync(companyNo, KLwpFormula, "DAILY_RATE * LWP_DAYS", ct),
            TaxRegime = await GetStrAsync(companyNo, KTax, "BD_NBR", ct),
            EnableProvidentFund = await GetShortAsync(companyNo, KPF, 1, ct),
            EnableGratuity = await GetShortAsync(companyNo, KGrat, 1, ct),
            EnableFestivalBonus = await GetShortAsync(companyNo, KFest, 1, ct),
            MaternityWeeks = await GetIntAsync(companyNo, KMat, 16, ct)
        };
    }

    public async Task<Hrm1008SettingsDto> SaveSettingsAsync(Hrm1008SettingsDto dto, CancellationToken ct = default)
    {
        long companyNo = Company();
        await UpsertAsync(companyNo, KCountry, dto.CountryCode, 1, "HR country pack", ct);
        await UpsertAsync(companyNo, KCurrency, dto.CurrencyCode, 1, "Payroll currency", ct);
        await UpsertAsync(companyNo, KWeekend, dto.WeekendDays, 1, "Weekly-off days", ct);
        await UpsertAsync(companyNo, KFY, dto.FiscalYearStart, 5, "Fiscal year start (MM-DD)", ct);
        await UpsertAsync(companyNo, KOT, dto.OvertimeMultiplier?.ToString(), 2, "Overtime rate multiplier", ct);
        await UpsertAsync(companyNo, KOTFormula, dto.OvertimeFormula, 1, "Overtime rate formula", ct);
        await UpsertAsync(companyNo, KDayFormula, dto.DailyRateFormula, 1, "Daily rate formula", ct);
        await UpsertAsync(companyNo, KLwpFormula, dto.LwpFormula, 1, "LWP deduction formula", ct);
        await UpsertAsync(companyNo, KTax, dto.TaxRegime, 1, "Payroll tax regime", ct);
        await UpsertAsync(companyNo, KPF, FlagStr(dto.EnableProvidentFund), 3, "Enable provident fund", ct);
        await UpsertAsync(companyNo, KGrat, FlagStr(dto.EnableGratuity), 3, "Enable gratuity", ct);
        await UpsertAsync(companyNo, KFest, FlagStr(dto.EnableFestivalBonus), 3, "Enable festival bonus", ct);
        await UpsertAsync(companyNo, KMat, dto.MaternityWeeks?.ToString(), 2, "Maternity leave weeks", ct);
        return await GetSettingsAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task UpsertAsync(long companyNo, string key, string? value, short valueType, string description, CancellationToken ct)
    {
        if (value == null) return; // Leave existing/default untouched.
        var entity = await _db.Settings.FirstOrDefaultAsync(s => s.CompanyNo == companyNo && s.SettingKey == key && s.IsDeleted == 0, ct);
        if (entity == null)
        {
            entity = new Setting
            {
                CompanyNo = companyNo,
                SettingKey = key,
                SettingGroup = Group,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.Settings.Add(entity);
        }
        entity.SettingValue = value;
        entity.ValueType = valueType;
        if (entity.SettingGroup == null) entity.SettingGroup = Group;
        if (entity.Description == null) entity.Description = description;
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> GetStrAsync(long companyNo, string key, string defaultValue, CancellationToken ct)
    {
        var setting = await _db.Settings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyNo == companyNo && s.SettingKey == key && s.IsDeleted == 0, ct);
        var value = setting?.SettingValue;
        return !string.IsNullOrWhiteSpace(value) ? value : defaultValue;
    }

    private async Task<short> GetShortAsync(long companyNo, string key, short defaultValue, CancellationToken ct)
    {
        var str = await GetStrAsync(companyNo, key, defaultValue.ToString(), ct);
        return short.TryParse(str, out var result) ? result : defaultValue;
    }

    private async Task<int> GetIntAsync(long companyNo, string key, int defaultValue, CancellationToken ct)
    {
        var str = await GetStrAsync(companyNo, key, defaultValue.ToString(), ct);
        return int.TryParse(str, out var result) ? result : defaultValue;
    }

    private async Task<decimal> GetDecimalAsync(long companyNo, string key, decimal defaultValue, CancellationToken ct)
    {
        var str = await GetStrAsync(companyNo, key, defaultValue.ToString(), ct);
        return decimal.TryParse(str, out var result) ? result : defaultValue;
    }

    private long Company()
    {
        var c = _ctx.CompanyNo;
        if (c == null) throw new ValidationException("No active company in context");
        return c.Value;
    }

    private static string? FlagStr(short? value)
    {
        if (!value.HasValue) return null;
        if (value.Value is not (0 or 1)) throw new ValidationException("Flag must be 0 or 1");
        return value.Value.ToString();
    }
}
