using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Inv.Domain;

[Table("inv_product")]
public class InvProduct : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("product_id")]
    [StringLength(40)]
    public string ProductId { get; set; } = string.Empty;

    [Column("product_name")]
    [StringLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Column("product_name_nls")]
    [StringLength(200)]
    public string? ProductNameNls { get; set; }

    [Column("short_name")]
    [StringLength(60)]
    public string? ShortName { get; set; }

    /// <summary>1=Standard,2=VariantParent,3=Service,4=Bundle.</summary>
    [Column("product_type")]
    public short ProductType { get; set; } = 1;

    [Column("category_no")]
    public long CategoryNo { get; set; }

    [Column("brand_no")]
    public long? BrandNo { get; set; }

    [Column("base_uom_no")]
    public long BaseUomNo { get; set; }

    [Column("purchase_uom_no")]
    public long? PurchaseUomNo { get; set; }

    [Column("sales_uom_no")]
    public long? SalesUomNo { get; set; }

    [Column("vat_tax_no")]
    public long? VatTaxNo { get; set; }

    [Column("is_tax_inclusive")]
    public short IsTaxInclusive { get; set; } = 0;

    [Column("hsn_sac_code")]
    [StringLength(20)]
    public string? HsnSacCode { get; set; }

    [Column("is_stock_tracked")]
    public short IsStockTracked { get; set; } = 1;

    [Column("is_batch_tracked")]
    public short IsBatchTracked { get; set; } = 0;

    [Column("is_expiry_tracked")]
    public short IsExpiryTracked { get; set; } = 0;

    [Column("is_serial_tracked")]
    public short IsSerialTracked { get; set; } = 0;

    [Column("has_variants")]
    public short HasVariants { get; set; } = 0;

    [Column("shelf_life_days")]
    public int? ShelfLifeDays { get; set; }

    [Column("cost_price")]
    public decimal CostPrice { get; set; } = 0m;

    [Column("purchase_price")]
    public decimal PurchasePrice { get; set; } = 0m;

    [Column("sale_price")]
    public decimal SalePrice { get; set; } = 0m;

    [Column("mrp")]
    public decimal Mrp { get; set; } = 0m;

    [Column("min_sale_price")]
    public decimal? MinSalePrice { get; set; }

    [Column("default_margin_pct")]
    public decimal? DefaultMarginPct { get; set; }

    [Column("reorder_level")]
    public decimal ReorderLevel { get; set; } = 0m;

    [Column("reorder_qty")]
    public decimal ReorderQty { get; set; } = 0m;

    [Column("min_stock")]
    public decimal MinStock { get; set; } = 0m;

    [Column("max_stock")]
    public decimal? MaxStock { get; set; }

    [Column("weight_gm")]
    public decimal? WeightGm { get; set; }

    [Column("barcode")]
    [StringLength(64)]
    public string? Barcode { get; set; }

    [Column("image_path")]
    [StringLength(255)]
    public string? ImagePath { get; set; }

    [Column("is_sellable")]
    public short IsSellable { get; set; } = 1;

    [Column("is_purchasable")]
    public short IsPurchasable { get; set; } = 1;

    [Column("allow_discount")]
    public short AllowDiscount { get; set; } = 1;

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public long UomNo { get => BaseUomNo; set => BaseUomNo = value; }

    [NotMapped]
    public decimal SellingPrice { get => SalePrice; set => SalePrice = value; }

    [NotMapped]
    public decimal TaxRate { get => VatTaxNo.HasValue ? 0m : 0m; set { } }

    [NotMapped]
    public short HasBatches { get => IsBatchTracked; set => IsBatchTracked = value; }

    [NotMapped]
    public short HasExpiry { get => IsExpiryTracked; set => IsExpiryTracked = value; }

    [NotMapped]
    public short IsStockable { get => IsStockTracked; set => IsStockTracked = value; }
}

[Table("inv_category")]
public class InvCategory : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("category_no")]
    public long CategoryNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("category_id")]
    [StringLength(30)]
    public string CategoryId { get; set; } = string.Empty;

    [Column("category_name")]
    [StringLength(150)]
    public string CategoryName { get; set; } = string.Empty;

    [Column("category_name_nls")]
    [StringLength(150)]
    public string? CategoryNameNls { get; set; }

    [Column("parent_category_no")]
    public long? ParentCategoryNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("default_vat_tax_no")]
    public long? DefaultVatTaxNo { get; set; }

    [Column("depth")]
    public short Depth { get; set; } = 1;

    [Column("order_sl")]
    public int OrderSl { get; set; }

    [Column("tree_path")]
    [StringLength(500)]
    public string? TreePath { get; set; }

    [Column("image_path")]
    [StringLength(255)]
    public string? ImagePath { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public string CategoryCode { get => CategoryId; set => CategoryId = value; }

    [NotMapped]
    public string? Description { get => Remarks; set => Remarks = value; }
}

