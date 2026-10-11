namespace RaceDay.Api.Models;

/// <summary>An age or distance category that a participant selects when entering an event (Categories table).</summary>
public class Category
{
    public int CategoryId { get; set; }

    public int EventId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? MinAge { get; set; }

    /// <summary>Null means the category has no upper age limit (for example "Veteran 40+").</summary>
    public int? MaxAge { get; set; }

    public Event Event { get; set; } = null!;
}
