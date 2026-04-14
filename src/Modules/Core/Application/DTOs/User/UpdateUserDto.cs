using System.ComponentModel.DataAnnotations;

namespace Aidly.src.Modules.Core.Application.DTOs.User;

public record UpdateUserDto(
    string? FullName,
    [EmailAddress] string? Email,
    int? Role,
    bool? IsActive
);