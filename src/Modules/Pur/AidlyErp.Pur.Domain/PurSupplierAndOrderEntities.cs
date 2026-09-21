using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Pur.Domain;

[Table("pur_supplier")]
public class PurSupplier : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    /// <summary>The business key is <c>supplier_id</c> in the schema, not <c>supplier_code</c>.</summary>
    [Column("supplier_id")]
    [StringLength(30)]
    public string SupplierId { get; set; } = string.Empty;

    [Column("supplier_name")]
    [StringLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    [Column("supplier_name_nls")]
    [StringLength(200)]
    public string? SupplierNameNls { get; set; }

    [Column("supplier_type")]
    public short? SupplierType { get; set; }

    [Column("contact_person")]
    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [Column("mobile_no")]
    [StringLength(20)]
    public string? MobileNo { get; set; }

    [Column("alt_mobile_no")]
    [StringLength(20)]
    public string? AltMobileNo { get; set; }

    [Column("phone_no")]
    [StringLength(25)]
    public string? PhoneNo { get; set; }

    [Column("email")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Column("website")]
    [StringLength(150)]
    public string? Website { get; set; }

    [Column("address_line1")]
    [StringLength(250)]
    public string? AddressLine1 { get; set; }

    [Column("address_line2")]
    [StringLength(250)]
    public string? AddressLine2 { get; set; }

    [Column("city")]
    [StringLength(100)]
    public string? City { get; set; }

    [Column("state_province")]
    [StringLength(100)]
    public string? StateProvince { get; set; }

    [Column("post_code")]
    [StringLength(20)]
    public string? PostCode { get; set; }

    [Column("country_code")]
    [StringLength(10)]
    public string? CountryCode { get; set; }

    [Column("vat_reg_no")]
    [StringLength(50)]
    public string? VatRegNo { get; set; }

    [Column("tin_no")]
    [StringLength(50)]
    public string? TinNo { get; set; }

    [Column("bin_no")]
    [StringLength(50)]
    public string? BinNo { get; set; }

    [Column("trade_license_no")]
    [StringLength(100)]
    public string? TradeLicenseNo { get; set; }

    /// <summary>1=Cash 2=Credit 3=Advance 4=Consignment.</summary>
    [Column("payment_terms")]
    public short PaymentTerms { get; set; } = 1;

    [Column("credit_days")]
    public int CreditDays { get; set; } = 0;

    [Column("credit_limit")]
    public decimal CreditLimit { get; set; } = 0m;

    [Column("default_currency_no")]
    public long? DefaultCurrencyNo { get; set; }

    [Column("default_vat_tax_no")]
    public long? DefaultVatTaxNo { get; set; }

    [Column("opening_balance")]
    public decimal OpeningBalance { get; set; } = 0m;

    [Column("opening_balance_date")]
    public DateTime? OpeningBalanceDate { get; set; }

    [Column("current_payable")]
    public decimal CurrentPayable { get; set; } = 0m;

    [Column("bank_name")]
    [StringLength(100)]
    public string? BankName { get; set; }

    [Column("bank_account_no")]
    [StringLength(40)]
    public string? BankAccountNo { get; set; }

    [Column("bank_branch")]
    [StringLength(100)]
    public string? BankBranch { get; set; }

    [Column("routing_no")]
    [StringLength(30)]
    public string? RoutingNo { get; set; }

    [Column("mfs_provider")]
    [StringLength(30)]
    public string? MfsProvider { get; set; }

    [Column("mfs_number")]
    [StringLength(20)]
    public string? MfsNumber { get; set; }

    [Column("lead_time_days")]
    public int? LeadTimeDays { get; set; }

    [Column("rating")]
    public short? Rating { get; set; }

    [Column("image_path")]
    [StringLength(255)]
    public string? ImagePath { get; set; }

    [Column("remarks")]
    [StringLength(500)]
    public string? Remarks { get; set; }

    [NotMapped]
    public string? Mobile { get => MobileNo; set => MobileNo = value; }

    [NotMapped]
    public string? Phone { get => PhoneNo; set => PhoneNo = value; }

    [NotMapped]
    public decimal CurrentBalance { get => CurrentPayable; set => CurrentPayable = value; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

[Table("pur_supplier_product")]
public class PurSupplierProduct : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("supplier_product_no")]
    public long SupplierProductNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    [Column("supplier_sku")]
    [StringLength(60)]
    public string? SupplierSku { get; set; }

    [Column("last_price")]
    public decimal LastPrice { get; set; } = 0m;

    [Column("discount_pct")]
    public decimal DiscountPct { get; set; } = 0m;

    [Column("moq")]
    public decimal Moq { get; set; } = 0m;

    [Column("lead_time_days")]
    public int? LeadTimeDays { get; set; }

    [Column("is_preferred")]
    public short IsPreferred { get; set; } = 0;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long SupProdNo { get => SupplierProductNo; set => SupplierProductNo = value; }

    [NotMapped]
    public string? SupplierProductCode { get => SupplierSku; set => SupplierSku = value; }

    [NotMapped]
    public decimal? LastPurchasePrice { get => LastPrice; set => LastPrice = value ?? 0m; }
}

