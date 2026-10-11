namespace RaceDay.Api.Contracts;

/// <summary>Body for POST and PUT /api/events.</summary>
public record EventRequest(
    string Name,
    string Description,
    DateTime Date,
    string Location,
    decimal Distance,
    string EventType,
    string? BannerImageUrl);

/// <summary>One row in the list returned by GET /api/events.</summary>
public record EventSummaryResponse(
    int EventId,
    string Name,
    DateTime Date,
    string Location,
    decimal Distance,
    string EventType,
    string? BannerImageUrl,
    int OrganiserId,
    string OrganisationName);

/// <summary>Full event returned by GET /api/events/{id}, POST, and PUT, including its categories.</summary>
public record EventDetailResponse(
    int EventId,
    string Name,
    string Description,
    DateTime Date,
    string Location,
    decimal Distance,
    string EventType,
    string? BannerImageUrl,
    int OrganiserId,
    string OrganisationName,
    IReadOnlyList<CategoryResponse> Categories);
