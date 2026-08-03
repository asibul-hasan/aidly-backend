using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sys.Domain;

[Table("sys_company")]
public class Company : AuditEntity, ICompanyScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("company_id")]
    [StringLength(15)]
    public string? CompanyId { get; set; }

    [Column("company_name")]
    [StringLength(250)]
    public string CompanyName { get; set; } = string.Empty;

    [Column("company_name_nls")]
    [StringLength(250)]
    public string? CompanyNameNls { get; set; }

    [Column("company_type")]
    [StringLength(50)]
    public string? CompanyType { get; set; }

    [Column("trade_license_no")]
    [StringLength(100)]
    public string? TradeLicenseNo { get; set; }

    [Column("vat_reg_no")]
    [StringLength(100)]
    public string? VatRegNo { get; set; }

    [Column("tin_no")]
    [StringLength(100)]
    public string? TinNo { get; set; }

    [Column("bin_no")]
    [StringLength(100)]
    public string? BinNo { get; set; }

    [Column("reg_no")]
    [StringLength(100)]
    public string? RegNo { get; set; }

    [Column("company_addr1")]
    [StringLength(250)]
    public string? CompanyAddr1 { get; set; }

    [Column("company_addr2")]
    [StringLength(250)]
    public string? CompanyAddr2 { get; set; }

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

    [Column("mobile_no")]
    [StringLength(20)]
    public string? MobileNo { get; set; }

    [Column("contact_no")]
    [StringLength(25)]
    public string? ContactNo { get; set; }

    [Column("email")]
    [StringLength(250)]
    public string? Email { get; set; }

    [Column("website")]
    [StringLength(250)]
    public string? Website { get; set; }

    [Column("logo_path")]
    [StringLength(255)]
    public string? LogoPath { get; set; }

    long? ICompanyScopedEntity.CompanyNo
    {
        get => CompanyNo;
        set => CompanyNo = value ?? 0;
    }

    protected override void NullifyBusinessId()
    {
        CompanyId = null;
    }
}
