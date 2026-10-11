namespace RaceDay.Api.Services;

/// <summary>Saves uploads under wwwroot/uploads with a random file name, so users cannot overwrite each other's files.</summary>
public class LocalFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    public async Task<string> SaveImageAsync(IFormFile file, string folder, CancellationToken ct)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, "uploads", folder);
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";

        await using var stream = File.Create(Path.Combine(directory, fileName));
        await file.CopyToAsync(stream, ct);

        return $"/uploads/{folder}/{fileName}";
    }
}
