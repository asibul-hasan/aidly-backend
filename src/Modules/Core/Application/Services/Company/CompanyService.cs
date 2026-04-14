using Aidly.src.Modules.Core.Application.Common;
using Aidly.src.Modules.Core.Application.DTOs.Company;
using Aidly.src.Modules.Core.Domain.Interfaces.Company;

namespace Aidly.src.Modules.Core.Application.Services.Company;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepository;

    public CompanyService(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<Result<IEnumerable<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>>> GetAllCompaniesAsync()
    {
        var companies = await _companyRepository.GetAllAsync();
        var dtos = companies.Select(MapToDto).ToList();
        return Result<IEnumerable<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>>.Success(dtos);
    }

    public async Task<Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>> GetCompanyByIdAsync(int id)
    {
        var company = await _companyRepository.GetByCompanyNoAsync(id);
        if (company == null)
            return Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>.Failure($"Company with ID {id} not found.");

        return Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>.Success(MapToDto(company));
    }

    public async Task<Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>> CreateCompanyAsync(Aidly.src.Modules.Core.Application.DTOs.Company.CreateCompanyDto dto)
    {
        // Check if CompanyId already exists
        var existing = await _companyRepository.GetByCompanyIdAsync(dto.CompanyId);
        if (existing != null)
            return Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>.Failure($"Company with ID '{dto.CompanyId}' already exists.");

        var company = new Aidly.src.Modules.Core.Domain.Common.Company.Company
        {
            CompanyName = dto.CompanyName,
            CompanyId = dto.CompanyId,
            Country = dto.Country,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            IsActive = dto.IsActive,
            VatRegNo = dto.VatRegNo,
            TradeLicenseNo = dto.TradeLicenseNo,
            PhoneNo = dto.PhoneNo,
            EmailAddress = dto.EmailAddress
        };

        await _companyRepository.AddAsync(company);
        await _companyRepository.SaveChangesAsync();

        return Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>.Success(MapToDto(company));
    }

    public async Task<Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>> UpdateCompanyAsync(int id, Aidly.src.Modules.Core.Application.DTOs.Company.UpdateCompanyDto dto)
    {
        var company = await _companyRepository.GetByCompanyNoAsync(id);
        if (company == null)
            return Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>.Failure($"Company with ID {id} not found.");

        // Update fields if provided
        if (!string.IsNullOrEmpty(dto.CompanyName)) company.CompanyName = dto.CompanyName;
        if (!string.IsNullOrEmpty(dto.CompanyId)) company.CompanyId = dto.CompanyId;
        if (!string.IsNullOrEmpty(dto.Country)) company.Country = dto.Country;
        if (!string.IsNullOrEmpty(dto.AddressLine1)) company.AddressLine1 = dto.AddressLine1;
        if (!string.IsNullOrEmpty(dto.AddressLine2)) company.AddressLine2 = dto.AddressLine2;
        if (!string.IsNullOrEmpty(dto.City)) company.City = dto.City;
        if (dto.IsActive.HasValue) company.IsActive = dto.IsActive.Value;
        if (dto.VatRegNo != null) company.VatRegNo = dto.VatRegNo;
        if (dto.TradeLicenseNo != null) company.TradeLicenseNo = dto.TradeLicenseNo;
        if (dto.PhoneNo != null) company.PhoneNo = dto.PhoneNo;
        if (dto.EmailAddress != null) company.EmailAddress = dto.EmailAddress;

        _companyRepository.Update(company);
        await _companyRepository.SaveChangesAsync();

        return Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>.Success(MapToDto(company));
    }

    public async Task<Result<string>> DeleteCompanyAsync(int id)
    {
        var company = await _companyRepository.GetByCompanyNoAsync(id);
        if (company == null)
            return Result<string>.Failure($"Company with ID {id} not found.");

        _companyRepository.Delete(company);
        await _companyRepository.SaveChangesAsync();

        return Result<string>.Success("Company deleted successfully.");
    }

    private static Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto MapToDto(Aidly.src.Modules.Core.Domain.Common.Company.Company company)
    {
        return new Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto(
            company.CompanyNo,
            company.CompanyName,
            company.CompanyId,
            company.Country,
            company.AddressLine1,
            company.AddressLine2,
            company.City,
            company.IsActive,
            company.VatRegNo,
            company.TradeLicenseNo,
            company.PhoneNo,
            company.EmailAddress,
            company.CreatedAt
        );
    }
}