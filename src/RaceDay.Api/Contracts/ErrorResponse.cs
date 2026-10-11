namespace RaceDay.Api.Contracts;

/// <summary>Standard body returned for every error response (400, 401, 403, 404, 409).</summary>
public record ErrorResponse(string Message);

/// <summary>Standard body for a successful action that has no other data to return.</summary>
public record MessageResponse(string Message);
