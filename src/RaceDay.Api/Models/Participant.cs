namespace RaceDay.Api.Models;

/// <summary>Participant profile, one-to-one with a User whose role is Participant (Participants table).</summary>
public class Participant
{
    public int ParticipantId { get; set; }

    public int UserId { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