[Table("pur_supplier_ledger")]
public class PurSupplierLedger : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("supplier_ledger_no")]
    public long SupplierLedgerNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("txn_date")]
    public DateTime TxnDate { get; set; }

    /// <summary>1=Opening 2=Invoice 3=Payment 4=Return/DebitNote 5=Adjustment.</summary>
    [Column("ref_doc_type")]
    public short RefDocType { get; set; }

    [Column("ref_doc_no")]
    [StringLength(40)]
    public string RefDocNo { get; set; } = string.Empty;

    [Column("ref_doc_pk")]
    public long? RefDocPk { get; set; }

    [Column("credit")]
    public decimal Credit { get; set; } = 0m;

    [Column("debit")]
    public decimal Debit { get; set; } = 0m;

    [Column("balance_after")]
    public decimal BalanceAfter { get; set; } = 0m;

    [Column("fin_year_no")]
    public long? FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long LedgerNo { get => SupplierLedgerNo; set => SupplierLedgerNo = value; }

    [NotMapped]
    public DateTime TransDate { get => TxnDate; set => TxnDate = value; }
}

[Table("pur_order")]
public class PurOrder : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("order_no")]
    public long OrderNo { get; set; }

    [Column("order_id")]
    [StringLength(40)]
    public string OrderId { get; set; } = string.Empty;

    [Column("order_date")]
    public DateTime OrderDate { get; set; }

    [Column("supplier_no")]
    public long SupplierNo { get; set; }

    [Column("warehouse_no")]
    public long WarehouseNo { get; set; }

    [Column("expected_date")]
    public DateTime? ExpectedDate { get; set; }

    [Column("currency_no")]
    public long? CurrencyNo { get; set; }

    [Column("exchange_rate")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Submitted 3=Approved 4=Cancelled

    [Column("sub_total")]
    public decimal SubTotal { get; set; } = 0m;

    [Column("discount_total")]
    public decimal DiscountTotal { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("shipping_estimate")]
    public decimal ShippingEstimate { get; set; } = 0m;

    [Column("grand_total")]
    public decimal GrandTotal { get; set; } = 0m;

    [Column("received_value")]
    public decimal ReceivedValue { get; set; } = 0m;

    [Column("fin_year_no")]
    public long FinYearNo { get; set; }

    [Column("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("submitted_by")]
    public long? SubmittedBy { get; set; }

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("terms_note")]
    public string? TermsNote { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long BranchNo { get; set; }

    [NotMapped]
    public DateTime? ExpectedDeliveryDate { get => ExpectedDate; set => ExpectedDate = value; }

    [NotMapped]
    public decimal TotalAmount { get => SubTotal; set => SubTotal = value; }
}

[Table("pur_order_dtl")]
public class PurOrderDtl : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("order_dtl_no")]
    public long OrderDtlNo { get; set; }

    [Column("order_no")]
    public long OrderNo { get; set; }

    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    [Column("order_qty")]
    public decimal OrderQty { get; set; } = 0m;

    [Column("order_qty_base")]
    public decimal OrderQtyBase { get; set; } = 0m;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; } = 0m;

    [Column("discount_pct")]
    public decimal DiscountPct { get; set; } = 0m;

    [Column("discount_amount")]
    public decimal DiscountAmount { get; set; } = 0m;

    [Column("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [Column("tax_rate_pct")]
    public decimal TaxRatePct { get; set; } = 0m;

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; } = 0m;

    [Column("line_total")]
    public decimal LineTotal { get; set; } = 0m;

    [Column("received_qty_base")]
    public decimal ReceivedQtyBase { get; set; } = 0m;

    [Column("remarks")]
    [StringLength(250)]
    public string? Remarks { get; set; }

    [NotMapped]
    public long ItemNo { get => ProductNo; set => ProductNo = value; }

    [NotMapped]
    public decimal Quantity { get => OrderQty; set => OrderQty = value; }

    [NotMapped]
    public decimal Qty { get => OrderQty; set => OrderQty = value; }
}
