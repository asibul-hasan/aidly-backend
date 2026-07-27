using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Domain.Common;

namespace AidlyErp.Domain.Sys;

[Table("sys_user_branch")]
public class UserBranch : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("user_branch_no")]
    public long UserBranchNo { get; set; }

    [Column("user_no")]
    public long UserNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("role_no")]
    public long RoleNo { get; set; }

    [Column("is_default")]
    public short IsDefault { get; set; } = 0;
}
