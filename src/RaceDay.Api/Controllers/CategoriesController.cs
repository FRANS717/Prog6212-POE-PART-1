using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Contracts;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Age or distance categories that participants choose from when they enrol in an event.</summary>
[ApiController]
[Route("api")]
public class CategoriesController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Lists the categories of an event (public).</summary>
    /// <response code="200">Categories for the event, in the order they were created.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType<IEnumerable<CategoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int eventId, CancellationToken ct)
    {
        if (!await db.Events.AnyAsync(e => e.EventId == eventId, ct))
            return NotFound(new ErrorResponse("Event not found."));

        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryId)
            .Select(c => new CategoryResponse(c.CategoryId, c.EventId, c.Name, c.Description, c.MinAge, c.MaxAge))
            .ToListAsync(ct);

        return Ok(categories);
    }

    /// <summary>Adds a category to an event owned by the logged-in Organiser.</summary>
    /// <remarks>Example: { "name": "Under 20", "description": "Cyclists under 20", "minAge": 15, "maxAge": 19 }</remarks>
    /// <response code="201">Category created.</response>
    /// <response code="400">Invalid data, such as minAge greater than maxAge.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCategory(int eventId, [FromBody] CategoryRequest request, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));

        if (!await IsOwnerAsync(ev.OrganiserId, ct)) return Forbid403();

        var error = ValidateCategory(request);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        var category = new Category { EventId = eventId };
        ApplyRequest(category, request);

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetCategories), new { eventId }, ToResponse(category));
    }

    /// <summary>Updates a category. Only the Organiser who owns its event may change it.</summary>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No category with this ID.</response>
    [HttpPut("categories/{id:int}")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id, ct);
        if (category is null) return NotFound(new ErrorResponse("Category not found."));

        if (!await IsOwnerAsync(category.Event.OrganiserId, ct)) return Forbid403();

        var error = ValidateCategory(request);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        ApplyRequest(category, request);
        await db.SaveChangesAsync(ct);

        return Ok(ToResponse(category));
    }

    /// <summary>
    /// Deletes a category. A category that participants have already enrolled in cannot be deleted (409),
    /// so their enrolments are not lost.
    /// </summary>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No category with this ID.</response>
    /// <response code="409">Participants are already enrolled in this category.</response>
    [HttpDelete("categories/{id:int}")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var category = await db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id, ct);
        if (category is null) return NotFound(new ErrorResponse("Category not found."));

        if (!await IsOwnerAsync(category.Event.OrganiserId, ct)) return Forbid403();

        if (await db.Enrolments.AnyAsync(en => en.CategoryId == id, ct))
            return Conflict(new ErrorResponse("This category has participant enrolments and cannot be deleted."));

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>True when the logged-in user is the Organiser that owns the given organiser profile.</summary>
    private async Task<bool> IsOwnerAsync(int ownerOrganiserId, CancellationToken ct) =>
        await FindOrganiserIdAsync(db, CurrentUserId, ct) == ownerOrganiserId;

    private static string? ValidateCategory(CategoryRequest request)
    {
        if (Validation.CheckRequiredText(request.Name, "Category name", 50) is { } nameError) return nameError;
        if (request.Description is { Length: > 200 }) return "Description must be 200 characters or fewer.";
        if (request.MinAge is < 0 or > 120 || request.MaxAge is < 0 or > 120)
            return "Ages must be between 0 and 120.";
        if (request.MinAge is not null && request.MaxAge is not null && request.MinAge > request.MaxAge)
            return "Minimum age cannot be greater than maximum age.";
        return null;
    }

    private static void ApplyRequest(Category target, CategoryRequest request)
    {
        target.Name = request.Name.Trim();
        target.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        target.MinAge = request.MinAge;
        target.MaxAge = request.MaxAge;
    }

    private static CategoryResponse ToResponse(Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.Description, c.MinAge, c.MaxAge);
}
