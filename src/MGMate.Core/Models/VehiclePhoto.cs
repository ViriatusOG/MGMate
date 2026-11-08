namespace MGMate.Core.Models;

/// <summary>
/// A picture of the car that the owner uploaded, shown on the dashboard's overview card in place
/// of the drawn top view. One per vehicle; a new upload replaces it.
/// </summary>
public class VehiclePhoto
{
    /// <summary>A photo for a card fits well within this; a phone's full-size shot is cut down first.</summary>
    public const int MaxBytes = 3 * 1024 * 1024;

    public int Id { get; set; }
    public int VehicleId { get; set; }

    /// <summary>image/png, image/jpeg or image/webp, as read from the file itself.</summary>
    public string ContentType { get; set; } = "";

    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The image type the bytes start with, or null for anything that is not a PNG, JPEG or WebP.
    /// The file decides, not the header the browser sent, so nothing else is ever served as an image.
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (data.StartsWith((byte[])[0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
