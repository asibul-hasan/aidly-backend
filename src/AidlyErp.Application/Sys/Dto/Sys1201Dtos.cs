using System.Text.Json.Serialization;

namespace AidlyErp.Application.Sys.Dto;

/// <summary>A top-level module in the SYS1201 menu builder.</summary>
public class Sys1201ModuleDto
{
    [JsonPropertyName("module_no")]
    public long? ModuleNo { get; set; }

    [JsonPropertyName("module_code")]
    public string? ModuleCode { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }

    [JsonPropertyName("module_desc")]
    public string? ModuleDesc { get; set; }

    [JsonPropertyName("module_icon")]
    public string? ModuleIcon { get; set; }

    [JsonPropertyName("module_route")]
    public string? ModuleRoute { get; set; }

    [JsonPropertyName("order_sl")]
    public int? OrderSl { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

/// <summary>A submodule grouping menus under a module.</summary>
public class Sys1201SubmoduleDto
{
    [JsonPropertyName("submodule_no")]
    public long? SubmoduleNo { get; set; }

    [JsonPropertyName("module_no")]
    public long? ModuleNo { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }

    [JsonPropertyName("submodule_code")]
    public string? SubmoduleCode { get; set; }

    [JsonPropertyName("submodule_name")]
    public string? SubmoduleName { get; set; }

    [JsonPropertyName("submodule_icon")]
    public string? SubmoduleIcon { get; set; }

    [JsonPropertyName("submodule_route")]
    public string? SubmoduleRoute { get; set; }

    [JsonPropertyName("order_sl")]
    public int? OrderSl { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

/// <summary>A menu entry (a form) under a submodule.</summary>
public class Sys1201MenuDto
{
    [JsonPropertyName("menu_no")]
    public long? MenuNo { get; set; }

    [JsonPropertyName("submodule_no")]
    public long? SubmoduleNo { get; set; }

    [JsonPropertyName("submodule_name")]
    public string? SubmoduleName { get; set; }

    [JsonPropertyName("module_name")]
    public string? ModuleName { get; set; }

    [JsonPropertyName("form_id")]
    public string? FormId { get; set; }

    [JsonPropertyName("form_name")]
    public string? FormName { get; set; }

    [JsonPropertyName("menu_desc")]
    public string? MenuDesc { get; set; }

    [JsonPropertyName("menu_type")]
    public string? MenuType { get; set; }

    [JsonPropertyName("route_path")]
    public string? RoutePath { get; set; }

    [JsonPropertyName("icon_name")]
    public string? IconName { get; set; }

    [JsonPropertyName("order_sl")]
    public int? OrderSl { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}
