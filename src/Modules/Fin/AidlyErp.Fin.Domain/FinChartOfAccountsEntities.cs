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

    [Column("parent_account_no")]
    public long? ParentAccountNo { get; set; }

    [Column("account_group_no")]
    public long? AccountGroupNo { get; set; }

    /// <summary>
    /// Legacy compatibility property mapping to ParentAccountNo / AccountGroupNo.
    /// </summary>
    [NotMapped]
    public long? ParentGroupNo
    {
        get => ParentAccountNo ?? AccountGroupNo;
        set
        {
            ParentAccountNo = value;
            AccountGroupNo = value;
        }
    }

    [Column("root_type")]
    public short RootType { get; set; }

    [Column("normal_balance")]
    [StringLength(2)]
    public string NormalBalance { get; set; } = "dr";

    [Column("is_group")]
    public short IsGroup { get; set; } = 0;

    [Column("is_postable")]
    public short IsPostable { get; set; } = 1;

    [Column("is_control")]
    public short IsControl { get; set; } = 0;

    [Column("control_type")]
    public short? ControlType { get; set; }

    [Column("account_category")]
    public short? AccountCategory { get; set; }

    [Column("sub_category")]
    public short? SubCategory { get; set; }

    [Column("account_class")]
    public short? AccountClass { get; set; }

    [Column("requires_cost_center")]
    public short RequiresCostCenter { get; set; } = 0;

    [Column("requires_party")]
    public short RequiresParty { get; set; } = 0;

    [Column("requires_reconciliation")]
    public short RequiresReconciliation { get; set; } = 0;

    [Column("requires_branch")]
    public short RequiresBranch { get; set; } = 0;

    [Column("requires_project")]
    public short RequiresProject { get; set; } = 0;

    [Column("requires_department")]
    public short RequiresDepartment { get; set; } = 0;

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("opening_balance")]
    public decimal OpeningBalance { get; set; } = 0m;

    [Column("opening_dr_cr")]
    [StringLength(2)]
    public string OpeningDrCr { get; set; } = "dr";

    [Column("order_sl")]
    public int DisplayOrder { get; set; } = 0;

    [Column("description")]
    public string? Description { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
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

    // The schema splits balances into opening and period pairs, as the Java entity does. The port
    // collapsed them into debit_amount/credit_amount/closing_balance, none of which exist.
    [Column("opening_debit")]
    public decimal OpeningDebit { get; set; } = 0m;

    [Column("opening_credit")]
    public decimal OpeningCredit { get; set; } = 0m;

    [Column("period_debit")]
    public decimal DebitAmount { get; set; } = 0m;

    [Column("period_credit")]
    public decimal CreditAmount { get; set; } = 0m;

    /// <summary>Derived: opening plus period movement. Not stored.</summary>
    [NotMapped]
    public decimal ClosingBalance
    {
        get => OpeningDebit - OpeningCredit + DebitAmount - CreditAmount;
        set { }
    }

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