[Table("inv_brand")]
public class InvBrand : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("brand_no")]
    public long BrandNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("brand_id")]
    [StringLength(30)]
    public string BrandId { get; set; } = string.Empty;

    [Column("brand_name")]
    [StringLength(150)]
    public string BrandName { get; set; } = string.Empty;

    [Column("brand_name_nls")]
    [StringLength(150)]
    public string? BrandNameNls { get; set; }

    [Column("manufacturer")]
    [StringLength(150)]
    public string? Manufacturer { get; set; }

    [Column("image_path")]
    [StringLength(255)]
    public string? ImagePath { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [NotMapped]
    public string BrandCode { get => BrandId; set => BrandId = value; }
}

[Table("inv_uom")]
public class InvUom : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("uom_no")]
    public long UomNo { get; set; }

    /// <summary>The business key is <c>uom_id</c> in the schema, not <c>uom_code</c>.</summary>
    [Column("uom_id")]
    [StringLength(20)]
    public string UomId { get; set; } = string.Empty;

    [Column("uom_name")]
    [StringLength(100)]
    public string UomName { get; set; } = string.Empty;

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("uom_name_nls")]
    [StringLength(100)]
    public string? UomNameNls { get; set; }

    /// <summary>1=Count 2=Weight 3=Volume 4=Length.</summary>
    [Column("uom_type")]
    public short UomType { get; set; } = 1;

    /// <summary>How many decimals a quantity in this UOM may carry — pieces 0, kilos 3.</summary>
    [Column("decimal_places")]
    public short DecimalPlaces { get; set; } = 0;

    [Column("remarks")]
    public string? Remarks { get; set; }
}

[Table("inv_uom_conversion")]
public class InvUomConversion : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("uom_conversion_no")]
    public long UomConversionNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("from_uom_no")]
    public long FromUomNo { get; set; }

    [Column("to_base_factor")]
    public decimal ToBaseFactor { get; set; } = 1m;

    [NotMapped]
    public long ConversionNo { get => UomConversionNo; set => UomConversionNo = value; }

    [NotMapped]
    public decimal Multiplier { get => ToBaseFactor; set => ToBaseFactor = value; }
}

[Table("inv_product_attribute")]
public class InvProductAttribute : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("attribute_no")]
    public long AttributeNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("attribute_id")]
    [StringLength(30)]
    public string AttributeId { get; set; } = string.Empty;

    [Column("attribute_name")]
    [StringLength(100)]
    public string AttributeName { get; set; } = string.Empty;

    [Column("order_sl")]
    public int OrderSl { get; set; }

    [NotMapped]
    public string? AttributeCode { get => AttributeId; set => AttributeId = value ?? string.Empty; }
}

[Table("inv_product_attribute_value")]
public class InvProductAttributeValue : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("attribute_value_no")]
    public long AttributeValueNo { get; set; }

    [Column("attribute_no")]
    public long AttributeNo { get; set; }

    [Column("value_code")]
    [StringLength(30)]
    public string ValueCode { get; set; } = string.Empty;

    [Column("order_sl")]
    public int OrderSl { get; set; }

    [Column("value_name")]
    [StringLength(100)]
    public string ValueName { get; set; } = string.Empty;

    [NotMapped]
    public long ValueNo { get => AttributeValueNo; set => AttributeValueNo = value; }

    [NotMapped]
    public string AttributeValue { get => ValueName; set => ValueName = value; }
}

[Table("inv_product_barcode")]
public class InvProductBarcode : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("barcode_no")]
    public long BarcodeNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    [Column("barcode")]
    [StringLength(100)]
    public string Barcode { get; set; } = string.Empty;

    [Column("barcode_type")]
    public short BarcodeType { get; set; } = 1;

    [Column("is_primary")]
    public short IsPrimary { get; set; } = 0;

    [Column("pack_qty")]
    public decimal PackQty { get; set; } = 1m;
}

[Table("inv_product_variant")]
public class InvProductVariant : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("variant_no")]
    public long VariantNo { get; set; }

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_sku")]
    [StringLength(48)]
    public string VariantSku { get; set; } = string.Empty;

    [Column("variant_name")]
    [StringLength(200)]
    public string VariantName { get; set; } = string.Empty;

    [Column("barcode")]
    [StringLength(64)]
    public string? Barcode { get; set; }

    [Column("attr1_value_no")]
    public long? Attr1ValueNo { get; set; }

    [Column("attr2_value_no")]
    public long? Attr2ValueNo { get; set; }

    [Column("attr3_value_no")]
    public long? Attr3ValueNo { get; set; }

    [Column("purchase_price")]
    public decimal? PurchasePrice { get; set; }

    [Column("sale_price")]
    public decimal? SalePrice { get; set; }

    [Column("mrp")]
    public decimal? Mrp { get; set; }

    [Column("image_path")]
    [StringLength(255)]
    public string? ImagePath { get; set; }

    [NotMapped]
    public string VariantCode { get => VariantSku; set => VariantSku = value; }

    [NotMapped]
    public string? Sku { get => VariantSku; set => VariantSku = value ?? string.Empty; }

    [NotMapped]
    public decimal CostPrice { get => PurchasePrice ?? 0m; set => PurchasePrice = value; }

    [NotMapped]
    public decimal SellingPrice { get => SalePrice ?? 0m; set => SalePrice = value; }
}
