namespace Aidly.src.Modules.Core.Domain.Interfaces.User;

public interface IUserRepository
{
    Task<IEnumerable<Aidly.src.Modules.Core.Domain.Common.User.User>> GetAllAsync();

    Task<Aidly.src.Modules.Core.Domain.Common.User.User?> GetByUserNoAsync(int userNo);

    Task<Aidly.src.Modules.Core.Domain.Common.User.User?> GetByUserIdAsync(string userId);

    Task AddAsync(Aidly.src.Modules.Core.Domain.Common.User.User user);

    void Update(Aidly.src.Modules.Core.Domain.Common.User.User user);

    void Delete(Aidly.src.Modules.Core.Domain.Common.User.User user);

    Task<int> SaveChangesAsync();
}