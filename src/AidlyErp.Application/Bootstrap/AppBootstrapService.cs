using System.Text.Json.Serialization;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Application.Sys.Services;

namespace AidlyErp.Application.Bootstrap;

/// <summary>
/// Everything the shell needs on first paint, in one round trip: the company logo inlined as a
/// data URL (so no second request just to render the header) and the base currency settings that
/// drive money formatting across every screen.
/// </summary>
public class AppBootstrapResponse
{
    [JsonPropertyName("company_logo_data_url")]
    public string? CompanyLogoDataUrl { get; set; }

    [JsonPropertyName("base_currency_settings")]
    public BaseCurrencySettingsDto? BaseCurrencySettings { get; set; }
}

public interface IAppBootstrapService
{
    Task<AppBootstrapResponse> GetBootstrapAsync(CancellationToken cancellationToken = default);
}

/// <summary>Port of the Java <c>core.bootstrap.AppBootstrapService</c>.</summary>
public class AppBootstrapService : IAppBootstrapService
{
    /// <summary>Entity-type 1 in <c>sys_file</c> is the company logo.</summary>
    private const short CompanyLogoEntityType = 1;

    private readonly ISys1004Service _currencyService;
    private readonly ISys1203Service _fileService;

    public AppBootstrapService(ISys1004Service currencyService, ISys1203Service fileService)
    {
        _currencyService = currencyService;
        _fileService = fileService;
    }

    public async Task<AppBootstrapResponse> GetBootstrapAsync(CancellationToken cancellationToken = default) => new()
    {
        CompanyLogoDataUrl = await LoadCompanyLogoDataUrlAsync(cancellationToken),
        BaseCurrencySettings = await _currencyService.GetBaseSettingsAsync(cancellationToken)
    };

    /// <summary>
    /// Prefers the primary logo, falling back to the first file of that type. Returns
    /// <c>null</c> — not an error — when the company has no logo, so bootstrap never fails
    /// just because branding hasn't been set up.
    /// </summary>
    private async Task<string?> LoadCompanyLogoDataUrlAsync(CancellationToken ct)
    {
        var files = await _fileService.GetFilesAsync(CompanyLogoEntityType, null, ct);

        var file = files.FirstOrDefault(f => f.IsPrimary == 1) ?? files.FirstOrDefault();
        if (file == null) return null;

        var content = await _fileService.GetContentAsync(file.FileNo, ct);

        var type = content.ContentType ?? "application/octet-stream";
        return $"data:{type};base64,{Convert.ToBase64String(content.Bytes)}";
    }
}
