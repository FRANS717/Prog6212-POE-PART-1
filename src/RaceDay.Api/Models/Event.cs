namespace RaceDay.Api.Models;

/// <summary>A road running, walking or cycling event created by an Organiser (Events table).</summary>
public class Event
{
    public int EventId { get; set; }

    public int OrganiserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime EventDate { get; set; }

    public string Location { get; set; } = string.Empty;

    /// <summary>Distance in kilometres, stored as DECIMAL(5,2).</summary>
    public decimal Distance { get; set; }

    /// <summary><see cref="EventTypes"/> value: Run, Walk or Cycle.</summary>
    public string EventType { get; set; } = string.Empty;

    public string? BannerImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public Organiser Organiser { get; set; } = null!;

    public ICollection<Category> Categories { get; set; } = new List<Category>();

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
