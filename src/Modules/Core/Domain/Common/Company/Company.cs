using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Aidly.src.Modules.Core.Domain.Base;

namespace Aidly.src.Modules.Core.Domain.Common.Company;

[Table("sys_company")]
public class Company : BaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("company_no")] // Maps C# CompanyNo to SQL company_no
    public new int CompanyNo { get; set; }

    [Required]
    [Column("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [Column("company_id")]
    public string CompanyId { get; set; } = string.Empty;

    [Required]
    [Column("country")]
    public string Country { get; set; } = string.Empty;

    [Required]
    [Column("address_line1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [Required]
    [Column("address_line2")]
    public string AddressLine2 { get; set; } = string.Empty;

    [Required]
    [Column("city")]
    public string City { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("vat_reg_no")]
    public string? VatRegNo { get; set; }

    [Column("trade_license_no")]
    public string? TradeLicenseNo { get; set; }

    [Column("phone_no")]
    public string? PhoneNo { get; set; }

    [Column("email_address")]
    public string? EmailAddress { get; set; }
}