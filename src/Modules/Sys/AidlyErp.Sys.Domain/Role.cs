using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sys.Domain;

[Table("sys_role")]
public class Role : AuditEntity, ICompanyScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("role_no")]
    public long RoleNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("role_id")]
    [StringLength(20)]
    public string RoleId { get; set; } = string.Empty;

    [Column("role_name")]
    [StringLength(150)]
    public string RoleName { get; set; } = string.Empty;

    [Column("role_desc")]
    [StringLength(500)]
    public string? RoleDesc { get; set; }

    [Column("is_system_role")]
    public short IsSystemRole { get; set; } = 0;

    protected override void NullifyBusinessId()
    {
        RoleId = "~" + (RoleNo != 0 ? RoleNo : DateTime.UtcNow.Ticks);
    }
}
