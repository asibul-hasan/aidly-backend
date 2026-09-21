using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sys.Domain;

[Table("sys_menu")]
public class Menu : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("menu_no")]
    public long MenuNo { get; set; }

    /// <summary>
    /// The form this menu entry points at (e.g. <c>SYS_1201</c>). This is the RBAC key — every
    /// permission grant and the URL→form resolution are keyed on it.
    /// </summary>
    [Column("form_id")]
    [StringLength(50)]
    public string? FormId { get; set; }

    [Column("form_name")]
    [StringLength(100)]
    public string? FormName { get; set; }

    [Column("menu_desc")]
    public string? MenuDesc { get; set; }

    /// <summary>Lower-cased to <c>'form'</c> when absent, per the menu-resolution queries.</summary>
    [Column("menu_type")]
    [StringLength(30)]
    public string? MenuType { get; set; }

    /// <summary>
    /// Route this menu maps to. RBAC resolves an incoming request URL to a form id by
    /// longest-prefix match on this column, so it must stay populated.
    /// </summary>
    [Column("route_path")]
    [StringLength(200)]
    public string? RoutePath { get; set; }

    [Column("icon_name")]
    [StringLength(50)]
    public string? IconName { get; set; }

    [Column("order_sl")]
    public int? OrderSl { get; set; }

    /// <summary>
    /// Owning submodule. The module is reached through it — the Java schema deliberately does not
    /// store <c>module_no</c> on the menu row.
    /// </summary>
    [Column("submodule_no")]
    public long? SubmoduleNo { get; set; }

    // --- Aliases for callers using the older .NET names ---

    [NotMapped]
    public string MenuId { get => FormId; set => FormId = value; }

    [NotMapped]
    public string MenuName { get => FormName; set => FormName = value; }

    [NotMapped]
    public string? Icon { get => IconName; set => IconName = value; }

    [NotMapped]
    public int DisplayOrder { get => OrderSl ?? 0; set => OrderSl = value; }
}

[Table("sys_enroll_menu")]
/// <summary>
/// A company's subscription to a menu/form (<c>sys_enroll_menu</c>) — "what the client
/// purchased". This is the licensing gate the RBAC layer joins against: a permission grant only
/// takes effect while a matching, in-window enrolment exists.
///
/// <para>Rows with <c>branch_no IS NULL</c> apply company-wide; a branch-specific row overrides.</para>
/// </summary>
public class EnrollMenu : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("enroll_menu_no")]
    public long EnrollMenuNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("menu_no")]
    public long MenuNo { get; set; }

    /// <summary>1 = perpetual; the start/end dates are then ignored (and stored as null).</summary>
    [Column("is_lifetime")]
    public short IsLifetime { get; set; } = 0;

    [Column("enroll_start_date")]
    public DateOnly? EnrollStartDate { get; set; }

    [Column("enroll_end_date")]
    public DateOnly? EnrollEndDate { get; set; }

    /// <summary>Alias — the schema column is <c>enroll_menu_no</c>.</summary>
    [NotMapped]
    public long EnrollNo { get => EnrollMenuNo; set => EnrollMenuNo = value; }
}

[Table("sys_module")]
public class SysModule : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("module_no")]
    public long ModuleNo { get; set; }

    [Column("module_code")]
    [StringLength(50)]
    public string ModuleCode { get; set; } = string.Empty;

    [Column("module_name")]
    [StringLength(100)]
    public string ModuleName { get; set; } = string.Empty;

    [Column("module_desc")]
    public string? ModuleDesc { get; set; }

    [Column("module_icon")]
    [StringLength(50)]
    public string? ModuleIcon { get; set; }

    [Column("module_route")]
    [StringLength(200)]
    public string? ModuleRoute { get; set; }

    /// <summary>Display order. Menus, submodules and modules are all sorted NULLS LAST on this.</summary>
    [Column("order_sl")]
    public int? OrderSl { get; set; }

    /// <summary>Alias — the schema column is <c>module_code</c>.</summary>
    [NotMapped]
    public string ModuleId { get => ModuleCode; set => ModuleCode = value; }
}

[Table("sys_submodule")]
public class SysSubmodule : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("submodule_no")]
    public long SubmoduleNo { get; set; }

    [Column("module_no")]
    public long ModuleNo { get; set; }

    [Column("submodule_code")]
    [StringLength(50)]
    public string SubmoduleCode { get; set; } = string.Empty;

    [Column("submodule_name")]
    [StringLength(100)]
    public string SubmoduleName { get; set; } = string.Empty;

    [Column("submodule_icon")]
    [StringLength(50)]
    public string? SubmoduleIcon { get; set; }

    [Column("submodule_route")]
    [StringLength(200)]
    public string? SubmoduleRoute { get; set; }

    [Column("order_sl")]
    public int? OrderSl { get; set; }

    /// <summary>Alias — the schema column is <c>submodule_code</c>.</summary>
    [NotMapped]
    public string SubmoduleId { get => SubmoduleCode; set => SubmoduleCode = value; }
}

[Table("sys_file")]
public class SysFile : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("file_no")]
    public long FileNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    /// <summary>What the file is attached to: 1=Company Logo, 2=Employee Photo, 3=Employee
    /// Signature, 4=User Avatar, 5=Product Image, 6=Branch Logo, 7=Document, 8=Other.</summary>
    [Column("entity_type")]
    public short EntityType { get; set; }

    /// <summary>Primary key of the attached record (e.g. the employee_no).</summary>
    [Column("entity_no")]
    public long? EntityNo { get; set; }

    [Column("file_name")]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Column("content_type")]
    [StringLength(100)]
    public string? ContentType { get; set; }

    [Column("file_extension")]
    [StringLength(10)]
    public string? FileExtension { get; set; }

    [Column("file_size")]
    public long? FileSize { get; set; }

    /// <summary>1 = stored in the database as BYTEA (the only mode currently used).</summary>
    [Column("storage_type")]
    public short StorageType { get; set; } = 1;

    [Column("file_bytes")]
    public byte[]? FileBytes { get; set; }

    [Column("external_url")]
    [StringLength(500)]
    public string? ExternalUrl { get; set; }

    [Column("checksum_sha256")]
    [StringLength(64)]
    public string? ChecksumSha256 { get; set; }

    [Column("width_px")]
    public int? WidthPx { get; set; }

    [Column("height_px")]
    public int? HeightPx { get; set; }

    /// <summary>At most one primary file per (company, entity_type, entity_no) slot.</summary>
    [Column("is_primary")]
    public short? IsPrimary { get; set; } = 1;
}

[Table("sys_setting")]
public class Setting : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("setting_no")]
    public long SettingNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("setting_key")]
    [StringLength(80)]
    public string SettingKey { get; set; } = string.Empty;

    [Column("setting_value")]
    [StringLength(500)]
    public string? SettingValue { get; set; }

    /// <summary>1=String, 2=Number, 3=Boolean, 4=JSON, 5=Date.</summary>
    [Column("value_type")]
    public short ValueType { get; set; } = 1;

    [Column("setting_group")]
    [StringLength(40)]
    public string? SettingGroup { get; set; }

    [Column("description")]
    [StringLength(250)]
    public string? Description { get; set; }
}

