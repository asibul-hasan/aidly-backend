using Aidly.src.Modules.Core.Domain.Interfaces;
using Aidly.src.Modules.Core.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Aidly.src.Modules.Core.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly CoreDbContext _context;

    public UserRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users.AsNoTracking().ToListAsync();
    }

    public async Task<User?> GetByUserNoAsync(int userNo)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.UserNo == userNo);
    }

    public async Task<User?> GetByUserIdAsync(string userId)
    {
        // UserId has been removed from User.cs. Throwing exception to satisfy interface.
        throw new NotImplementedException("UserId property no longer exists on User entity.");
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public void Update(User user)
    {
        _context.Users.Update(user);
    }

    public void Delete(User user)
    {
        _context.Users.Remove(user);
    }

    public async Task<int> SaveChangesAsync()
    {
        // Automatically handle BaseEntity properties if needed
        foreach (var entry in _context.ChangeTracker.Entries<Aidly.src.Modules.Core.Domain.Base.BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        return await _context.SaveChangesAsync();
    }
}
