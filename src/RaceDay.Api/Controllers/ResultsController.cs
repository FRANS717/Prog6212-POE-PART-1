using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Contracts;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Organisers capture finish times and positions after an event. Participants read their own results.</summary>
[ApiController]
[Route("api")]
public class ResultsController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Returns the logged-in Participant's race history across all events they entered.</summary>
    /// <response code="200">The participant's results, most recent event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("results/me")]
    [RequireSession(Roles.Participant)]
    [ProducesResponseType<IEnumerable<ResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults(CancellationToken ct)
    {
        var participantId = await db.Participants
            .Where(p => p.UserId == CurrentUserId)
            .Select(p => (int?)p.ParticipantId)
            .FirstOrDefaultAsync(ct);
        if (participantId is null) return Forbid403();

        var results = await LoadResultsAsync(
            db.Results.AsNoTracking().Where(r => r.Enrolment.ParticipantId == participantId.Value), ct);

        return Ok(results.OrderByDescending(r => r.EventDate).ToList());
    }

    /// <summary>Returns every captured result for one event. Only the event's Organiser may view them.</summary>
    /// <response code="200">Results for the event, by finishing position.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpGet("events/{eventId:int}/results")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<IEnumerable<ResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int eventId, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));

        if (!await IsOwnerAsync(ev.OrganiserId, ct)) return Forbid403();

        var results = await LoadResultsAsync(
            db.Results.AsNoTracking().Where(r => r.Enrolment.EventId == eventId), ct);

        return Ok(results.OrderBy(r => r.FinishPosition).ToList());
    }

    /// <summary>Records the finish time and position for one enrolment. Each enrolment can have only one result.</summary>
    /// <remarks>Example: { "enrolmentId": 1, "finishTime": "03:45:30", "finishPosition": 47 }</remarks>
    /// <response code="201">Result recorded with status Published.</response>
    /// <response code="400">Invalid time or position, or a result already exists for this enrolment.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No event with this ID, or the enrolment is not part of this event.</response>
    [HttpPost("events/{eventId:int}/results")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<ResultResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CaptureResult(int eventId, [FromBody] CaptureResultRequest request, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));

        if (!await IsOwnerAsync(ev.OrganiserId, ct)) return Forbid403();

        var error = ValidateResult(request.FinishTime, request.FinishPosition);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        var enrolment = await db.Enrolments.AsNoTracking()
            .FirstOrDefaultAsync(en => en.EnrolmentId == request.EnrolmentId && en.EventId == eventId, ct);
        if (enrolment is null) return NotFound(new ErrorResponse("Enrolment not found for this event."));

        if (await db.Results.AnyAsync(r => r.EnrolmentId == enrolment.EnrolmentId, ct))
            return BadRequest(new ErrorResponse("A result has already been captured for this enrolment. Use PUT to change it."));

        var result = new Result
        {
            EnrolmentId = enrolment.EnrolmentId,
            FinishTime = request.FinishTime,
            FinishPosition = request.FinishPosition,
            Status = ResultStatuses.Published
        };
        db.Results.Add(result);
        await db.SaveChangesAsync(ct);

        var created = (await LoadResultsAsync(
            db.Results.AsNoTracking().Where(r => r.ResultId == result.ResultId), ct)).Single();

        return CreatedAtAction(nameof(GetEventResults), new { eventId }, created);
    }

    /// <summary>Changes the finish time or position of an existing result. Only the event's Organiser may do this.</summary>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Invalid time or position.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No result with this ID.</response>
    [HttpPut("results/{id:int}")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<ResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResult(int id, [FromBody] UpdateResultRequest request, CancellationToken ct)
    {
        var result = await db.Results
            .Include(r => r.Enrolment)
            .ThenInclude(en => en.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id, ct);
        if (result is null) return NotFound(new ErrorResponse("Result not found."));

        if (!await IsOwnerAsync(result.Enrolment.Event.OrganiserId, ct)) return Forbid403();

        var error = ValidateResult(request.FinishTime, request.FinishPosition);
        if (error is not null) return BadRequest(new ErrorResponse(error));

        result.FinishTime = request.FinishTime;
        result.FinishPosition = request.FinishPosition;
        await db.SaveChangesAsync(ct);

        var updated = (await LoadResultsAsync(
            db.Results.AsNoTracking().Where(r => r.ResultId == id), ct)).Single();

        return Ok(updated);
    }

    private async Task<bool> IsOwnerAsync(int ownerOrganiserId, CancellationToken ct) =>
        await FindOrganiserIdAsync(db, CurrentUserId, ct) == ownerOrganiserId;

    /// <summary>Returns the first problem with a finish time or position, or null when both are valid.</summary>
    private static string? ValidateResult(TimeSpan finishTime, int finishPosition)
    {
        if (finishTime <= TimeSpan.Zero || finishTime >= TimeSpan.FromHours(24))
            return "Finish time must be between 00:00:01 and 23:59:59.";
        if (finishPosition < 1)
            return "Finish position must be 1 or more.";
        return null;
    }

    /// <summary>Loads results with event, category and participant details, and adds the number of finishers for each event.</summary>
    private async Task<List<ResultResponse>> LoadResultsAsync(IQueryable<Result> source, CancellationToken ct)
    {
        var rows = await source
            .Select(r => new
            {
                r.ResultId,
                r.EnrolmentId,
                r.Enrolment.EventId,
                EventName = r.Enrolment.Event.Name,
                EventDate = r.Enrolment.Event.EventDate,
                CategoryName = r.Enrolment.Category.Name,
                ParticipantName = r.Enrolment.Participant.User.FullName,
                r.FinishTime,
                r.FinishPosition,
                r.Status
            })
            .ToListAsync(ct);

        var eventIds = rows.Select(r => r.EventId).Distinct().ToList();
        var finishers = await db.Results.AsNoTracking()
            .Where(r => eventIds.Contains(r.Enrolment.EventId))
            .GroupBy(r => r.Enrolment.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, ct);

        return rows.Select(r => new ResultResponse(
            r.ResultId,
            r.EnrolmentId,
            r.EventId,
            r.EventName,
            r.EventDate,
            r.CategoryName,
            r.ParticipantName,
            r.FinishTime,
            r.FinishPosition,
            finishers.GetValueOrDefault(r.EventId),
            r.Status)).ToList();
    }
}
