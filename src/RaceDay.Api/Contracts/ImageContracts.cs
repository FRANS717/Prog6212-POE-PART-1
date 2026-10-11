namespace RaceDay.Api.Contracts;

/// <summary>Response from the banner and profile picture uploads: the URL the image can be shown from.</summary>
public record ImageUploadResponse(string ImageUrl);
