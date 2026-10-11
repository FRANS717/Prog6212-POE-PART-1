namespace RaceDay.Api.Contracts;

/// <summary>A category (age or distance group) within an event.</summary>
public record CategoryResponse(
    int CategoryId,
    int EventId,
    string Name,
    string? Description,
    int? MinAge,
    int? MaxAge);

/// <summary>Body for POST /api/events/{eventId}/categories and PUT /api/categories/{id}. Ages are optional.</summary>
public record CategoryRequest(string Name, string? Description, int? MinAge, int? MaxAge);
