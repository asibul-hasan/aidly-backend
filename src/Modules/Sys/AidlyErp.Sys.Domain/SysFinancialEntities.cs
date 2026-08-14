using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sys.Domain;

[Table("sys_currency")]
public class Currency : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("currency_no")]
    public long CurrencyNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("currency_code")]
    [StringLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Column("currency_name")]
    [StringLength(100)]
    public string CurrencyName { get; set; } = string.Empty;

    [Column("currency_symbol")]
    [StringLength(10)]
    public string? CurrencySymbol { get; set; }

    [Column("fraction_name")]
    [StringLength(50)]
    public string? FractionName { get; set; }

    [Column("decimal_places")]
    public short DecimalPlaces { get; set; } = 2;

    [Column("number_system")]
    public short NumberSystem { get; set; } = 1;

    [Column("exchange_rate")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    [Column("is_base_currency")]
    public short IsBaseCurrency { get; set; } = 0;

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }
}

[Table("sys_exchange_rate")]
public class ExchangeRate : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("exchange_rate_no")]
    public long ExchangeRateNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    /// <summary>
    /// The quoted currency. Rates are held against the branch's base currency, so the Java
    /// schema stores a single <c>currency_no</c> rather than a from/to pair.
    /// </summary>
    [Column("currency_no")]
    public long CurrencyNo { get; set; }

    [Column("rate_date")]
    public DateOnly RateDate { get; set; }

    [Column("rate")]
    public decimal Rate { get; set; } = 1.0m;

    [NotMapped]
    public long RateNo { get => ExchangeRateNo; set => ExchangeRateNo = value; }
}

[Table("sys_fin_year")]
public class FinYear : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("fin_year_id")]
    [StringLength(20)]
    public string FinYearId { get; set; } = string.Empty;

    [Column("fin_year_name")]
    [StringLength(100)]
    public string FinYearName { get; set; } = string.Empty;

    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    [Column("is_closed")]
    public short IsClosed { get; set; } = 0;

    /// <summary><c>NULL</c> = company-wide (applies to all branches).</summary>
    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public string YearName { get => FinYearName; set => FinYearName = value; }

    [NotMapped]
    public short YearStatus { get => (short)(IsClosed == 1 ? 2 : 1); set => IsClosed = (short)(value == 2 ? 1 : 0); }
}

[Table("sys_fin_year_dtl")]
public class FinYearDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("fin_period_no")]
    public long FinPeriodNo { get; set; }

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_id")]
    [StringLength(20)]
    public string FinPeriodId { get; set; } = string.Empty;

    [Column("fin_period_name")]
    [StringLength(100)]
    public string FinPeriodName { get; set; } = string.Empty;

    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    /// <summary>1=Monthly, 2=Quarterly, 3=Half-Yearly, 4=Yearly, 5=Adjustment.</summary>
    [Column("period_type")]
    public short PeriodType { get; set; } = 1;

    /// <summary>1=Open, 2=Closed, 3=Locked.</summary>
    [Column("period_status")]
    public short PeriodStatus { get; set; } = 1;

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public long FinYearDtlNo { get => FinPeriodNo; set => FinPeriodNo = value; }

    [NotMapped]
    public short IsClosed
    {
        get => (short)(PeriodStatus == 2 ? 1 : 0);
        set => PeriodStatus = (short)(value == 1 ? 2 : 1);
    }

    /// <summary>
    /// Frees the <c>(fin_year_no, fin_period_id)</c> UNIQUE slot using a sentinel that fits the
    /// VARCHAR(20) column — a long PK is at most 19 digits, so "~" + PK is always ≤ 20 chars.
    /// </summary>
    protected override void NullifyBusinessId() =>
        FinPeriodId = "~" + (FinPeriodNo != 0 ? FinPeriodNo : DateTime.UtcNow.Ticks);
}

[Table("sys_vat_tax")]
public class VatTax : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("vat_tax_no")]
    public long VatTaxNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("tax_code")]
    [StringLength(20)]
    public string TaxCode { get; set; } = string.Empty;

    [Column("tax_name")]
    [StringLength(150)]
    public string TaxName { get; set; } = string.Empty;

    /// <summary>Numeric tax-type code, per the Java schema (the .NET model previously held a string).</summary>
    [Column("tax_type")]
    public short TaxType { get; set; }

    [Column("rate_percentage")]
    public decimal RatePercentage { get; set; }

    [Column("effective_from")]
    public DateOnly EffectiveFrom { get; set; }

    [Column("effective_to")]
    public DateOnly? EffectiveTo { get; set; }

    [Column("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [Column("authority_name")]
    [StringLength(150)]
    public string? AuthorityName { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    /// <summary>Alias — the schema column is <c>rate_percentage</c>.</summary>
    [NotMapped]
    public decimal TaxRate { get => RatePercentage; set => RatePercentage = value; }
}

[Table("sys_cost_center")]
public class CostCenter : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("cost_center_no")]
    public long CostCenterNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("cost_center_id")]
    [StringLength(20)]
    public string CostCenterId { get; set; } = string.Empty;

    [Column("cost_center_name")]
    [StringLength(150)]
    public string CostCenterName { get; set; } = string.Empty;

    /// <summary>Self-referencing hierarchy; <c>null</c> = a root cost centre.</summary>
    [Column("parent_cost_center_no")]
    public long? ParentCostCenterNo { get; set; }

    /// <summary>Alias — the schema column is <c>cost_center_id</c>.</summary>
    [NotMapped]
    public string CostCenterCode { get => CostCenterId; set => CostCenterId = value; }
}

[Table("sys_doc_sequence")]
public class DocSequence : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("doc_sequence_no")]
    public long DocSeqNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("doc_type")]
    [StringLength(50)]
    public string DocType { get; set; } = string.Empty;

    [Column("prefix")]
    [StringLength(20)]
    public string? Prefix { get; set; }

    [Column("suffix")]
    [StringLength(20)]
    public string Suffix { get; set; } = string.Empty;

    [Column("next_no")]
    public long NextVal { get; set; } = 1;

    /// <summary>Where the series began. Kept apart from <see cref="NextVal"/> so a period reset
    /// returns to a known number instead of guessing.</summary>
    [Column("starting_no")]
    public long StartingNo { get; set; } = 1;

    [Column("padding")]
    public short Padding { get; set; } = 6;

    /// <summary>1=Yearly 2=Never 3=Monthly — when the counter returns to <see cref="StartingNo"/>.</summary>
    [Column("reset_policy")]
    public short ResetPolicy { get; set; } = 2;

    /// <summary>Full template, e.g. <c>INV-{FY_YY_YY}-{SEQ:6}</c>. NULL keeps the older
    /// prefix + padding behaviour, so existing series are unaffected.</summary>
    [Column("pattern")]
    [StringLength(200)]
    public string? Pattern { get; set; }

    /// <summary>Separates series that share a doc_type — cash versus credit sales, say.</summary>
    [Column("doc_sub_type")]
    [StringLength(30)]
    public string DocSubType { get; set; } = string.Empty;

    /// <summary>The form this series belongs to, so SYS_1301 can list series by screen.</summary>
    [Column("menu_no")]
    public long? MenuNo { get; set; }

    [Column("fin_year_no")]
    public long? FinYearNo { get; set; }
}
