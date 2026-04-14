using Aidly.src.Modules.Core.Application.Common;

namespace Aidly.src.Modules.Core.Domain.Interfaces.Company;

public interface ICompanyService
{
    Task<Result<IEnumerable<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>>> GetAllCompaniesAsync();
    Task<Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>>              GetCompanyByIdAsync(int id);
    Task<Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>>              CreateCompanyAsync(Aidly.src.Modules.Core.Application.DTOs.Company.CreateCompanyDto dto);
    Task<Result<Aidly.src.Modules.Core.Application.DTOs.Company.CompanyResponseDto>>              UpdateCompanyAsync(int id, Aidly.src.Modules.Core.Application.DTOs.Company.UpdateCompanyDto dto);
    Task<Result<string>>                          DeleteCompanyAsync(int id);
}