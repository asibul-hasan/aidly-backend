using System.ComponentModel.DataAnnotations;

namespace Aidly.src.Modules.Core.Application.DTOs.Company;

public record CreateCompanyDto(
    [Required] string CompanyName,
    [Required] string CompanyId,
    [Required] string Country,
    [Required] string AddressLine1,
    [Required] string AddressLine2,
    [Required] string City,
    string? VatRegNo,
    string? TradeLicenseNo,
    string? PhoneNo,
    string? EmailAddress,
    bool IsActive = true
);