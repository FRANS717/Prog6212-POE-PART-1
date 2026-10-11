namespace RaceDay.Api.Services;

/// <summary>
/// Stores uploaded files and returns the URL that is saved on the database row.
/// Part 2 keeps files on local disk. Part 3 adds an Azure Blob implementation of this same interface.
/// </summary>
public interface IFileStorage
{
    /// <summary>Saves an image in a sub-folder and returns its public URL path, for example /uploads/banners/abc.jpg.</summary>
    Task<string> SaveImageAsync(IFormFile file, string folder, CancellationToken ct);
}
