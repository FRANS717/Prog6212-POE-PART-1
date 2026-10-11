namespace RaceDay.Api.Contracts;

/// <summary>Body for POST /api/events/{eventId}/enrol: the category the participant chooses.</summary>
public record EnrolRequest(int CategoryId);

/// <summary>Body for PUT /api/enrolments/{id}/status. Allowed values: Pending, Confirmed, Cancelled.</summary>
public record UpdateEnrolmentStatusRequest(string Status);

/// <summary>An enrolment with the event, category and participant details needed by the Organiser and Participant views.</summary>
public record EnrolmentResponse(
    int EnrolmentId,
    int EventId,
    string EventName,
    DateTime EventDate,
    int CategoryId,
    string CategoryName,
    int ParticipantId,
    string ParticipantName,
    DateTime RegistrationDate,
    string Status);
