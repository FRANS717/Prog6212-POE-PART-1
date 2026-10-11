namespace RaceDay.Api.Services;

/// <summary>
/// BCrypt password hashing. BCrypt generates a random salt for every hash, so two users with
/// the same password still get different stored values. The work factor is set to 12.
/// </summary>
public class PasswordService : IPasswordService
{
    private const int WorkFactor = 12;

    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);

    public bool VerifyPassword(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
}
