namespace RaceDay.Api.Services;

/// <summary>Hashes passwords before they are stored and checks login attempts against the stored hash.</summary>
public interface IPasswordService
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
