namespace RaceDay.Api.Contracts;

/// <summary>Profile returned by GET and PUT /api/users/profile. Role-specific fields are null for the other role.</summary>
public record UserProfileResponse(
    int UserId,
    string Email,
    string FullName,
    string Role,
    string? OrganisationName,
    string? ContactNumber,
    DateOnly? DateOfBirth,
    string? ProfilePictureUrl);

/// <summary>
/// Body for PUT /api/users/profile. OrganisationName and ContactNumber apply to Organisers,
/// DateOfBirth applies to Participants. ContactNumber is an addition to the Part 1 plan,
/// because the Organisers table requires it.
/// </summary>
public record UpdateProfileRequest(
    string FullName,
    string Email,
    string? OrganisationName,
    string? ContactNumber,
    DateOnly? DateOfBirth);
