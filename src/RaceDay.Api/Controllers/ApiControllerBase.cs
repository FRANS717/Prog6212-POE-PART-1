using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Contracts;
using RaceDay.Api.Data;

namespace RaceDay.Api.Controllers;

/// <summary>Base class that gives controllers easy access to the logged-in user's session values.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The UserId from the session. Only read inside actions protected by <see cref="RequireSessionAttribute"/>.</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    /// <summary>The role from the session (Organiser or Participant).</summary>
    protected string CurrentRole => HttpContext.Session.GetString(SessionKeys.Role)!;

    /// <summary>
    /// Reads the session for actions that are not protected by <see cref="RequireSessionAttribute"/>.
    /// Returns a null UserId when nobody is logged in.
    /// </summary>
    protected async Task<(int? UserId, string? Role)> ReadSessionAsync(CancellationToken ct)
    {
        await HttpContext.Session.LoadAsync(ct);
        return (HttpContext.Session.GetInt32(SessionKeys.UserId), HttpContext.Session.GetString(SessionKeys.Role));
    }

    /// <summary>403 response for a logged-in user whose role is not allowed to perform the action.</summary>
    protected ObjectResult Forbid403() =>
        StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Your role does not have access to this action."));

    /// <summary>Finds the Organiser profile ID that belongs to a user account, or null if there is none.</summary>
    protected static Task<int?> FindOrganiserIdAsync(RaceDayDbContext db, int userId, CancellationToken ct) =>
        db.Organisers
            .Where(o => o.UserId == userId)
            .Select(o => (int?)o.OrganiserId)
            .FirstOrDefaultAsync(ct);
}
