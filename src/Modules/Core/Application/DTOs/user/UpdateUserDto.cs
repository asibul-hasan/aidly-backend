using System.ComponentModel.DataAnnotations;

namespace Aidly.src.Modules.Core.Application.DTOs;

public record UpdateUserDto(
    string? FullName,
    [EmailAddress] string? Email,
    int? Role,
    bool? IsActive
);
