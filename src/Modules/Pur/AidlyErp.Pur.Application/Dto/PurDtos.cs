using System.Text.Json.Serialization;

namespace AidlyErp.Pur.Application.Dto;

public class Pur1001SupplierDto
{
    [JsonPropertyName("supplier_no")]
    public long? SupplierNo { get; set; }

    [JsonPropertyName("supplier_code")]
    public string? SupplierCode { get; set; }

    [JsonPropertyName("supplier_id")]
    public string? SupplierId { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("supplier_name_nls")]
    public string? SupplierNameNls { get; set; }

    [JsonPropertyName("supplier_type")]
    public short? SupplierType { get; set; }

    [JsonPropertyName("contact_person")]
    public string? ContactPerson { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("mobile_no")]
    public string? MobileNo { get; set; }

    [JsonPropertyName("alt_mobile_no")]
    public string? AltMobileNo { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("phone_no")]
    public string? PhoneNo { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("address_line1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("address_line2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state_province")]
    public string? StateProvince { get; set; }

    [JsonPropertyName("post_code")]
    public string? PostCode { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("tax_number")]
    public string? TaxNumber { get; set; }

    [JsonPropertyName("vat_reg_no")]
    public string? VatRegNo { get; set; }

    [JsonPropertyName("tin_no")]
    public string? TinNo { get; set; }

    [JsonPropertyName("bin_no")]
    public string? BinNo { get; set; }

    [JsonPropertyName("trade_license_no")]
    public string? TradeLicenseNo { get; set; }

    [JsonPropertyName("payment_terms")]
    public short? PaymentTerms { get; set; }

    [JsonPropertyName("credit_days")]
    public int? CreditDays { get; set; }

    [JsonPropertyName("credit_limit")]
    public decimal? CreditLimit { get; set; }

    [JsonPropertyName("default_currency_no")]
    public long? DefaultCurrencyNo { get; set; }

    [JsonPropertyName("default_vat_tax_no")]
    public long? DefaultVatTaxNo { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [JsonPropertyName("opening_balance_date")]
    public DateTime? OpeningBalanceDate { get; set; }

    [JsonPropertyName("current_balance")]
    public decimal CurrentBalance { get; set; }

    [JsonPropertyName("current_payable")]
    public decimal CurrentPayable { get; set; }

    [JsonPropertyName("bank_name")]
    public string? BankName { get; set; }

    [JsonPropertyName("bank_account_no")]
    public string? BankAccountNo { get; set; }

    [JsonPropertyName("bank_branch")]
    public string? BankBranch { get; set; }

    [JsonPropertyName("routing_no")]
    public string? RoutingNo { get; set; }

    [JsonPropertyName("mfs_provider")]
    public string? MfsProvider { get; set; }

    [JsonPropertyName("mfs_number")]
    public string? MfsNumber { get; set; }

    [JsonPropertyName("lead_time_days")]
    public int? LeadTimeDays { get; set; }

    [JsonPropertyName("rating")]
    public short? Rating { get; set; }

    [JsonPropertyName("image_path")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Pur1002PriceRowDto
{
    [JsonPropertyName("supplier_product_no")]
    public long? SupplierProductNo { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long UomNo { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("supplier_sku")]
    public string? SupplierSku { get; set; }

    [JsonPropertyName("last_price")]
    public decimal LastPrice { get; set; }

    [JsonPropertyName("discount_pct")]
    public decimal DiscountPct { get; set; }

    [JsonPropertyName("moq")]
    public decimal Moq { get; set; }

    [JsonPropertyName("lead_time_days")]
    public int? LeadTimeDays { get; set; }

    [JsonPropertyName("is_preferred")]
    public short IsPreferred { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Pur1101LineDto
{
    [JsonPropertyName("order_dtl_no")]
    public long? OrderDtlNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_code")]
    public string? ProductCode { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("received_qty_base")]
    public decimal ReceivedQtyBase { get; set; }


    [JsonPropertyName("order_qty")]
    public decimal OrderQty { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("discount_pct")]
    public decimal DiscountPct { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [JsonPropertyName("tax_rate_pct")]
    public decimal TaxRatePct { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Pur1101OrderDto
{
    [JsonPropertyName("order_no")]
    public long? OrderNo { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("order_date")]
    public DateTime OrderDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("expected_date")]
    public DateTime? ExpectedDate { get; set; }

    [JsonPropertyName("currency_no")]
    public long? CurrencyNo { get; set; }

    [JsonPropertyName("sub_total")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("discount_total")]
    public decimal DiscountTotal { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("shipping_estimate")]
    public decimal ShippingEstimate { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("received_value")]
    public decimal ReceivedValue { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Submitted 3=Approved 7=Cancelled

    [JsonPropertyName("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [JsonPropertyName("terms_note")]
    public string? TermsNote { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Pur1101LineDto> Lines { get; set; } = new();
}

public class Pur1102LineDto
{
    [JsonPropertyName("invoice_dtl_no")]
    public long? InvoiceDtlNo { get; set; }

    /// <summary>
    /// Set when this line bills a goods-receipt line. Drives the three-way match: the line then
    /// clears GRN clearing instead of posting stock and debiting Inventory a second time.
    /// </summary>
    [JsonPropertyName("receipt_dtl_no")]
    public long? ReceiptDtlNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_code")]
    public string? ProductCode { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

    [JsonPropertyName("qty_base")]
    public decimal QtyBase { get; set; }

    [JsonPropertyName("final_unit_cost")]
    public decimal FinalUnitCost { get; set; }


    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("discount_pct")]
    public decimal DiscountPct { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [JsonPropertyName("tax_rate_pct")]
    public decimal TaxRatePct { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("net_amount")]
    public decimal NetAmount { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("mfg_date")]
    public DateTime? MfgDate { get; set; }

    [JsonPropertyName("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Pur1102InvoiceDto
{
    [JsonPropertyName("invoice_no")]
    public long? InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("supplier_invoice_no")]
    public string? SupplierInvoiceNo { get; set; }

    [JsonPropertyName("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("order_no")]
    public long? OrderNo { get; set; }

    [JsonPropertyName("currency_no")]
    public long? CurrencyNo { get; set; }

    [JsonPropertyName("exchange_rate")]
    public decimal ExchangeRate { get; set; } = 1m;

    [JsonPropertyName("sub_total")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("discount_total")]
    public decimal DiscountTotal { get; set; }

    [JsonPropertyName("taxable_amount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("shipping_charge")]
    public decimal ShippingCharge { get; set; }

    [JsonPropertyName("other_charges")]
    public decimal OtherCharges { get; set; }

    [JsonPropertyName("round_off")]
    public decimal RoundOff { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("paid_amount")]
    public decimal PaidAmount { get; set; }

    [JsonPropertyName("due_amount")]
    public decimal DueAmount { get; set; }

    [JsonPropertyName("payment_status")]
    public short PaymentStatus { get; set; } = 1;

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled 4=Returned 5=Cancelled

    [JsonPropertyName("due_date")]
    public DateTime? DueDate { get; set; }

    [JsonPropertyName("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [JsonPropertyName("cancel_reason")]
    public string? CancelReason { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Pur1102LineDto> Lines { get; set; } = new();
}

/// <summary>One dropdown entry. The frontend binds <c>no</c>/<c>name</c> for every PUR lookup list.</summary>
public class PurOptionDto
{
    [JsonPropertyName("no")]
    public long No { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    public PurOptionDto() { }

    public PurOptionDto(long no, string? name)
    {
        No = no;
        Name = name ?? string.Empty;
    }
}

public class Pur1102LookupDto
{
    [JsonPropertyName("suppliers")]
    public List<PurOptionDto> Suppliers { get; set; } = new();

    [JsonPropertyName("warehouses")]
    public List<PurOptionDto> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<PurOptionDto> Products { get; set; } = new();

    [JsonPropertyName("uoms")]
    public List<PurOptionDto> Uoms { get; set; } = new();
}

public class Pur1103LineDto
{
    [JsonPropertyName("return_dtl_no")]
    public long? ReturnDtlNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }


    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("tax_rate_pct")]
    public decimal TaxRatePct { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Pur1103ReturnDto
{
    [JsonPropertyName("return_no")]
    public long? ReturnNo { get; set; }

    [JsonPropertyName("return_id")]
    public string? ReturnId { get; set; }

    [JsonPropertyName("return_date")]
    public DateTime ReturnDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("invoice_no")]
    public long? InvoiceNo { get; set; }

    [JsonPropertyName("original_invoice_no")]
    public long? OriginalInvoiceNo { get; set; }

    [JsonPropertyName("return_reason")]
    public string? ReturnReason { get; set; }

    [JsonPropertyName("settlement_mode")]
    public short SettlementMode { get; set; } = 1;

    [JsonPropertyName("sub_total")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("lines")]
    public List<Pur1103LineDto> Lines { get; set; } = new();
}

public class Pur1104AllocDto
{
    [JsonPropertyName("payment_alloc_no")]
    public long? PaymentAllocNo { get; set; }

    [JsonPropertyName("payment_no")]
    public long? PaymentNo { get; set; }

    [JsonPropertyName("invoice_no")]
    public long InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [JsonPropertyName("invoice_due")]
    public decimal InvoiceDue { get; set; }

    [JsonPropertyName("allocated_amount")]
    public decimal AllocatedAmount { get; set; }
}

public class Pur1104OpenInvoiceDto
{
    [JsonPropertyName("invoice_no")]
    public long InvoiceNo { get; set; }

    /// <summary>The supplier's own bill number. Our invoice_id means nothing to them, so this is
    /// what a payables clerk matches against the paperwork.</summary>
    [JsonPropertyName("supplier_invoice_no")]
    public string? SupplierInvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("paid_amount")]
    public decimal PaidAmount { get; set; }

    [JsonPropertyName("due_amount")]
    public decimal DueAmount { get; set; }
}

public class Pur1104PaymentDto
{
    [JsonPropertyName("payment_no")]
    public long? PaymentNo { get; set; }

    [JsonPropertyName("payment_id")]
    public string? PaymentId { get; set; }

    [JsonPropertyName("payment_date")]
    public DateTime PaymentDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("payment_method")]
    public short PaymentMethod { get; set; } = 1; // 1=Cash 2=Bank 3=Cheque 4=Card 5=Mobile 6=Online

    [JsonPropertyName("gl_account_no")]
    public long? GlAccountNo { get; set; }

    [JsonPropertyName("cheque_no")]
    public string? ChequeNo { get; set; }

    [JsonPropertyName("cheque_date")]
    public DateTime? ChequeDate { get; set; }

    [JsonPropertyName("txn_ref")]
    public string? TxnRef { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("allocated_amount")]
    public decimal AllocatedAmount { get; set; }

    [JsonPropertyName("unallocated_amount")]
    public decimal UnallocatedAmount { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("allocations")]
    public List<Pur1104AllocDto> Allocations { get; set; } = new();
}

public class Pur1105LineDto
{
    [JsonPropertyName("receipt_dtl_no")]
    public long? ReceiptDtlNo { get; set; }

    [JsonPropertyName("order_dtl_no")]
    public long? OrderDtlNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }


    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("qty_base")]
    public decimal QtyBase { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("tax_rate_pct")]
    public decimal TaxRatePct { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("batch_no")]
    public long? BatchNo { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("mfg_date")]
    public DateTime? MfgDate { get; set; }

    [JsonPropertyName("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Pur1105ReceiptDto
{
    [JsonPropertyName("receipt_no")]
    public long? ReceiptNo { get; set; }

    [JsonPropertyName("receipt_id")]
    public string? ReceiptId { get; set; }

    [JsonPropertyName("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [JsonPropertyName("supplier_no")]
    public long SupplierNo { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("order_no")]
    public long? OrderNo { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("sub_total")]
    public decimal SubTotal { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    /// <summary>Why the receipt was cancelled. Stored on the row; a blank cancelled GRN is
    /// unauditable.</summary>
    [JsonPropertyName("cancel_reason")]
    public string? CancelReason { get; set; }

    [JsonPropertyName("lines")]
    public List<Pur1105LineDto> Lines { get; set; } = new();
}

public class Pur1106AllocDto
{
    [JsonPropertyName("landed_cost_alloc_no")]
    public long? LandedCostAllocNo { get; set; }

    [JsonPropertyName("invoice_dtl_no")]
    public long InvoiceDtlNo { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("qty_base")]
    public decimal QtyBase { get; set; }

    [JsonPropertyName("taxable_amount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("previous_alloc")]
    public decimal PreviousAlloc { get; set; }

    [JsonPropertyName("allocated_amount")]
    public decimal AllocatedAmount { get; set; }

    [JsonPropertyName("final_unit_cost")]
    public decimal FinalUnitCost { get; set; }
}

public class Pur1106LandedCostDto
{
    [JsonPropertyName("landed_cost_no")]
    public long? LandedCostNo { get; set; }

    [JsonPropertyName("landed_cost_id")]
    public string? LandedCostId { get; set; }

    [JsonPropertyName("cost_date")]
    public DateTime CostDate { get; set; }

    [JsonPropertyName("invoice_no")]
    public long InvoiceNo { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("receipt_no")]
    public long? ReceiptNo { get; set; }

    /// <summary>1 Freight · 2 Duty · 3 Clearing · 4 Insurance · 5 Handling (purConstants.landedCostTypes).</summary>
    [JsonPropertyName("cost_type")]
    public short? CostType { get; set; } = 1;

    [JsonPropertyName("vendor_no")]
    public long? VendorNo { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("alloc_basis")]
    public short? AllocBasis { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("invoice_landed_cost_total")]
    public decimal InvoiceLandedCostTotal { get; set; }

    [JsonPropertyName("allocations")]
    public List<Pur1106AllocDto> Allocations { get; set; } = new();
}

/// <summary>
/// A posted goods-receipt line that is still waiting to be billed. PUR_1102 offers these so an
/// invoice can be raised against real receipts — the link is what stops the invoice re-posting
/// stock the GRN already took in.
/// </summary>
public class Pur1102ReceiptLineDto
{
    [JsonPropertyName("receipt_dtl_no")]
    public long ReceiptDtlNo { get; set; }

    [JsonPropertyName("receipt_no")]
    public long ReceiptNo { get; set; }

    [JsonPropertyName("receipt_id")]
    public string? ReceiptId { get; set; }

    [JsonPropertyName("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("uom_no")]
    public long UomNo { get; set; }

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("tax_rate_pct")]
    public decimal TaxRatePct { get; set; }

    [JsonPropertyName("batch_no")]
    public long? BatchNo { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }
}
