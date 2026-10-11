using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Contracts;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Browsing events is public. Creating, changing and deleting events is limited to the Organiser who owns them.</summary>
[ApiController]
[Route("api/events")]
public class EventsController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Lists events, optionally filtered by type, date, location, or only the logged-in Organiser's own events.</summary>
    /// <param name="type">Run, Walk or Cycle.</param>
    /// <param name="date">Only events on this calendar day (yyyy-MM-dd).</param>
    /// <param name="location">Part of the location text, for example "Cape Town".</param>
    /// <param name="mine">
    /// When true, returns only the events of the logged-in Organiser. Added beyond the Part 1 plan so the
    /// Organiser dashboard can list its own events. Requires an Organiser session.
    /// </param>
    /// <response code="200">Matching events, earliest first.</response>
    /// <response code="400">Unknown event type.</response>
    /// <response code="401">mine=true but not logged in.</response>
    /// <response code="403">mine=true but logged in as a Participant.</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<EventSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? type,
        [FromQuery] DateOnly? date,
        [FromQuery] string? location,
        [FromQuery] bool mine,
        CancellationToken ct)
    {
        if (type is not null && !EventTypes.All.Contains(type))
            return BadRequest(new ErrorResponse("Event type must be Run, Walk or Cycle."));

        var query = db.Events.AsNoTracking().AsQueryable();

        if (mine)
        {
            var (userId, role) = await ReadSessionAsync(ct);
            if (userId is null) return Unauthorized(new ErrorResponse("You must be logged in to do this."));
            if (role != Roles.Organiser) return Forbid403();

            var organiserId = await FindOrganiserIdAsync(db, userId.Value, ct);
            if (organiserId is null) return Forbid403();
            query = query.Where(e => e.OrganiserId == organiserId.Value);
        }

        if (type is not null) query = query.Where(e => e.EventType == type);

        if (date is not null)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            query = query.Where(e => e.EventDate >= start && e.EventDate < end);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var term = location.Trim();
            query = query.Where(e => e.Location.Contains(term));
        }

        var events = await query
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummaryResponse(
                e.EventId,
                e.Name,
                e.EventDate,
                e.Location,
                e.Distance,
                e.EventType,
                e.BannerImageUrl,
                e.OrganiserId,
                e.Organiser.OrganisationName))
            .ToListAsync(ct);

        return Ok(events);
    }

    /// <summary>Returns one event with its categories.</summary>
    /// <response code="200">The event and its categories.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id, CancellationToken ct)
    {
        var detail = await FindEventDetailAsync(id, ct);
        return detail is null ? NotFound(new ErrorResponse("Event not found.")) : Ok(detail);
    }

    /// <summary>403 response used when a logged-in user's role is not allowed to perform the action.</summary>
    protected ObjectResult Forbid403() =>
        StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Your role does not have access to this action."));

    /// <summary>Loads an event with its organiser and categories, mapped to the response shape. Returns null if it does not exist.</summary>
    protected async Task<EventDetailResponse?> FindEventDetailAsync(int eventId, CancellationToken ct)
    {
        var e = await db.Events
            .AsNoTracking()
            .Include(ev => ev.Organiser)
            .Include(ev => ev.Categories)
            .FirstOrDefaultAsync(ev => ev.EventId == eventId, ct);

        return e is null ? null : ToDetail(e);
    }

    protected static EventDetailResponse ToDetail(Event e) => new(
        e.EventId,
        e.Name,
        e.Description,
        e.EventDate,
        e.Location,
        e.Distance,
        e.EventType,
        e.BannerImageUrl,
        e.OrganiserId,
        e.Organiser.OrganisationName,
        e.Categories
            .OrderBy(c => c.CategoryId)
            .Select(c => new CategoryResponse(c.CategoryId, c.EventId, c.Name, c.Description, c.MinAge, c.MaxAge))
            .ToList());
}
