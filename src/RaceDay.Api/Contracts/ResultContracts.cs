namespace RaceDay.Api.Contracts;

/// <summary>Body for POST /api/events/{eventId}/results. FinishTime uses the hh:mm:ss format, for example "03:45:30".</summary>
public record CaptureResultRequest(int EnrolmentId, TimeSpan FinishTime, int FinishPosition);

/// <summary>Body for PUT /api/results/{id}.</summary>
public record UpdateResultRequest(TimeSpan FinishTime, int FinishPosition);

/// <summary>
/// A captured result. TotalFinishers is the number of results recorded for the event,
/// so a page can show "47th out of 312 finishers".
/// </summary>
public record ResultResponse(
    int ResultId,
    int EnrolmentId,
    int EventId,
    string EventName,
    DateTime EventDate,
    string CategoryName,
    string ParticipantName,
    TimeSpan FinishTime,
    int FinishPosition,
    int TotalFinishers,
    string Status);
