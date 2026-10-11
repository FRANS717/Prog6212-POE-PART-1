using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Contracts;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Lets the logged-in user view and update their own profile, whatever their role.</summary>
[ApiController]
[Route("api/users/profile")]
[RequireSession]
public class UsersController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Returns the profile of the logged-in user.</summary>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var user = await LoadCurrentUserAsync(ct);
        return user is null ? Unauthorized(new ErrorResponse("You must be logged in to do this.")) : Ok(ToProfile(user));
    }

    /// <summary>Updates the logged-in user's name, email and role-specific details. The role cannot be changed.</summary>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="409">Another account already uses this email.</response>
    [HttpPut]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await LoadCurrentUserAsync(ct);
        if (user is null) return Unauthorized(new ErrorResponse("You must be logged in to do this."));

        var error = Validation.CheckEmail(request.Email)
            ?? Validation.CheckRequiredText(request.FullName, "Full name", 100);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        var email = request.Email.Trim().ToLowerInvariant();
        if (email != user.Email && await db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new ErrorResponse("An account with this email already exists."));

        if (user.Organiser is not null)
        {
            error = Validation.CheckRequiredText(request.OrganisationName, "Organisation name", 100)
                ?? Validation.CheckRequiredText(request.ContactNumber, "Contact number", 20);
            if (error is not null) return BadRequest(new ErrorResponse(error));

            user.Organiser.OrganisationName = request.OrganisationName!.Trim();
            user.Organiser.ContactNumber = request.ContactNumber!.Trim();
        }
        else if (user.Participant is not null)
        {
            error = Validation.CheckDateOfBirth(request.DateOfBirth, DateOnly.FromDateTime(DateTime.Today));
            if (error is not null) return BadRequest(new ErrorResponse(error));

            user.Participant.DateOfBirth = request.DateOfBirth!.Value;
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        await db.SaveChangesAsync(ct);

        return Ok(ToProfile(user));
    }

    private Task<User?> LoadCurrentUserAsync(CancellationToken ct) =>
        db.Users
            .Include(u => u.Organiser)
            .Include(u => u.Participant)
            .FirstOrDefaultAsync(u => u.UserId == CurrentUserId, ct);

    private static UserProfileResponse ToProfile(User user) => new(
        user.UserId,
        user.Email,
        user.FullName,
        user.Role,
        user.Organiser?.OrganisationName,
        user.Organiser?.ContactNumber,
        user.Participant?.DateOfBirth,
        user.Participant?.ProfilePictureUrl);
}
