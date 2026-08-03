using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sys.Domain;

[Table("sys_branch")]
public class Branch : AuditEntity, IBranchScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("branch_id")]
    [StringLength(15)]
    public string BranchId { get; set; } = string.Empty;

    [Column("branch_name")]
    [StringLength(250)]
    public string BranchName { get; set; } = string.Empty;

    [Column("branch_name_nls")]
    [StringLength(250)]
    public string? BranchNameNls { get; set; }

    [Column("branch_type")]
    [StringLength(50)]
    public string? BranchType { get; set; }

    [Column("branch_addr1")]
    [StringLength(250)]
    public string? BranchAddr1 { get; set; }

    [Column("branch_addr2")]
    [StringLength(250)]
    public string? BranchAddr2 { get; set; }

    [Column("city")]
    [StringLength(100)]
    public string? City { get; set; }

    [Column("post_code")]
    [StringLength(20)]
    public string? PostCode { get; set; }

    [Column("mobile_no")]
    [StringLength(20)]
    public string? MobileNo { get; set; }

    [Column("contact_no")]
    [StringLength(25)]
    public string? ContactNo { get; set; }

    [Column("email")]
    [StringLength(250)]
    public string? Email { get; set; }

    [Column("manager_employee_no")]
    public long? ManagerEmployeeNo { get; set; }

    [Column("is_main_branch")]
    public short IsMainBranch { get; set; } = 0;

    long? IMultiTenantEntity.BranchNo
    {
        get => BranchNo;
        set => BranchNo = value ?? 0;
    }

    protected override void NullifyBusinessId()
    {
        BranchId = "~" + (BranchNo != 0 ? BranchNo : DateTime.UtcNow.Ticks);
    }
}
