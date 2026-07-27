using System.Text.Json.Serialization;

namespace AidlyErp.Application.Inv.Dto;

public class Inv1001AttributeOptionDto
{
    [JsonPropertyName("attribute_no")]
    public long AttributeNo { get; set; }

    [JsonPropertyName("attribute_name")]
    public string? AttributeName { get; set; }

    [JsonPropertyName("attribute_value_no")]
    public long AttributeValueNo { get; set; }

    [JsonPropertyName("attribute_value")]
    public string? AttributeValue { get; set; }
}

public class Inv1001BarcodeDto
{
    [JsonPropertyName("barcode_no")]
    public long? BarcodeNo { get; set; }

    [JsonPropertyName("product_no")]
    public long? ProductNo { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [JsonPropertyName("barcode_type")]
    public string? BarcodeType { get; set; } = "CODE128";

    [JsonPropertyName("is_primary")]
    public short IsPrimary { get; set; } = 0;
}

public class Inv1001BarcodeResolveDto
{
    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_code")]
    public string? ProductCode { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("variant_name")]
    public string? VariantName { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }
}

public class Inv1001UomConvDto
{
    [JsonPropertyName("conversion_no")]
    public long? ConversionNo { get; set; }

    [JsonPropertyName("from_uom_no")]
    public long FromUomNo { get; set; }

    [JsonPropertyName("from_uom_name")]
    public string? FromUomName { get; set; }

    [JsonPropertyName("to_uom_no")]
    public long ToUomNo { get; set; }

    [JsonPropertyName("to_uom_name")]
    public string? ToUomName { get; set; }

    [JsonPropertyName("conversion_factor")]
    public decimal ConversionFactor { get; set; }
}

public class Inv1001VariantDto
{
    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("product_no")]
    public long? ProductNo { get; set; }

    [JsonPropertyName("variant_code")]
    public string? VariantCode { get; set; }

    [JsonPropertyName("variant_name")]
    public string? VariantName { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("cost_price")]
    public decimal CostPrice { get; set; }

    [JsonPropertyName("selling_price")]
    public decimal SellingPrice { get; set; }

    [JsonPropertyName("barcodes")]
    public List<Inv1001BarcodeDto> Barcodes { get; set; } = new();

    [JsonPropertyName("attributes")]
    public List<Inv1001AttributeOptionDto> Attributes { get; set; } = new();
}

public class Inv1001ProductDto
{
    [JsonPropertyName("product_no")]
    public long? ProductNo { get; set; }

    [JsonPropertyName("product_code")]
    public string? ProductCode { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("category_no")]
    public long CategoryNo { get; set; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("brand_no")]
    public long? BrandNo { get; set; }

    [JsonPropertyName("brand_name")]
    public string? BrandName { get; set; }

    [JsonPropertyName("uom_no")]
    public long UomNo { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("cost_price")]
    public decimal CostPrice { get; set; }

    [JsonPropertyName("selling_price")]
    public decimal SellingPrice { get; set; }

    [JsonPropertyName("mrp")]
    public decimal Mrp { get; set; }

    [JsonPropertyName("tax_rate")]
    public decimal TaxRate { get; set; }

    [JsonPropertyName("has_variants")]
    public short HasVariants { get; set; } = 0;

    [JsonPropertyName("has_batches")]
    public short HasBatches { get; set; } = 0;

    [JsonPropertyName("has_expiry")]
    public short HasExpiry { get; set; } = 0;

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("variants")]
    public List<Inv1001VariantDto> Variants { get; set; } = new();

    [JsonPropertyName("barcodes")]
    public List<Inv1001BarcodeDto> Barcodes { get; set; } = new();

    [JsonPropertyName("uom_conversions")]
    public List<Inv1001UomConvDto> UomConversions { get; set; } = new();
}

public class Inv1001LookupDto
{
    [JsonPropertyName("categories")]
    public List<object> Categories { get; set; } = new();

    [JsonPropertyName("brands")]
    public List<object> Brands { get; set; } = new();

    [JsonPropertyName("uoms")]
    public List<object> Uoms { get; set; } = new();

    [JsonPropertyName("attributes")]
    public List<object> Attributes { get; set; } = new();
}

public class Inv1002BarcodeDto
{
    [JsonPropertyName("barcode_no")]
    public long? BarcodeNo { get; set; }

    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("variant_name")]
    public string? VariantName { get; set; }

    [JsonPropertyName("is_primary")]
    public short IsPrimary { get; set; } = 0;
}

public class Inv1002LookupDto
{
    [JsonPropertyName("products")]
    public List<object> Products { get; set; } = new();
}

public class Inv1003CategoryDto
{
    [JsonPropertyName("category_no")]
    public long? CategoryNo { get; set; }

