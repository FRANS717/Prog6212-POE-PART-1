namespace RaceDay.Api.Models;

/// <summary>Login account for every person using RaceDay (maps to the Users table).</summary>
public class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the password. The plain-text password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary><see cref="Roles"/> value: Organiser or Participant.</summary>
    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Organiser? Organiser { get; set; }

    public Participant? Participant { get; set; }
}
