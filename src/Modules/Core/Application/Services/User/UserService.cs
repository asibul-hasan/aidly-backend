using Aidly.src.Modules.Core.Application.Common;
using Aidly.src.Modules.Core.Application.DTOs.User;
using Aidly.src.Modules.Core.Domain.Interfaces.User;
using BCrypt.Net;

namespace Aidly.src.Modules.Core.Application.Services.User;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<IEnumerable<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        var dtos = users.Select(MapToDto).ToList();
        return Result<IEnumerable<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>>.Success(dtos);
    }

    public async Task<Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>> GetUserByIdAsync(int id)
    {
        var user = await _userRepository.GetByUserNoAsync(id);
        if (user == null)
            return Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>.Failure($"User with ID {id} not found.");

        return Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>.Success(MapToDto(user));
    }

    public async Task<Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>> CreateUserAsync(Aidly.src.Modules.Core.Application.DTOs.User.CreateUserDto dto)
    {
        // Simple check to see if email exists could be added here
        
        var user = new Aidly.src.Modules.Core.Domain.Common.User.User
        {
            FullName = dto.FullName,
            UserName = dto.UserName,
            Email = dto.Email.ToLowerInvariant(),
            Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            IsActive = true
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>.Success(MapToDto(user));
    }

    public async Task<Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>> UpdateUserAsync(int id, Aidly.src.Modules.Core.Application.DTOs.User.UpdateUserDto dto)
    {
        var user = await _userRepository.GetByUserNoAsync(id);
        if (user == null)
            return Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>.Failure($"User with ID {id} not found.");

        // Update fields if provided
        if (!string.IsNullOrEmpty(dto.FullName)) user.FullName = dto.FullName;
        if (!string.IsNullOrEmpty(dto.Email)) user.Email = dto.Email.ToLowerInvariant();
        if (dto.Role.HasValue) user.Role = dto.Role.Value;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return Result<Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto>.Success(MapToDto(user));
    }

    public async Task<Result<string>> DeleteUserAsync(int id)
    {
        var user = await _userRepository.GetByUserNoAsync(id);
        if (user == null)
            return Result<string>.Failure($"User with ID {id} not found.");

        _userRepository.Delete(user);
        await _userRepository.SaveChangesAsync();

        return Result<string>.Success("User deleted successfully.");
    }

    private static Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto MapToDto(Aidly.src.Modules.Core.Domain.Common.User.User user)
    {
        return new Aidly.src.Modules.Core.Application.DTOs.User.UserResponseDto(
            user.UserNo,
            user.FullName,
            user.UserName,
            user.Email,
            user.Role,
            user.IsActive,
            user.LastLoginAt,
            user.LastLoginIp,
            user.CreatedAt
        );
    }
}