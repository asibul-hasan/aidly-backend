using Aidly.src.Modules.Core.Application.Common;
using Aidly.src.Modules.Core.Application.DTOs;
using Aidly.src.Modules.Core.Domain.Common;
using Aidly.src.Modules.Core.Domain.Interfaces;
using BCrypt.Net;

namespace Aidly.src.Modules.Core.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<IEnumerable<UserResponseDto>>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        var dtos = users.Select(MapToDto).ToList();
        return Result<IEnumerable<UserResponseDto>>.Success(dtos);
    }

    public async Task<Result<UserResponseDto>> GetUserByIdAsync(int id)
    {
        var user = await _userRepository.GetByUserNoAsync(id);
        if (user == null)
            return Result<UserResponseDto>.Failure($"User with ID {id} not found.");

        return Result<UserResponseDto>.Success(MapToDto(user));
    }

    public async Task<Result<UserResponseDto>> CreateUserAsync(CreateUserDto dto)
    {
        // Simple check to see if email exists could be added here
        
        var user = new User
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

        return Result<UserResponseDto>.Success(MapToDto(user));
    }

    public async Task<Result<UserResponseDto>> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _userRepository.GetByUserNoAsync(id);
        if (user == null)
            return Result<UserResponseDto>.Failure($"User with ID {id} not found.");

        if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName;
        if (!string.IsNullOrWhiteSpace(dto.Email)) user.Email = dto.Email.ToLowerInvariant();
        if (dto.Role.HasValue) user.Role = dto.Role.Value;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return Result<UserResponseDto>.Success(MapToDto(user));
    }

    public async Task<Result<string>> DeleteUserAsync(int id)
    {
        var user = await _userRepository.GetByUserNoAsync(id);
        if (user == null)
            return Result<string>.Failure($"User with ID {id} not found.");

        // Hard delete for simplicity. Typically you'd set IsDeleted = true
        user.IsDeleted = true;
        _userRepository.Update(user);
        // or _userRepository.Delete(user); depending on your strategy
        
        await _userRepository.SaveChangesAsync();

        return Result<string>.Success("User deleted successfully.");
    }

    private static UserResponseDto MapToDto(User user)
    {
        return new UserResponseDto(
            UserNo: user.UserNo,
            FullName: user.FullName,
            UserName: user.UserName,
            Email: user.Email,
            Role: user.Role,
            IsActive: user.IsActive,
            LastLoginAt: user.LastLoginAt,
            LastLoginIp: user.LastLoginIp,
            CreatedAt: user.CreatedAt
        );
    }
}
