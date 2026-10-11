namespace RaceDay.Api.Contracts;

/// <summary>Body for POST /api/auth/register. OrganisationName, ContactNumber and DateOfBirth depend on the role.</summary>
public record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Role,
    string? OrganisationName,
    string? ContactNumber,
    DateOnly? DateOfBirth);

/// <summary>Body for POST /api/auth/login.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Response for register and login: the identity and role stored in the session.</summary>
public record AuthResponse(int UserId, string Email, string Role);
