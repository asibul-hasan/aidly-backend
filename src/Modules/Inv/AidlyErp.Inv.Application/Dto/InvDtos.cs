using System.Text.Json.Serialization;

namespace AidlyErp.Inv.Application.Dto;
public class Inv1003CategoryDto
{
    [JsonPropertyName("category_no")]
    public long? CategoryNo { get; set; }

    [JsonPropertyName("category_id")]
    public string? CategoryId { get; set; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("category_name_nls")]
    public string? CategoryNameNls { get; set; }

    [JsonPropertyName("parent_category_no")]
    public long? ParentCategoryNo { get; set; }

    [JsonPropertyName("parent_category_name")]
    public string? ParentCategoryName { get; set; }

    [JsonPropertyName("default_vat_tax_no")]
    public long? DefaultVatTaxNo { get; set; }

    [JsonPropertyName("default_vat_tax_label")]
    public string? DefaultVatTaxLabel { get; set; }

    /// <summary>Server-derived from the parent chain; the client never sets it.</summary>
    [JsonPropertyName("depth")]
    public short? Depth { get; set; }

    /// <summary>Server-derived materialised path, e.g. <c>/1/7/22/</c>.</summary>
    [JsonPropertyName("tree_path")]
    public string? TreePath { get; set; }

    [JsonPropertyName("image_path")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("order_sl")]
    public int? OrderSl { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Inv1004BrandDto
{
    [JsonPropertyName("brand_no")]
    public long? BrandNo { get; set; }

    [JsonPropertyName("brand_id")]
    public string? BrandId { get; set; }

    [JsonPropertyName("brand_name")]
    public string? BrandName { get; set; }

    [JsonPropertyName("brand_name_nls")]
    public string? BrandNameNls { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("image_path")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Inv1005UomDto
{
    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_id")]
    public string? UomId { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("uom_name_nls")]
    public string? UomNameNls { get; set; }

    /// <summary>1=Count 2=Weight 3=Volume 4=Length.</summary>
    [JsonPropertyName("uom_type")]
    public short? UomType { get; set; }

    [JsonPropertyName("decimal_places")]
    public short? DecimalPlaces { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Inv1006AttributeValueDto
{
    [JsonPropertyName("attribute_value_no")]
    public long? AttributeValueNo { get; set; }

    [JsonPropertyName("value_code")]
    public string ValueCode { get; set; } = string.Empty;

    [JsonPropertyName("value_name")]
    public string ValueName { get; set; } = string.Empty;

    [JsonPropertyName("order_sl")]
    public int? OrderSl { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Inv1006AttributeDto
{
    [JsonPropertyName("attribute_no")]
    public long? AttributeNo { get; set; }

    [JsonPropertyName("attribute_id")]
    public string? AttributeId { get; set; }

    [JsonPropertyName("attribute_name")]
    public string? AttributeName { get; set; }

    [JsonPropertyName("order_sl")]
    public int? OrderSl { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("values")]
    public List<Inv1006AttributeValueDto> Values { get; set; } = new();
}

/// <summary>
/// One dropdown entry for an INV form. INV binds <c>value</c>/<c>label</c> — unlike PUR and SAL,
/// which use <c>no</c>/<c>name</c>. Renaming these would break the built Angular forms.
/// </summary>
public class InvOptionDto
{
    [JsonPropertyName("value")]
    public long Value { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    public InvOptionDto() { }

    public InvOptionDto(long value, string? label)
    {
        Value = value;
        Label = label ?? string.Empty;
    }
}

public class Inv1101LineDto
{
    [JsonPropertyName("adjustment_dtl_no")]
    public long? AdjustmentDtlNo { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

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

    [JsonPropertyName("qty_base")]
    public decimal QtyBase { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("line_value")]
    public decimal LineValue { get; set; }

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

/// <summary>
/// INV_1101 Opening Stock. Stored as an <c>inv_stock_adjustment</c> with
/// <c>adjustment_type = 2</c>, which is why the fields are named after the adjustment document.
/// </summary>
public class Inv1101OpeningDto
{
    [JsonPropertyName("adjustment_no")]
    public long? AdjustmentNo { get; set; }

    [JsonPropertyName("adjustment_id")]
    public string? AdjustmentId { get; set; }

    [JsonPropertyName("adjustment_date")]
    public DateTime AdjustmentDate { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("reason_text")]
    public string? ReasonText { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("total_in_value")]
    public decimal? TotalInValue { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Inv1101LineDto> Lines { get; set; } = new();
}

public class Inv1101LookupDto
{
    [JsonPropertyName("warehouses")]
    public List<InvOptionDto> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<InvOptionDto> Products { get; set; } = new();

    [JsonPropertyName("uoms")]
    public List<InvOptionDto> Uoms { get; set; } = new();
}

public class Inv1102LineDto
{
    [JsonPropertyName("adjustment_dtl_no")]
    public long? AdjustmentDtlNo { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

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

    /// <summary>+1 stock in, -1 stock out.</summary>
    [JsonPropertyName("direction")]
    public short Direction { get; set; } = 1;

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("qty_base")]
    public decimal QtyBase { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("line_value")]
    public decimal LineValue { get; set; }

    [JsonPropertyName("batch_no")]
    public long? BatchNo { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Inv1102AdjustmentDto
{
    [JsonPropertyName("adjustment_no")]
    public long? AdjustmentNo { get; set; }

    [JsonPropertyName("adjustment_id")]
    public string? AdjustmentId { get; set; }

    [JsonPropertyName("adjustment_date")]
    public DateTime AdjustmentDate { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("adjustment_type")]
    public short AdjustmentType { get; set; } = 1;

    [JsonPropertyName("reason_text")]
    public string? ReasonText { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("total_in_value")]
    public decimal? TotalInValue { get; set; }

    [JsonPropertyName("total_out_value")]
    public decimal? TotalOutValue { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    [JsonPropertyName("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Inv1102LineDto> Lines { get; set; } = new();
}

public class Inv1102LookupDto
{
    [JsonPropertyName("warehouses")]
    public List<InvOptionDto> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<InvOptionDto> Products { get; set; } = new();

    [JsonPropertyName("uoms")]
    public List<InvOptionDto> Uoms { get; set; } = new();
}

public class Inv2001WarehouseDto
{
    [JsonPropertyName("warehouse_no")]
    public long? WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_id")]
    public string? WarehouseId { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("warehouse_name_nls")]
    public string? WarehouseNameNls { get; set; }

    /// <summary>1=Main 2=Outlet 3=Transit 4=Damage 5=Returns.</summary>
    [JsonPropertyName("warehouse_type")]
    public short? WarehouseType { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("manager_employee_no")]
    public long? ManagerEmployeeNo { get; set; }

    [JsonPropertyName("is_default")]
    public short? IsDefault { get; set; }

    [JsonPropertyName("allow_negative_stock")]
    public short? AllowNegativeStock { get; set; }

    [JsonPropertyName("is_sale_point")]
    public short? IsSalePoint { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Inv2002RackDto
{
    [JsonPropertyName("rack_no")]
    public long? RackNo { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("rack_id")]
    public string? RackId { get; set; }

    [JsonPropertyName("rack_name")]
    public string? RackName { get; set; }

    [JsonPropertyName("aisle")]
    public string? Aisle { get; set; }

    [JsonPropertyName("rack")]
    public string? Rack { get; set; }

    [JsonPropertyName("shelf")]
    public string? Shelf { get; set; }

    [JsonPropertyName("bin")]
    public string? Bin { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Inv2003TransferLineDto
{
    [JsonPropertyName("transfer_dtl_no")]
    public long? TransferDtlNo { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

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

    [JsonPropertyName("qty_sent")]
    public decimal QtySent { get; set; }

    [JsonPropertyName("qty_sent_base")]
    public decimal QtySentBase { get; set; }

    [JsonPropertyName("qty_received")]
    public decimal? QtyReceived { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("batch_no")]
    public long? BatchNo { get; set; }

    [JsonPropertyName("batch_code")]
    public string? BatchCode { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Inv2003TransferDto
{
    [JsonPropertyName("transfer_no")]
    public long? TransferNo { get; set; }

    [JsonPropertyName("transfer_id")]
    public string? TransferId { get; set; }

    [JsonPropertyName("from_warehouse_no")]
    public long FromWarehouseNo { get; set; }

    [JsonPropertyName("from_warehouse_name")]
    public string? FromWarehouseName { get; set; }

    [JsonPropertyName("to_warehouse_no")]
    public long ToWarehouseNo { get; set; }

    [JsonPropertyName("to_warehouse_name")]
    public string? ToWarehouseName { get; set; }

    /// <summary>Optional — resolves to the branch's type-3 transit store when omitted.</summary>
    [JsonPropertyName("transit_warehouse_no")]
    public long? TransitWarehouseNo { get; set; }

    [JsonPropertyName("transfer_date")]
    public DateTime TransferDate { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("total_qty")]
    public decimal? TotalQty { get; set; }

    [JsonPropertyName("total_value")]
    public decimal? TotalValue { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("lines")]
    public List<Inv2003TransferLineDto> Lines { get; set; } = new();
}

public class Inv2003LookupDto
{
    [JsonPropertyName("warehouses")]
    public List<InvOptionDto> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<InvOptionDto> Products { get; set; } = new();

    [JsonPropertyName("uoms")]
    public List<InvOptionDto> Uoms { get; set; } = new();
}

public class Inv1002BarcodeDto
{
    [JsonPropertyName("barcode_no")]
    public long? BarcodeNo { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [JsonPropertyName("uom_no")]
    public long UomNo { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    /// <summary>Units of the base UOM this barcode represents — a carton barcode scans as 12.</summary>
    [JsonPropertyName("pack_qty")]
    public decimal? PackQty { get; set; }

    [JsonPropertyName("barcode_type")]
    public short? BarcodeType { get; set; }

    [JsonPropertyName("is_primary")]
    public short? IsPrimary { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Inv1002LookupDto
{
    [JsonPropertyName("products")]
    public List<InvOptionDto> Products { get; set; } = new();

    [JsonPropertyName("uoms")]
    public List<InvOptionDto> Uoms { get; set; } = new();
}

/// <summary>One batch's position in one warehouse, as the INV_2004 expiry dashboard shows it.</summary>
public class Inv2004BatchDto
{
    [JsonPropertyName("batch_no")]
    public long BatchNo { get; set; }

    [JsonPropertyName("batch_code")]
    public string BatchCode { get; set; } = string.Empty;

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("mfg_date")]
    public DateTime? MfgDate { get; set; }

    [JsonPropertyName("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [JsonPropertyName("days_to_expiry")]
    public int? DaysToExpiry { get; set; }

    /// <summary><c>expired</c>, <c>near_expiry</c>, <c>ok</c>, or <c>no_expiry</c>.</summary>
    [JsonPropertyName("expiry_status")]
    public string ExpiryStatus { get; set; } = "no_expiry";

    [JsonPropertyName("received_cost")]
    public decimal? ReceivedCost { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("qty_on_hand")]
    public decimal QtyOnHand { get; set; }

    [JsonPropertyName("qty_available")]
    public decimal QtyAvailable { get; set; }

    [JsonPropertyName("stock_value")]
    public decimal StockValue { get; set; }
}

public class Inv2005ReorderDto
{
    [JsonPropertyName("reorder_no")]
    public long? ReorderNo { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    /// <summary>On-hand level at which replenishment should be triggered.</summary>
    [JsonPropertyName("reorder_level")]
    public decimal? ReorderLevel { get; set; }

    [JsonPropertyName("reorder_qty")]
    public decimal? ReorderQty { get; set; }

    [JsonPropertyName("min_stock")]
    public decimal? MinStock { get; set; }

    [JsonPropertyName("max_stock")]
    public decimal? MaxStock { get; set; }

    [JsonPropertyName("preferred_supplier_no")]
    public long? PreferredSupplierNo { get; set; }

    [JsonPropertyName("lead_time_days")]
    public int? LeadTimeDays { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class InvLookupDto
{
    [JsonPropertyName("products")]
    public List<object> Products { get; set; } = new();

    [JsonPropertyName("warehouses")]
    public List<object> Warehouses { get; set; } = new();
}
