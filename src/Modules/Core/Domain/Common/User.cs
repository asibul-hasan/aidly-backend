using Aidly.src.Modules.Core.Domain.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aidly.src.Modules.Core.Domain.Common;

[Table("sys_user")]
public class User : BaseEntity
{
    [Key] // This makes UserNo the Primary Key
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // This enables the 1, 2, 3... auto-increment
    public int UserNo { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }

    [Required]
    public int Role { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; } = DateTime.UtcNow;

    public string? LastLoginIp { get; set; }
}