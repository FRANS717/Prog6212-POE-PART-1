namespace RaceDay.Api.Models;

/// <summary>Allowed values for Events.EventType (matches the SQL CHECK constraint).</summary>
public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";

    public static readonly string[] All = [Run, Walk, Cycle];
}
