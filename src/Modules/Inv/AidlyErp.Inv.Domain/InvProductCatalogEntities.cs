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

    /// <summary>The business key is <c>product_id</c> in the schema, not <c>product_code</c>.</summary>
    [Column("product_id")]
    [StringLength(50)]
    public string ProductId { get; set; } = string.Empty;

    [Column("product_name")]
    [StringLength(250)]
    public string ProductName { get; set; } = string.Empty;

    [Column("category_no")]
    public long CategoryNo { get; set; }

    [Column("brand_no")]
    public long? BrandNo { get; set; }

    [Column("uom_no")]
    public long UomNo { get; set; }

    [Column("cost_price")]
    public decimal CostPrice { get; set; } = 0m;

    [Column("selling_price")]
    public decimal SellingPrice { get; set; } = 0m;

    [Column("mrp")]
    public decimal Mrp { get; set; } = 0m;

    [Column("costing_method")]
    [StringLength(20)]
    public string CostingMethod { get; set; } = "FIFO";

    [Column("tax_rate")]
    public decimal TaxRate { get; set; } = 0m;

    [Column("has_variants")]
    public short HasVariants { get; set; } = 0;

    [Column("has_batches")]
    public short HasBatches { get; set; } = 0;

    [Column("has_expiry")]
    public short HasExpiry { get; set; } = 0;

    [Column("is_stockable")]
    public short IsStockable { get; set; } = 1;

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = "ACTIVE";

    [Column("company_no")]
    public long CompanyNo { get; set; }
}

[Table("inv_category")]
public class InvCategory : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("category_no")]
    public long CategoryNo { get; set; }

    [Column("category_code")]
    [StringLength(30)]
    public string CategoryCode { get; set; } = string.Empty;

    [Column("category_name")]
    [StringLength(150)]
    public string CategoryName { get; set; } = string.Empty;

    [Column("parent_category_no")]
    public long? ParentCategoryNo { get; set; }

    [Column("description")]
    [StringLength(250)]
    public string? Description { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }
}

[Table("inv_brand")]
public class InvBrand : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("brand_no")]
    public long BrandNo { get; set; }

    [Column("brand_code")]
    [StringLength(30)]
    public string BrandCode { get; set; } = string.Empty;

    [Column("brand_name")]
    [StringLength(150)]
    public string BrandName { get; set; } = string.Empty;

    [Column("company_no")]
    public long CompanyNo { get; set; }
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
}

[Table("inv_uom_conversion")]
public class InvUomConversion : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("conversion_no")]
    public long ConversionNo { get; set; }

    [Column("from_uom_no")]
    public long FromUomNo { get; set; }

    [Column("to_uom_no")]
    public long ToUomNo { get; set; }

    [Column("multiplier")]
    public decimal Multiplier { get; set; } = 1m;

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [NotMapped]
    public decimal ConversionFactor { get => Multiplier; set => Multiplier = value; }
}

[Table("inv_product_attribute")]
public class InvProductAttribute : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("attribute_no")]
    public long AttributeNo { get; set; }

    [Column("attribute_code")]
    [StringLength(30)]
    public string? AttributeCode { get; set; }

    [Column("attribute_name")]
    [StringLength(100)]
    public string AttributeName { get; set; } = string.Empty;

    [Column("company_no")]
    public long CompanyNo { get; set; }
}

[Table("inv_product_attribute_value")]
public class InvProductAttributeValue : AuditEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("value_no")]
    public long ValueNo { get; set; }

    [Column("attribute_no")]
    public long AttributeNo { get; set; }

    [Column("value_name")]
    [StringLength(100)]
    public string ValueName { get; set; } = string.Empty;

    [NotMapped]
    public long AttributeValueNo { get => ValueNo; set => ValueNo = value; }

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

    [Column("product_no")]
    public long ProductNo { get; set; }

    [Column("variant_no")]
    public long? VariantNo { get; set; }

    [Column("barcode")]
    [StringLength(100)]
    public string Barcode { get; set; } = string.Empty;

    [Column("barcode_type")]
    [StringLength(30)]
    public string? BarcodeType { get; set; } = "CODE128";

    [Column("is_primary")]
    public short IsPrimary { get; set; } = 0;

    [Column("company_no")]
    public long CompanyNo { get; set; }
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

    [Column("variant_code")]
    [StringLength(50)]
    public string VariantCode { get; set; } = string.Empty;

    [Column("variant_name")]
    [StringLength(200)]
    public string VariantName { get; set; } = string.Empty;

    [Column("sku")]
    [StringLength(100)]
    public string? Sku { get; set; }

    [Column("cost_price")]
    public decimal CostPrice { get; set; } = 0m;

    [Column("selling_price")]
    public decimal SellingPrice { get; set; } = 0m;
}
