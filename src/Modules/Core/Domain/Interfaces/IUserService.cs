using Aidly.src.Modules.Core.Application.Common;
using Aidly.src.Modules.Core.Application.DTOs;

namespace Aidly.src.Modules.Core.Domain.Interfaces;

public interface IUserService
{
    Task<Result<IEnumerable<UserResponseDto>>> GetAllUsersAsync();
    Task<Result<UserResponseDto>>              GetUserByIdAsync(int id);
    Task<Result<UserResponseDto>>              CreateUserAsync(CreateUserDto dto);
    Task<Result<UserResponseDto>>              UpdateUserAsync(int id, UpdateUserDto dto);
    Task<Result<string>>                       DeleteUserAsync(int id);
}