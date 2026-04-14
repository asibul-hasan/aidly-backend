using Aidly.src.Modules.Core.Domain.Interfaces.User;
using Microsoft.EntityFrameworkCore;

namespace Aidly.src.Modules.Core.Infrastructure.Persistence.Repositories.User;

public class UserRepository : IUserRepository
{
    private readonly CoreDbContext _context;

    public UserRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Aidly.src.Modules.Core.Domain.Common.User.User>> GetAllAsync()
    {
        return await _context.Users.AsNoTracking().ToListAsync();
    }

    public async Task<Aidly.src.Modules.Core.Domain.Common.User.User?> GetByUserNoAsync(int userNo)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.UserNo == userNo);
    }

    public async Task<Aidly.src.Modules.Core.Domain.Common.User.User?> GetByUserIdAsync(string userId)
    {
        // UserId has been removed from User.cs. Throwing exception to satisfy interface.
        throw new NotImplementedException("UserId property no longer exists on User entity.");
    }

    public async Task AddAsync(Aidly.src.Modules.Core.Domain.Common.User.User user)
    {
        await _context.Users.AddAsync(user);
    }

    public void Update(Aidly.src.Modules.Core.Domain.Common.User.User user)
    {
        _context.Users.Update(user);
    }

    public void Delete(Aidly.src.Modules.Core.Domain.Common.User.User user)
    {
        _context.Users.Remove(user);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}