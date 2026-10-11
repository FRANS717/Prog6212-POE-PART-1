namespace RaceDay.Api.Models;

/// <summary>Organiser profile, one-to-one with a User whose role is Organiser (Organisers table).</summary>
public class Organiser
{
    public int OrganiserId { get; set; }

    public int UserId { get; set; }

    public string OrganisationName { get; set; } = string.Empty;

    public string ContactNumber { get; set; } = string.Empty;

    public User User { get; set; } = null!;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
