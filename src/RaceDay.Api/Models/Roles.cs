namespace RaceDay.Api.Models;

/// <summary>
/// The two system roles. The string values are stored in Users.Role and
/// checked by the SQL CHECK constraint, so they must not change.
/// </summary>
public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}
