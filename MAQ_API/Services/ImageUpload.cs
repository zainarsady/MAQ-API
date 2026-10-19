using MAQ_API.Exceptions;

namespace MAQ_API.Services;

public static class ImageUpload
{
    private const long MaxBytes = 5 * 1024 * 1024;

    public static async Task<(byte[] Data, string ContentType)?> ReadAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidImageException("The uploaded file must be an image.");
        if (file.Length > MaxBytes)
            throw new InvalidImageException("The image must be 5 MB or smaller.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return (ms.ToArray(), file.ContentType);
    }
}
