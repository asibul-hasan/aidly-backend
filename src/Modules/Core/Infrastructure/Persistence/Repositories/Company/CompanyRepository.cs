using Aidly.src.Modules.Core.Domain.Interfaces.Company;
using Microsoft.EntityFrameworkCore;

namespace Aidly.src.Modules.Core.Infrastructure.Persistence.Repositories.Company;

public class CompanyRepository : ICompanyRepository
{
    private readonly CoreDbContext _context;

    public CompanyRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Aidly.src.Modules.Core.Domain.Common.Company.Company>> GetAllAsync()
    {
        return await _context.Companies.AsNoTracking().ToListAsync();
    }

    public async Task<Aidly.src.Modules.Core.Domain.Common.Company.Company?> GetByCompanyNoAsync(int companyNo)
    {
        return await _context.Companies.FirstOrDefaultAsync(c => c.CompanyNo == companyNo);
    }

    public async Task<Aidly.src.Modules.Core.Domain.Common.Company.Company?> GetByCompanyIdAsync(string companyId)
    {
        return await _context.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
    }

    public async Task AddAsync(Aidly.src.Modules.Core.Domain.Common.Company.Company company)
    {
        await _context.Companies.AddAsync(company);
    }

    public void Update(Aidly.src.Modules.Core.Domain.Common.Company.Company company)
    {
        _context.Companies.Update(company);
    }

    public void Delete(Aidly.src.Modules.Core.Domain.Common.Company.Company company)
    {
        _context.Companies.Remove(company);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}