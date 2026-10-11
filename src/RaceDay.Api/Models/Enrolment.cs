namespace RaceDay.Api.Models;

/// <summary>Links a participant to an event and the category they entered (Enrolments table).</summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }

    public int EventId { get; set; }

    public int CategoryId { get; set; }

    public DateTime RegistrationDate { get; set; }

    /// <summary><see cref="EnrolmentStatuses"/> value. New enrolments start as Pending.</summary>
    public string Status { get; set; } = EnrolmentStatuses.Pending;

    public Participant Participant { get; set; } = null!;

    public Event Event { get; set; } = null!;

    public Category Category { get; set; } = null!;

    public Result? Result { get; set; }
}
