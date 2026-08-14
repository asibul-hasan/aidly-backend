using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sal.Domain;

/// <summary>
/// A physical register. Binds a till to the warehouse it sells from and to the GL account its
/// drawer cash lands in.
/// </summary>
[Table("sal_pos_terminal")]
public class SalPosTerminal : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("terminal_no")]
    public long TerminalNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    /// <summary>Stock source — a sale on this terminal relieves this warehouse.</summary>
    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("terminal_id")]
    [StringLength(30)]
    public string TerminalId { get; set; } = string.Empty;

    [Column("terminal_name")]
    [StringLength(100)]
    public string TerminalName { get; set; } = string.Empty;

    /// <summary>Bound device id, used by offline sync to tell two tills apart.</summary>
    [Column("device_uuid")]
    [StringLength(80)]
    public string? DeviceUuid { get; set; }

    [Column("receipt_prefix")]
    [StringLength(20)]
    public string? ReceiptPrefix { get; set; }

    /// <summary>Drawer cash account in <c>fin_account</c>.</summary>
    [Column("cash_gl_account_no")]
    public long? CashGlAccountNo { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }
}

/// <summary>
/// One cashier's drawer period on one terminal. Bounds a set of sales for cash reconciliation and
/// the Z-report. A partial unique index on the table enforces at most one open session per terminal.
/// </summary>
[Table("sal_pos_session")]
public class SalPosSession : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("session_no")]
    public long SessionNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("terminal_no")]
    public long TerminalNo { get; set; }

    [Column("session_id")]
    [StringLength(40)]
    public string SessionId { get; set; } = string.Empty;

    [Column("cashier_user_no")]
    public long CashierUserNo { get; set; }

    [Column("opened_at")]
    public DateTime OpenedAt { get; set; }

    /// <summary>Cash placed in the drawer at open; excluded from takings but part of the count.</summary>
    [Column("opening_float")]
    public decimal OpeningFloat { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("expected_cash")]
    public decimal? ExpectedCash { get; set; }

    [Column("expected_card")]
    public decimal? ExpectedCard { get; set; }

    [Column("expected_mobile")]
    public decimal? ExpectedMobile { get; set; }

    [Column("expected_other")]
    public decimal? ExpectedOther { get; set; }

    [Column("counted_cash")]
    public decimal? CountedCash { get; set; }

    /// <summary>counted − (opening_float + expected_cash). Negative means the drawer is short.</summary>
    [Column("cash_variance")]
    public decimal? CashVariance { get; set; }

    [Column("total_sales")]
    public decimal TotalSales { get; set; }

    [Column("total_returns")]
    public decimal TotalReturns { get; set; }

    [Column("invoice_count")]
    public int InvoiceCount { get; set; }

    /// <summary>1=Open 2=Closing 3=Closed.</summary>
    [Column("status")]
    public short Status { get; set; } = 1;

    [Column("variance_approved_by")]
    public long? VarianceApprovedBy { get; set; }

    [Column("variance_remarks")]
    public string? VarianceRemarks { get; set; }

    [Column("gl_voucher_no")]
    public long? GlVoucherNo { get; set; }
}

/// <summary>
/// One tender against a sale. A cash-plus-mobile split is two rows; the invoice's
/// <c>paid_amount</c> is their sum. This is what the session close groups to get expected totals.
/// </summary>
[Table("sal_invoice_payment")]
public class SalInvoicePayment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("invoice_payment_no")]
    public long InvoicePaymentNo { get; set; }

    [Column("invoice_no")]
    public long InvoiceNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    /// <summary>
    /// 1=Cash 2=Card 3=MobileBanking 4=BankTransfer 5=Cheque 6=Credit/Due 7=LoyaltyPoints
    /// 8=GiftCard 9=StoreCredit.
    /// </summary>
    [Column("payment_method")]
    public short PaymentMethod { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    /// <summary>Cash actually handed over; change = tendered − amount.</summary>
    [Column("tendered_amount")]
    public decimal? TenderedAmount { get; set; }

    [Column("card_last4")]
    [StringLength(4)]
    public string? CardLast4 { get; set; }

    [Column("card_type")]
    [StringLength(20)]
    public string? CardType { get; set; }

    [Column("approval_code")]
    [StringLength(40)]
    public string? ApprovalCode { get; set; }

    [Column("mobile_provider")]
    [StringLength(30)]
    public string? MobileProvider { get; set; }

    [Column("txn_ref")]
    [StringLength(80)]
    public string? TxnRef { get; set; }

    [Column("bank_no")]
    public long? BankNo { get; set; }

    [Column("cheque_no")]
    [StringLength(40)]
    public string? ChequeNo { get; set; }

    [Column("cheque_date")]
    public DateTime? ChequeDate { get; set; }

    [Column("points_redeemed")]
    public decimal? PointsRedeemed { get; set; }

    /// <summary>Resolved cash / bank / card-clearing account for the GL leg.</summary>
    [Column("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [Column("row_version")]
    public long RowVersion { get; set; } = 1;
}
