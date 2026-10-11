namespace RaceDay.Api.Models;

/// <summary>Finish time and position captured by an Organiser for one enrolment (Results table).</summary>
public class Result
{
    public int ResultId { get; set; }

    /// <summary>Unique: each enrolment can have at most one result.</summary>
    public int EnrolmentId { get; set; }

    public TimeSpan FinishTime { get; set; }

    public int FinishPosition { get; set; }

    /// <summary><see cref="ResultStatuses"/> value. New results are Published by default.</summary>
    public string Status { get; set; } = ResultStatuses.Published;

    public Enrolment Enrolment { get; set; } = null!;
}
