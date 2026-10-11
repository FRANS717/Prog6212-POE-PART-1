using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Contracts;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Participants enter events by choosing a category. Organisers review and confirm the enrolments for their events.</summary>
[ApiController]
[Route("api")]
public class EnrolmentsController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>
    /// Lists enrolments for the logged-in user. Organisers see every enrolment for their events.
    /// Participants see only their own enrolments.
    /// </summary>
    /// <response code="200">The enrolments visible to this user.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("enrolments")]
    [RequireSession]
    [ProducesResponseType<IEnumerable<EnrolmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyEnrolments(CancellationToken ct)
    {
        IQueryable<Enrolment> source = db.Enrolments.AsNoTracking();

        if (CurrentRole == Roles.Organiser)
        {
            var organiserId = await FindOrganiserIdAsync(db, CurrentUserId, ct);
            if (organiserId is null) return Forbid403();
            source = source.Where(en => en.Event.OrganiserId == organiserId.Value);
        }
        else
        {
            var participantId = await FindParticipantIdAsync(CurrentUserId, ct);
            if (participantId is null) return Forbid403();
            source = source.Where(en => en.ParticipantId == participantId.Value);
        }

        // Order the entity query before projecting: SQL Server cannot translate an OrderBy applied after this projection.
        var enrolments = await Project(source.OrderBy(en => en.Event.EventDate)).ToListAsync(ct);
        return Ok(enrolments);
    }

    /// <summary>Lists every enrolment for one event. Only the event's Organiser may view them.</summary>
    /// <response code="200">Enrolments for the event.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<IEnumerable<EnrolmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int eventId, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));

        if (!await IsOwnerAsync(ev.OrganiserId, ct)) return Forbid403();

        var enrolments = await Project(
                db.Enrolments.AsNoTracking()
                    .Where(en => en.EventId == eventId)
                    .OrderBy(en => en.RegistrationDate))
            .ToListAsync(ct);

        return Ok(enrolments);
    }

    /// <summary>
    /// Enrols the logged-in Participant in an event under one of its categories. The category must belong to the event,
    /// the participant's age on the event date must fit the category, and a participant can enrol only once per event.
    /// </summary>
    /// <remarks>Example: { "categoryId": 2 }</remarks>
    /// <response code="201">Enrolment recorded with status Pending.</response>
    /// <response code="400">Already enrolled, category does not belong to the event, or age outside the category.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpPost("events/{eventId:int}/enrol")]
    [RequireSession(Roles.Participant)]
    [ProducesResponseType<EnrolmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enrol(int eventId, [FromBody] EnrolRequest request, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return NotFound(new ErrorResponse("Event not found."));

        var participant = await db.Participants.FirstOrDefaultAsync(p => p.UserId == CurrentUserId, ct);
        if (participant is null) return Forbid403();

        var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId, ct);
        if (category is null || category.EventId != eventId)
            return BadRequest(new ErrorResponse("The selected category does not belong to this event."));

        if (await db.Enrolments.AnyAsync(en => en.EventId == eventId && en.ParticipantId == participant.ParticipantId, ct))
            return BadRequest(new ErrorResponse("You are already enrolled in this event."));

        var age = AgeOn(participant.DateOfBirth, ev.EventDate);
        if ((category.MinAge is not null && age < category.MinAge) || (category.MaxAge is not null && age > category.MaxAge))
            return BadRequest(new ErrorResponse($"Your age on the event date ({age}) is outside this category's range."));

        var enrolment = new Enrolment
        {
            ParticipantId = participant.ParticipantId,
            EventId = eventId,
            CategoryId = category.CategoryId,
            RegistrationDate = DateTime.Now,
            Status = EnrolmentStatuses.Pending
        };

        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync(ct);

        var created = await ProjectOne(enrolment.EnrolmentId, ct);
        return CreatedAtAction(nameof(GetMyEnrolments), created);
    }

    /// <summary>Changes an enrolment's status to Pending, Confirmed or Cancelled. Only the event's Organiser may do this.</summary>
    /// <remarks>Example: { "status": "Confirmed" }</remarks>
    /// <response code="200">The updated enrolment.</response>
    /// <response code="400">Status is not Pending, Confirmed or Cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or not the event's organiser.</response>
    /// <response code="404">No enrolment with this ID.</response>
    [HttpPut("enrolments/{id:int}/status")]
    [RequireSession(Roles.Organiser)]
    [ProducesResponseType<EnrolmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateEnrolmentStatusRequest request, CancellationToken ct)
    {
        var enrolment = await db.Enrolments.Include(en => en.Event).FirstOrDefaultAsync(en => en.EnrolmentId == id, ct);
        if (enrolment is null) return NotFound(new ErrorResponse("Enrolment not found."));

        if (!await IsOwnerAsync(enrolment.Event.OrganiserId, ct)) return Forbid403();

        if (!EnrolmentStatuses.All.Contains(request.Status))
            return BadRequest(new ErrorResponse("Status must be Pending, Confirmed or Cancelled."));

        enrolment.Status = request.Status;
        await db.SaveChangesAsync(ct);

        return Ok(await ProjectOne(id, ct));
    }

    private Task<EnrolmentResponse> ProjectOne(int enrolmentId, CancellationToken ct) =>
        Project(db.Enrolments.AsNoTracking().Where(en => en.EnrolmentId == enrolmentId)).FirstAsync(ct);

    /// <summary>Maps enrolments to the response shape, including the event, category and participant names.</summary>
    private static IQueryable<EnrolmentResponse> Project(IQueryable<Enrolment> source) =>
        source
            .Select(en => new EnrolmentResponse(
                en.EnrolmentId,
                en.EventId,
                en.Event.Name,
                en.Event.EventDate,
                en.CategoryId,
                en.Category.Name,
                en.ParticipantId,
                en.Participant.User.FullName,
                en.RegistrationDate,
                en.Status));

    private async Task<bool> IsOwnerAsync(int ownerOrganiserId, CancellationToken ct) =>
        await FindOrganiserIdAsync(db, CurrentUserId, ct) == ownerOrganiserId;

    private Task<int?> FindParticipantIdAsync(int userId, CancellationToken ct) =>
        db.Participants
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.ParticipantId)
            .FirstOrDefaultAsync(ct);

    /// <summary>Whole years between the date of birth and the given date.</summary>
    private static int AgeOn(DateOnly dateOfBirth, DateTime onDate)
    {
        var date = DateOnly.FromDateTime(onDate);
        var age = date.Year - dateOfBirth.Year;
        if (date < dateOfBirth.AddYears(age)) age--;
        return age;
    }
}
