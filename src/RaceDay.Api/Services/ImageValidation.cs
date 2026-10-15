namespace RaceDay.Api.Services;

/// <summary>
/// Checks an uploaded image before it is stored. The file extension is checked, and the first bytes are
/// checked against the known JPEG, PNG and WEBP signatures so a renamed text file is rejected.
/// </summary>
public static class ImageValidation
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>Returns a message for the client when the file is not an acceptable image, or null when it is.</summary>
    public static async Task<string?> ValidateAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return "Choose an image file to upload.";
        if (file.Length > MaxBytes) return "Image must be 5 MB or smaller.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension)) return "Only JPG, PNG and WEBP images are allowed.";

        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);

        return IsKnownImageHeader(header.AsSpan(0, read)) ? null : "The file is not a valid JPG, PNG or WEBP image.";
    }

    private static bool IsKnownImageHeader(ReadOnlySpan<byte> header)
    {
        // I check the bytes, not just the extension, so a renamed text file gets rejected.
        // JPEG starts with FF D8 FF.
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;

        // PNG starts with 89 50 4E 47.
        if (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;

        // WEBP starts with RIFF and has WEBP at byte 8.
        return header.Length >= 12
            && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
            && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P';
    }
}
