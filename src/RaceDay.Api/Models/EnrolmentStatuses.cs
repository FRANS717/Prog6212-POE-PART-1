namespace RaceDay.Api.Models;

/// <summary>Allowed values for Enrolments.Status (matches the SQL CHECK constraint).</summary>
public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Pending, Confirmed, Cancelled];
}
