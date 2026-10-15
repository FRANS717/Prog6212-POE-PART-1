using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Contracts;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Browsing events is public. Creating, changing and deleting events is limited to the Organiser who owns them.</summary>
[ApiController]
[Route("api/events")]
public class EventsController(RaceDayDbContext db, IFileStorage storage) : ApiControllerBase
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

    /// <summary>Creates an event owned by the logged-in Organiser.</summary>
    /// <remarks>
    /// Example body: { "name": "Cape Town Cycle Tour", "description": "...", "date": "2026-03-15T07:00:00",
    /// "location": "Cape Town, Western Cape", "distance": 109.00, "eventType": "Cycle", "bannerImageUrl": null }
    /// </remarks>
    /// <response code="201">Event created. Returns the event with an empty category list.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpPost]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent([FromBody] EventRequest request, CancellationToken ct)
    {
        var organiserId = await FindOrganiserIdAsync(db, CurrentUserId, ct);
        if (organiserId is null) return Forbid403();

        var error = ValidateEvent(request);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        var newEvent = new Event
        {
            OrganiserId = organiserId.Value,
            CreatedAt = DateTime.Now
        };
        ApplyRequest(newEvent, request);

        db.Events.Add(newEvent);
        await db.SaveChangesAsync(ct);

        var detail = await FindEventDetailAsync(newEvent.EventId, ct);
        return CreatedAtAction(nameof(GetEvent), new { id = newEvent.EventId }, detail);
    }

    /// <summary>Updates an event. Only the Organiser who created it may change it.</summary>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpPut("{id:int}")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] EventRequest request, CancellationToken ct)
    {
        var existing = await db.Events.FirstOrDefaultAsync(e => e.EventId == id, ct);
        if (existing is null) return NotFound(new ErrorResponse("Event not found."));

        if (!await IsOwnerAsync(existing.OrganiserId, ct)) return Forbid403();

        var error = ValidateEvent(request);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        ApplyRequest(existing, request);
        await db.SaveChangesAsync(ct);

        return Ok(await FindEventDetailAsync(id, ct));
    }

    /// <summary>
    /// Deletes an event. Its categories, enrolments and results are removed with it.
    /// Only the Organiser who created the event may delete it.
    /// </summary>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpDelete("{id:int}")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id, CancellationToken ct)
    {
        var existing = await db.Events.FirstOrDefaultAsync(e => e.EventId == id, ct);
        if (existing is null) return NotFound(new ErrorResponse("Event not found."));

        if (!await IsOwnerAsync(existing.OrganiserId, ct)) return Forbid403();

        // Child rows go first, because the foreign keys block deleting the event while they exist. One save does it all.
        var results = db.Results.Where(r => r.Enrolment.EventId == id);
        var enrolments = db.Enrolments.Where(en => en.EventId == id);
        var categories = db.Categories.Where(c => c.EventId == id);

        db.Results.RemoveRange(results);
        db.Enrolments.RemoveRange(enrolments);
        db.Categories.RemoveRange(categories);
        db.Events.Remove(existing);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>True when the logged-in user is the Organiser that owns the given organiser profile.</summary>
    protected async Task<bool> IsOwnerAsync(int ownerOrganiserId, CancellationToken ct)
    {
        var organiserId = await FindOrganiserIdAsync(db, CurrentUserId, ct);
        return organiserId == ownerOrganiserId;
    }

    /// <summary>Uploads a banner image for an event and saves its URL on the event. Only the event's Organiser may do this.</summary>
    /// <remarks>Send a multipart/form-data request with the file in a field named "image". JPG, PNG and WEBP up to 5 MB.</remarks>
    /// <response code="200">Banner saved. Returns the image URL.</response>
    /// <response code="400">Missing, too large, or not a valid image file.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpPost("{eventId:int}/upload-banner")]
    [RequireSession(Roles.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ImageUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadBanner(int eventId, IFormFile? image, CancellationToken ct)
    {
        var existing = await db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (existing is null) return NotFound(new ErrorResponse("Event not found."));

        if (!await IsOwnerAsync(existing.OrganiserId, ct)) return Forbid403();

        var error = await ImageValidation.ValidateAsync(image, ct);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        var imageUrl = await storage.SaveImageAsync(image!, "banners", ct);
        existing.BannerImageUrl = imageUrl;
        await db.SaveChangesAsync(ct);

        return Ok(new ImageUploadResponse(imageUrl));
    }

    /// <summary>Returns the first validation problem with an event request, or null when it is valid.</summary>
    protected static string? ValidateEvent(EventRequest request) =>
        Validation.CheckRequiredText(request.Name, "Event name", 100)
        ?? Validation.CheckRequiredText(request.Description, "Description", 500)
        ?? Validation.CheckRequiredText(request.Location, "Location", 200)
        ?? (request.Date == default ? "Event date is required." : null)
        ?? (request.Distance <= 0 || request.Distance > 999.99m ? "Distance must be greater than 0 and no more than 999.99 km." : null)
        ?? (EventTypes.All.Contains(request.EventType) ? null : "Event type must be Run, Walk or Cycle.")
        ?? (request.BannerImageUrl is { Length: > 500 } ? "Banner image URL must be 500 characters or fewer." : null);

    protected static void ApplyRequest(Event target, EventRequest request)
    {
        target.Name = request.Name.Trim();
        target.Description = request.Description.Trim();
        target.EventDate = request.Date;
        target.Location = request.Location.Trim();
        target.Distance = request.Distance;
        target.EventType = request.EventType;
        target.BannerImageUrl = string.IsNullOrWhiteSpace(request.BannerImageUrl) ? null : request.BannerImageUrl.Trim();
    }

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
