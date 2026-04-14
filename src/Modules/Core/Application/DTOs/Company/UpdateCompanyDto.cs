using System.ComponentModel.DataAnnotations;

namespace Aidly.src.Modules.Core.Application.DTOs.Company;

public record UpdateCompanyDto(
    string? CompanyName,
    string? CompanyId,
    string? Country,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? VatRegNo,
    string? TradeLicenseNo,
    string? PhoneNo,
    string? EmailAddress,
    bool? IsActive
);