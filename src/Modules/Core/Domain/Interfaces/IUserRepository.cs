using Aidly.src.Modules.Core.Domain.Common;

namespace Aidly.src.Modules.Core.Domain.Interfaces;

public interface IUserRepository
{
    Task<IEnumerable<User>> GetAllAsync();

    Task<User?> GetByUserNoAsync(int userNo);

    Task<User?> GetByUserIdAsync(string userId);

    Task AddAsync(User user);

    void Update(User user);

    void Delete(User user);

    Task<int> SaveChangesAsync();
}