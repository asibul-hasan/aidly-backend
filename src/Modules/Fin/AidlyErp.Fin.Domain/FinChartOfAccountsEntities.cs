using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Fin.Domain;

[Table("fin_account")]
public class FinAccount : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("account_no")]
    public long AccountNo { get; set; }

    [Column("account_code")]
    [StringLength(30)]
    public string AccountCode { get; set; } = string.Empty;

    [Column("account_name")]
    [StringLength(200)]
    public string AccountName { get; set; } = string.Empty;

    [Column("account_group_no")]
    public long AccountGroupNo { get; set; }

    [Column("root_type")]
    public short RootType { get; set; }

    [Column("normal_balance")]
    [StringLength(2)]
    public string NormalBalance { get; set; } = "dr";

    [Column("is_postable")]
    public short IsPostable { get; set; } = 1;

    [Column("control_type")]
    public short? ControlType { get; set; }

    [Column("requires_cost_center")]
    public short RequiresCostCenter { get; set; } = 0;

    [Column("requires_party")]
    public short RequiresParty { get; set; } = 0;

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("opening_balance")]
    public decimal OpeningBalance { get; set; } = 0m;

    [Column("opening_dr_cr")]
    [StringLength(2)]
    public string OpeningDrCr { get; set; } = "dr";

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

[Table("fin_account_group")]
public class FinAccountGroup : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("account_group_no")]
    public long AccountGroupNo { get; set; }

    [Column("group_code")]
    [StringLength(30)]
    public string GroupCode { get; set; } = string.Empty;

    [Column("group_name")]
    [StringLength(200)]
    public string GroupName { get; set; } = string.Empty;

    [Column("parent_group_no")]
    public long? ParentGroupNo { get; set; }

    [Column("root_type")]
    public short RootType { get; set; }

    [Column("normal_balance")]
    [StringLength(2)]
    public string? NormalBalance { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 0;

    [Column("company_no")]
    public long CompanyNo { get; set; }
}

[Table("fin_account_balance")]
public class FinAccountBalance : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("balance_no")]
    public long BalanceNo { get; set; }

    [Column("account_no")]
    public long AccountNo { get; set; }

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long FinPeriodNo { get; set; }

    [Column("debit_amount")]
    public decimal DebitAmount { get; set; } = 0m;

    [Column("credit_amount")]
    public decimal CreditAmount { get; set; } = 0m;

    [Column("closing_balance")]
    public decimal ClosingBalance { get; set; } = 0m;

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }
}

[Table("fin_gl_map")]
public class FinGlMap : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("gl_map_no")]
    public long GlMapNo { get; set; }

    [Column("event_type")]
    [StringLength(60)]
    public string EventType { get; set; } = string.Empty;

    [Column("leg_key")]
    [StringLength(60)]
    public string LegKey { get; set; } = string.Empty;

    [Column("sub_key")]
    [StringLength(60)]
    public string? SubKey { get; set; }

    [Column("account_no")]
    public long AccountNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [NotMapped]
    public long MapNo { get => GlMapNo; set => GlMapNo = value; }
}
