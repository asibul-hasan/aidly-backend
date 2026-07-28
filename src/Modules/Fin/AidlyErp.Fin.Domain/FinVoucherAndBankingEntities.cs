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

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
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

    /// <summary>Derived from which of Debit/Credit carries the value — there is no dr_cr column.</summary>
    [NotMapped]
    public string DrCr
    {
        get => Credit > 0m && Debit == 0m ? "cr" : "dr";
        set
        {
            if (string.Equals(value, "cr", StringComparison.OrdinalIgnoreCase)) { Credit = Debit > 0m ? Debit : Credit; Debit = 0m; }
            else { Debit = Credit > 0m ? Credit : Debit; Credit = 0m; }
        }
    }

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

    [Column("voucher_type_id")]
    [StringLength(20)]
    public string VoucherTypeCode { get; set; } = string.Empty;

    [Column("type_name")]
    [StringLength(100)]
    public string VoucherTypeName { get; set; } = string.Empty;

    [Column("base_kind")]
    public short BaseKind { get; set; } = 1;

    [Column("number_prefix")]
    [StringLength(10)]
    public string? Prefix { get; set; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public short RequiresApproval { get; set; } = 0;

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
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

    // fin_ledger stores signed debit/credit columns, exactly as the Java entity does. There is no
    // dr_cr/amount pair in the schema; the .NET port invented one, which made every ledger read
    // and write fail. DrCr/Amount are kept as a derived view so posting code is unchanged.
    [Column("debit")]
    public decimal Debit { get; set; } = 0m;

    [Column("credit")]
    public decimal Credit { get; set; } = 0m;

    private decimal _amount;
    private bool _isCredit;

    /// <summary>"dr" or "cr". Reading derives from which column carries the value.</summary>
    [NotMapped]
    public string DrCr
    {
        get => Credit > 0m && Debit == 0m ? "cr" : "dr";
        set
        {
            _isCredit = string.Equals(value, "cr", StringComparison.OrdinalIgnoreCase);
            ApplyDirection();
        }
    }

    /// <summary>Unsigned magnitude; writes land in Debit or Credit according to <see cref="DrCr"/>.</summary>
    [NotMapped]
    public decimal Amount
    {
        get => Debit > 0m ? Debit : Credit;
        set
        {
            _amount = value;
            ApplyDirection();
        }
    }

    /// <summary>Re-applies the pair so DrCr and Amount may be assigned in either order.</summary>
    private void ApplyDirection()
    {
        if (_isCredit) { Credit = _amount; Debit = 0m; }
        else { Debit = _amount; Credit = 0m; }
    }

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

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
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
    [Column("bank_recon_no")]
    public long ReconNo { get; set; }

    [Column("account_no")]
    public long AccountNo { get; set; }

    [Column("statement_date")]
    public DateTime StatementDate { get; set; }

    [Column("statement_balance")]
    public decimal StatementBalance { get; set; }

    [Column("book_balance")]
    public decimal BookBalance { get; set; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public decimal ClearedDebits { get; set; }

    /// <summary>Not a column in this schema — kept so callers and DTOs are unaffected, but never persisted.</summary>
    [NotMapped]
    public decimal ClearedCredits { get; set; }

    [Column("cleared_balance")]
    public decimal ReconciledBalance { get; set; }

    [Column("difference")]
    public decimal Difference { get; set; }

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Completed

    /// <summary>Java calls this column <c>narration</c>; there is no <c>remarks</c> on this table.</summary>
    [Column("narration")]
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
    [Column("bank_recon_line_no")]
    public long ReconLineNo { get; set; }

    [Column("bank_recon_no")]
    public long ReconNo { get; set; }

    [Column("ledger_no")]
    public long LedgerNo { get; set; }

    /// <summary>Not a column — the line points at the ledger entry, not a voucher line.</summary>
    [NotMapped]
    public long VoucherDtlNo { get; set; }

    [Column("bank_date")]
    public DateTime? ClearedDate { get; set; }

    /// <summary>Derived: a line is cleared once the bank date is set. There is no is_cleared column.</summary>
    [NotMapped]
    public short IsCleared
    {
        get => (short)(ClearedDate != null ? 1 : 0);
        set => ClearedDate = value == 1 ? ClearedDate ?? DateTime.UtcNow : null;
    }
}
