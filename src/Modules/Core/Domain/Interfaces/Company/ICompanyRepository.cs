namespace Aidly.src.Modules.Core.Domain.Interfaces.Company;

public interface ICompanyRepository
{
    Task<IEnumerable<Aidly.src.Modules.Core.Domain.Common.Company.Company>> GetAllAsync();

    Task<Aidly.src.Modules.Core.Domain.Common.Company.Company?> GetByCompanyNoAsync(int companyNo);

    Task<Aidly.src.Modules.Core.Domain.Common.Company.Company?> GetByCompanyIdAsync(string companyId);

    Task AddAsync(Aidly.src.Modules.Core.Domain.Common.Company.Company company);

    void Update(Aidly.src.Modules.Core.Domain.Common.Company.Company company);

    void Delete(Aidly.src.Modules.Core.Domain.Common.Company.Company company);

    Task<int> SaveChangesAsync();
}