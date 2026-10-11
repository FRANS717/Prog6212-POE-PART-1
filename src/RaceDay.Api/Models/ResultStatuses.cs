namespace RaceDay.Api.Models;

/// <summary>Allowed values for Results.Status (matches the SQL CHECK constraint).</summary>
public static class ResultStatuses
{
    public const string Published = "Published";
    public const string Pending = "Pending";
    public const string Disqualified = "Disqualified";
}
