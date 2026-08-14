using System.Text.Json.Serialization;

namespace AidlyErp.Inv.Application.Dto;

/// <summary>One attribute axis plus its selectable values, for the variant-matrix builder.</summary>
public class Inv1001AttributeOptionDto
{
    [JsonPropertyName("attribute_no")]
    public long AttributeNo { get; set; }

    [JsonPropertyName("attribute_name")]
    public string? AttributeName { get; set; }

    [JsonPropertyName("values")]
    public List<InvOptionDto> Values { get; set; } = new();
}

public class Inv1001BarcodeDto
{
    [JsonPropertyName("barcode_no")]
    public long? BarcodeNo { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    /// <summary>Base-UOM units this barcode stands for — a carton barcode scans as its pack size.</summary>
    [JsonPropertyName("pack_qty")]
    public decimal? PackQty { get; set; }

    [JsonPropertyName("barcode_type")]
    public short? BarcodeType { get; set; }

    [JsonPropertyName("is_primary")]
    public short? IsPrimary { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }
}

/// <summary>What a scanner lookup returns — enough to add a priced cart line.</summary>
public class Inv1001BarcodeResolveDto
{
    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [JsonPropertyName("product_no")]
    public long ProductNo { get; set; }

    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("variant_name")]
    public string? VariantName { get; set; }

    [JsonPropertyName("uom_no")]
    public long? UomNo { get; set; }

    [JsonPropertyName("pack_qty")]
    public decimal PackQty { get; set; } = 1m;

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }
}

public class Inv1001UomConvDto
{
    [JsonPropertyName("uom_conversion_no")]
    public long? UomConversionNo { get; set; }

    [JsonPropertyName("from_uom_no")]
    public long? FromUomNo { get; set; }

    [JsonPropertyName("from_uom_name")]
    public string? FromUomName { get; set; }

    /// <summary>Multiplier from the from-UOM to the base UOM.</summary>
    [JsonPropertyName("to_base_factor")]
    public decimal? ToBaseFactor { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }
}

public class Inv1001VariantDto
{
    [JsonPropertyName("variant_no")]
    public long? VariantNo { get; set; }

    [JsonPropertyName("variant_sku")]
    public string? VariantSku { get; set; }

    [JsonPropertyName("variant_name")]
    public string? VariantName { get; set; }

    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }

    [JsonPropertyName("attr1_value_no")]
    public long? Attr1ValueNo { get; set; }

    [JsonPropertyName("attr2_value_no")]
    public long? Attr2ValueNo { get; set; }

    [JsonPropertyName("attr3_value_no")]
    public long? Attr3ValueNo { get; set; }

    [JsonPropertyName("purchase_price")]
    public decimal? PurchasePrice { get; set; }

    [JsonPropertyName("sale_price")]
    public decimal? SalePrice { get; set; }

    [JsonPropertyName("mrp")]
    public decimal? Mrp { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }
}

public class Inv1001ProductDto
{
    [JsonPropertyName("product_no")]
    public long? ProductNo { get; set; }

    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("product_name_nls")]
    public string? ProductNameNls { get; set; }

    /// <summary>Short label for POS buttons and receipt lines.</summary>
    [JsonPropertyName("short_name")]
    public string? ShortName { get; set; }

    /// <summary>1=Standard 2=VariantParent 3=Service 4=Bundle.</summary>
    [JsonPropertyName("product_type")]
    public short? ProductType { get; set; }

    [JsonPropertyName("category_no")]
    public long CategoryNo { get; set; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("brand_no")]
    public long? BrandNo { get; set; }

    [JsonPropertyName("brand_name")]
    public string? BrandName { get; set; }

    [JsonPropertyName("base_uom_no")]
    public long BaseUomNo { get; set; }

    [JsonPropertyName("base_uom_name")]
    public string? BaseUomName { get; set; }

    [JsonPropertyName("purchase_uom_no")]
    public long? PurchaseUomNo { get; set; }

    [JsonPropertyName("sales_uom_no")]
    public long? SalesUomNo { get; set; }

    [JsonPropertyName("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [JsonPropertyName("is_tax_inclusive")]
    public short? IsTaxInclusive { get; set; }

    [JsonPropertyName("hsn_sac_code")]
    public string? HsnSacCode { get; set; }

    [JsonPropertyName("is_stock_tracked")]
    public short? IsStockTracked { get; set; }

    [JsonPropertyName("is_batch_tracked")]
    public short? IsBatchTracked { get; set; }

    [JsonPropertyName("is_expiry_tracked")]
    public short? IsExpiryTracked { get; set; }

    [JsonPropertyName("is_serial_tracked")]
    public short? IsSerialTracked { get; set; }

    [JsonPropertyName("has_variants")]
    public short? HasVariants { get; set; }

    [JsonPropertyName("shelf_life_days")]
    public int? ShelfLifeDays { get; set; }

    [JsonPropertyName("cost_price")]
    public decimal? CostPrice { get; set; }

    [JsonPropertyName("purchase_price")]
    public decimal? PurchasePrice { get; set; }

    [JsonPropertyName("sale_price")]
    public decimal? SalePrice { get; set; }

    [JsonPropertyName("mrp")]
    public decimal? Mrp { get; set; }

    [JsonPropertyName("min_sale_price")]
    public decimal? MinSalePrice { get; set; }

    [JsonPropertyName("default_margin_pct")]
    public decimal? DefaultMarginPct { get; set; }

    [JsonPropertyName("reorder_level")]
    public decimal? ReorderLevel { get; set; }

    [JsonPropertyName("reorder_qty")]
    public decimal? ReorderQty { get; set; }

    [JsonPropertyName("min_stock")]
    public decimal? MinStock { get; set; }

    [JsonPropertyName("max_stock")]
    public decimal? MaxStock { get; set; }

    [JsonPropertyName("weight_gm")]
    public decimal? WeightGm { get; set; }

    /// <summary>The primary barcode; the full set lives in the barcodes collection.</summary>
    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }

    [JsonPropertyName("image_path")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("is_sellable")]
    public short? IsSellable { get; set; }

    [JsonPropertyName("is_purchasable")]
    public short? IsPurchasable { get; set; }

    [JsonPropertyName("allow_discount")]
    public short? AllowDiscount { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("is_active")]
    public short? IsActive { get; set; }

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    /// <summary>True once stock has moved; the form then locks tracking flags and the base UOM.</summary>
    [JsonPropertyName("has_movements")]
    public bool HasMovements { get; set; }

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
    public List<InvOptionDto> Categories { get; set; } = new();

    [JsonPropertyName("brands")]
    public List<InvOptionDto> Brands { get; set; } = new();

    [JsonPropertyName("uoms")]
    public List<InvOptionDto> Uoms { get; set; } = new();

    [JsonPropertyName("taxes")]
    public List<InvOptionDto> Taxes { get; set; } = new();

    [JsonPropertyName("attributes")]
    public List<Inv1001AttributeOptionDto> Attributes { get; set; } = new();
}
