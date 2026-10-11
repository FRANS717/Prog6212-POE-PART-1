using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Contracts;

namespace RaceDay.Api.Auth;

/// <summary>
/// Protects an endpoint with the server-side session created at login.
/// No session returns 401. If roles are listed and the session role is not one of them, the request returns 403.
/// Example: [RequireSession(Roles.Organiser)] allows Organisers only.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireSessionAttribute(params string[] roles) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        await session.LoadAsync(context.HttpContext.RequestAborted);

        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || role is null)
        {
            context.Result = new UnauthorizedObjectResult(new ErrorResponse("You must be logged in to do this."));
            return;
        }

        if (roles.Length > 0 && !roles.Contains(role, StringComparer.Ordinal))
        {
            context.Result = new ObjectResult(new ErrorResponse("Your role does not have access to this action."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
