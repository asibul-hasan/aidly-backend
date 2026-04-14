namespace Aidly.src.Modules.Core.Application.DTOs.Company;

public record CompanyResponseDto(
    int CompanyNo,
    string CompanyName,
    string CompanyId,
    string Country,
    string AddressLine1,
    string AddressLine2,
    string City,
    bool IsActive,
    string? VatRegNo,
    string? TradeLicenseNo,
    string? PhoneNo,
    string? EmailAddress,
    DateTime CreatedAt
);