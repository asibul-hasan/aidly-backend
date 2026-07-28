using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sys.Domain;

[Table("sys_role_permission")]
public class RolePermission : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("role_permission_no")]
    public long RolePermissionNo { get; set; }

    [Column("role_no")]
    public long RoleNo { get; set; }

    [Column("menu_no")]
    public long MenuNo { get; set; }

    [Column("can_view")]
    public short CanView { get; set; } = 0;

    [Column("can_insert")]
    public short CanInsert { get; set; } = 0;

    [Column("can_update")]
    public short CanUpdate { get; set; } = 0;

    [Column("can_delete")]
    public short CanDelete { get; set; } = 0;

    [Column("can_approve")]
    public short CanApprove { get; set; } = 0;

    [Column("can_post")]
    public short CanPost { get; set; } = 0;

    [Column("can_cancel")]
    public short CanCancel { get; set; } = 0;

    [Column("can_export")]
    public short CanExport { get; set; } = 0;

    [Column("perm_scope")]
    public short PermScope { get; set; } = 2;

    [Column("record_filter")]
    public short RecordFilter { get; set; } = 1;
}