    [JsonPropertyName("category_code")]
    public string? CategoryCode { get; set; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("parent_category_no")]
    public long? ParentCategoryNo { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class Inv1004BrandDto
{
    [JsonPropertyName("brand_no")]
    public long? BrandNo { get; set; }

    [JsonPropertyName("brand_code")]
    public string? BrandCode { get; set; }

    [JsonPropertyName("brand_name")]
    public string? BrandName { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class Inv1005UomDto
{
    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("uom_code")]
    public string? UomCode { get; set; }

    [JsonPropertyName("uom_name")]
    public string? UomName { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class Inv1006AttributeValueDto
{
    [JsonPropertyName("attribute_value_no")]
    public long? AttributeValueNo { get; set; }

    [JsonPropertyName("attribute_value")]
    public string AttributeValue { get; set; } = string.Empty;
}

public class Inv1006AttributeDto
{
    [JsonPropertyName("attribute_no")]
    public long? AttributeNo { get; set; }

    [JsonPropertyName("attribute_code")]
    public string? AttributeCode { get; set; }

    [JsonPropertyName("attribute_name")]
    public string? AttributeName { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("values")]
    public List<Inv1006AttributeValueDto> Values { get; set; } = new();
}

public class Inv1101LineDto
{
    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("uom_no")]
    public long UomNo { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("total_cost")]
    public decimal TotalCost { get; set; }
}

public class Inv1101OpeningDto
{
    [JsonPropertyName("opening_no")]
    public long? OpeningNo { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("opening_date")]
    public DateTime OpeningDate { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("lines")]
    public List<Inv1101LineDto> Lines { get; set; } = new();
}

public class Inv1101LookupDto
{
    [JsonPropertyName("warehouses")]
    public List<object> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<object> Products { get; set; } = new();
}

public class Inv1102LineDto
{
    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("uom_no")]
    public long UomNo { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("adjustment_type")]
    public short AdjustmentType { get; set; } = 1; // 1=Add 2=Deduct

    [JsonPropertyName("unit_cost")]
    public decimal UnitCost { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public class Inv1102AdjustmentDto
{
    [JsonPropertyName("adjustment_no")]
    public long? AdjustmentNo { get; set; }

    [JsonPropertyName("adjustment_id")]
    public string? AdjustmentId { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("adjustment_date")]
    public DateTime AdjustmentDate { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("lines")]
    public List<Inv1102LineDto> Lines { get; set; } = new();
}

public class Inv1102LookupDto
{
    [JsonPropertyName("warehouses")]
    public List<object> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<object> Products { get; set; } = new();
}

public class Inv2001WarehouseDto
{
    [JsonPropertyName("warehouse_no")]
    public long? WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_code")]
    public string? WarehouseCode { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class Inv2002RackDto
{
    [JsonPropertyName("rack_no")]
    public long? RackNo { get; set; }

    [JsonPropertyName("warehouse_no")]
    public long WarehouseNo { get; set; }

    [JsonPropertyName("warehouse_name")]
    public string? WarehouseName { get; set; }

    [JsonPropertyName("rack_code")]
    public string? RackCode { get; set; }

    [JsonPropertyName("rack_name")]
    public string? RackName { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;
}

public class Inv2003TransferLineDto
{
    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("uom_no")]
    public long UomNo { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }
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

    [JsonPropertyName("transfer_date")]
    public DateTime TransferDate { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1;

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("lines")]
    public List<Inv2003TransferLineDto> Lines { get; set; } = new();
}

public class Inv2003LookupDto
{
    [JsonPropertyName("warehouses")]
    public List<object> Warehouses { get; set; } = new();

    [JsonPropertyName("products")]
    public List<object> Products { get; set; } = new();
}

public class Inv2004BatchDto
{
    [JsonPropertyName("batch_no")]
    public long? BatchNo { get; set; }

    [JsonPropertyName("batch_code")]
    public string BatchCode { get; set; } = string.Empty;

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("mfg_date")]
    public DateTime? MfgDate { get; set; }

    [JsonPropertyName("exp_date")]
    public DateTime? ExpDate { get; set; }

    [JsonPropertyName("qty_on_hand")]
    public decimal QtyOnHand { get; set; }
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

    [JsonPropertyName("min_qty")]
    public decimal MinQty { get; set; }

    [JsonPropertyName("max_qty")]
    public decimal MaxQty { get; set; }

    [JsonPropertyName("reorder_qty")]
    public decimal ReorderQty { get; set; }
}

public class InvLookupDto
{
    [JsonPropertyName("products")]
    public List<object> Products { get; set; } = new();

    [JsonPropertyName("warehouses")]
    public List<object> Warehouses { get; set; } = new();
}
