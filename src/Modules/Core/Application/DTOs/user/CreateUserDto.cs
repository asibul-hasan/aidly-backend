using System.ComponentModel.DataAnnotations;

namespace Aidly.src.Modules.Core.Application.DTOs;

public record CreateUserDto(
    [Required] string FullName,
    [Required] string UserName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password,
    int Role = 0
);
