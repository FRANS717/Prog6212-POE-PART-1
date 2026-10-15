using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Contracts;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout. Login creates the server-side session that protects every other endpoint.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(RaceDayDbContext db, IPasswordService passwords) : ApiControllerBase
{
    /// <summary>Registers a new Organiser or Participant account (public).</summary>
    /// <remarks>
    /// Organisers must send organisationName and contactNumber.
    /// Participants must send dateOfBirth. The role must be "Organiser" or "Participant".
    /// </remarks>
    /// <response code="201">Account created. Returns the new userId, email and role.</response>
    /// <response code="400">Invalid or missing data.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var error = Validation.CheckEmail(request.Email)
            ?? Validation.CheckPassword(request.Password)
            ?? Validation.CheckRequiredText(request.FullName, "Full name", 100);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        if (request.Role is not (Roles.Organiser or Roles.Participant))
            return BadRequest(new ErrorResponse("Role must be either Organiser or Participant."));

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new ErrorResponse("An account with this email already exists."));

        var user = new User
        {
            Email = email,
            PasswordHash = passwords.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Role = request.Role,
            CreatedAt = DateTime.Now
        };

        if (request.Role == Roles.Organiser)
        {
            error = Validation.CheckRequiredText(request.OrganisationName, "Organisation name", 100)
                ?? Validation.CheckRequiredText(request.ContactNumber, "Contact number", 20);
            if (error is not null) return BadRequest(new ErrorResponse(error));

            user.Organiser = new Organiser
            {
                OrganisationName = request.OrganisationName!.Trim(),
                ContactNumber = request.ContactNumber!.Trim()
            };
        }
        else
        {
            error = Validation.CheckDateOfBirth(request.DateOfBirth, DateOnly.FromDateTime(DateTime.Today));
            if (error is not null) return BadRequest(new ErrorResponse(error));

            user.Participant = new Participant { DateOfBirth = request.DateOfBirth!.Value };
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Created("/api/users/profile", new AuthResponse(user.UserId, user.Email, user.Role));
    }

    /// <summary>Checks the credentials and starts a session holding the user's ID and role.</summary>
    /// <response code="200">Logged in. The session cookie is set in the response.</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new ErrorResponse("Email and password are required."));

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same message for a wrong email and a wrong password, so nobody can use this to find out which emails exist.
        if (user is null || !passwords.VerifyPassword(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse("Invalid email or password."));

        // I clear any old session first, then store the user ID and role the protected endpoints check.
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);

        return Ok(new AuthResponse(user.UserId, user.Email, user.Role));
    }

    /// <summary>Ends the current session.</summary>
    /// <response code="200">Logged out.</response>
    /// <response code="401">No active session.</response>
    [HttpPost("logout")]
    [RequireSession]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new MessageResponse("Logged out successfully"));
    }
}
