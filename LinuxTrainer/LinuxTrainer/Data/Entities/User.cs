using System.ComponentModel.DataAnnotations;

namespace LinuxTrainer.Data.Entities;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Login { get; set; } = "";

    [Required]
    public string PasswordHash { get; set; } = "";

    [Required, MaxLength(32)]
    public string Role { get; set; } = Roles.Student;

    [MaxLength(128)]
    public string DisplayName { get; set; } = "";

    public string? AvatarApprovedBase64 { get; set; }
    public string? AvatarPendingBase64 { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
}
