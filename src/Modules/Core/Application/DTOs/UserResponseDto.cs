namespace Aidly.src.Modules.Core.Application.DTOs;

public record UserResponseDto(
    int UserNo,
    string FullName,
    string UserName,
    string Email,
    int Role,
    bool IsActive,
    DateTime? LastLoginAt,
    string? LastLoginIp,
    DateTime CreatedAt
);
