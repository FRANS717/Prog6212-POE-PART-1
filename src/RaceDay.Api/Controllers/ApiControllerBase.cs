using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;

namespace RaceDay.Api.Controllers;

/// <summary>Base class that gives controllers easy access to the logged-in user's session values.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The UserId from the session. Only read inside actions protected by <see cref="RequireSessionAttribute"/>.</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    /// <summary>The role from the session (Organiser or Participant).</summary>
    protected string CurrentRole => HttpContext.Session.GetString(SessionKeys.Role)!;
}
