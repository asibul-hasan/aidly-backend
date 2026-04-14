using Aidly.src.Modules.Core.Application.Common;

namespace Aidly.src.Modules.Core.Domain.Interfaces.User;

public interface IUserService
{
    Task<Result<IEnumerable<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>>> GetAllUsersAsync();
    Task<Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>>              GetUserByIdAsync(int id);
    Task<Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>>              CreateUserAsync(Aidly.src.Modules.Core.Application.DTOs.User.CreateUserDto dto);
    Task<Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>>              UpdateUserAsync(int id, Aidly.src.Modules.Core.Application.DTOs.User.UpdateUserDto dto);
    Task<Result<string>>                       DeleteUserAsync(int id);
}