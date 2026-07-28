using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Fin.Domain;

[Table("fin_voucher")]
public class FinVoucher : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("voucher_no")]
    public long VoucherNo { get; set; }

    [Column("voucher_id")]
    [StringLength(30)]
    public string VoucherId { get; set; } = string.Empty;

    [Column("voucher_type_no")]
    public long VoucherTypeNo { get; set; }

    [Column("voucher_date")]
    public DateTime VoucherDate { get; set; }

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long FinPeriodNo { get; set; }

    [Column("narration")]
    [StringLength(1000)]
    public string? Narration { get; set; }

    [Column("reference_no")]
    [StringLength(100)]
    public string? ReferenceNo { get; set; }

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("fx_rate")]
    public decimal FxRate { get; set; } = 1m;

    [Column("total_debit")]
    public decimal TotalDebit { get; set; } = 0m;

    [Column("total_credit")]
    public decimal TotalCredit { get; set; } = 0m;

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    [Column("source_module")]
    public short? SourceModule { get; set; }

    [Column("source_doc_type")]
    [StringLength(40)]
    public string? SourceDocType { get; set; }

    [Column("source_doc_no")]
    public long? SourceDocNo { get; set; }

    [Column("reversal_of_no")]
    public long? ReversalOfNo { get; set; }

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("posted_by")]
    public long? PostedBy { get; set; }

    [Column("posted_at")]
    public DateTime? PostedAt { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }
}

[Table("fin_voucher_dtl")]
public class FinVoucherDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("voucher_dtl_no")]
    public long VoucherDtlNo { get; set; }

    [Column("voucher_no")]
    public long VoucherNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("account_no")]
    public long AccountNo { get; set; }

    [Column("dr_cr")]
    [StringLength(2)]
    public string DrCr { get; set; } = "dr";

    [Column("debit_fc")]
    public decimal DebitFc { get; set; } = 0m;

    [Column("credit_fc")]
    public decimal CreditFc { get; set; } = 0m;

    [Column("debit")]
    public decimal Debit { get; set; } = 0m;

    [Column("credit")]
    public decimal Credit { get; set; } = 0m;

    [Column("cost_center_no")]
    public long? CostCenterNo { get; set; }

    [Column("party_type")]
    public short? PartyType { get; set; } // 1=Customer 2=Supplier 3=Employee

    [Column("party_no")]
    public long? PartyNo { get; set; }

    [Column("against_voucher_no")]
    public long? AgainstVoucherNo { get; set; }

    [Column("line_narration")]
    [StringLength(500)]
    public string? LineNarration { get; set; }

    [NotMapped]
    public decimal Amount { get => Debit > 0 ? Debit : Credit; set { if (DrCr == "dr") Debit = value; else Credit = value; } }

    [NotMapped]
    public decimal BaseAmount { get => Debit > 0 ? Debit : Credit; set { if (DrCr == "dr") Debit = value; else Credit = value; } }

    [NotMapped]
    public string? Narration { get => LineNarration; set => LineNarration = value; }
}

[Table("fin_voucher_type")]
public class FinVoucherType : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("voucher_type_no")]
    public long VoucherTypeNo { get; set; }

    [Column("voucher_type_code")]
    [StringLength(20)]
    public string VoucherTypeCode { get; set; } = string.Empty;

    [Column("voucher_type_name")]
    [StringLength(100)]
    public string VoucherTypeName { get; set; } = string.Empty;

    [Column("base_kind")]
    public short BaseKind { get; set; } = 1;

    [Column("prefix")]
    [StringLength(10)]
    public string? Prefix { get; set; }

    [Column("requires_approval")]
    public short RequiresApproval { get; set; } = 0;

    [Column("is_auto_numbered")]
    public short IsAutoNumbered { get; set; } = 1;

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [NotMapped]
    public string TypeCode { get => VoucherTypeCode; set => VoucherTypeCode = value; }

    [NotMapped]
    public string TypeName { get => VoucherTypeName; set => VoucherTypeName = value; }
}

[Table("fin_ledger")]
public class FinLedger : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("ledger_no")]
    public long LedgerNo { get; set; }

    [Column("account_no")]
    public long AccountNo { get; set; }

    [Column("voucher_no")]
    public long VoucherNo { get; set; }

    [Column("voucher_dtl_no")]
    public long VoucherDtlNo { get; set; }

    [Column("voucher_date")]
    public DateTime VoucherDate { get; set; }

    [Column("dr_cr")]
    [StringLength(2)]
    public string DrCr { get; set; } = "dr";

    [Column("amount")]
    public decimal Amount { get; set; } = 0m;

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long FinPeriodNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }
}

[Table("fin_bank_account")]
public class FinBankAccount : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("bank_account_no")]
    public long BankAccountNo { get; set; }

    [Column("account_no")]
    public long AccountNo { get; set; }

    [Column("bank_name")]
    [StringLength(100)]
    public string BankName { get; set; } = string.Empty;

    [Column("branch_name")]
    [StringLength(100)]
    public string? BranchName { get; set; }

    [Column("account_number")]
    [StringLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Column("swift_code")]
    [StringLength(20)]
    public string? SwiftCode { get; set; }

    [Column("iban")]
    [StringLength(40)]
    public string? Iban { get; set; }

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }
}

[Table("fin_bank_recon")]
public class FinBankRecon : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("recon_no")]
    public long ReconNo { get; set; }

    [Column("account_no")]
    public long AccountNo { get; set; }

    [Column("statement_date")]
    public DateTime StatementDate { get; set; }

    [Column("statement_balance")]
    public decimal StatementBalance { get; set; }

    [Column("book_balance")]
    public decimal BookBalance { get; set; }

    [Column("cleared_debits")]
    public decimal ClearedDebits { get; set; }

    [Column("cleared_credits")]
    public decimal ClearedCredits { get; set; }

    [Column("reconciled_balance")]
    public decimal ReconciledBalance { get; set; }

    [Column("difference")]
    public decimal Difference { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Completed

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long BankAccountNo { get => AccountNo; set => AccountNo = value; }

    [NotMapped]
    public DateTime ReconDate { get => StatementDate; set => StatementDate = value; }
}

[Table("fin_bank_recon_line")]
public class FinBankReconLine : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("recon_line_no")]
    public long ReconLineNo { get; set; }

    [Column("recon_no")]
    public long ReconNo { get; set; }

    [Column("ledger_no")]
    public long LedgerNo { get; set; }

    [Column("voucher_dtl_no")]
    public long VoucherDtlNo { get; set; }

    [Column("cleared_date")]
    public DateTime? ClearedDate { get; set; }

    [Column("is_cleared")]
    public short IsCleared { get; set; } = 0;
}
