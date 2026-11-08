using MGMate.Core.Models;

namespace MGMate.Tests;

public class VehiclePhotoTests
{
    [Fact]
    public void DetectContentType_ReadsTheTypeFromTheBytes()
    {
        Assert.Equal("image/png", VehiclePhoto.DetectContentType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]));
        Assert.Equal("image/jpeg", VehiclePhoto.DetectContentType([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal("image/webp", VehiclePhoto.DetectContentType("RIFF\u0010\0\0\0WEBPVP8 "u8));
    }

    [Fact]
    public void DetectContentType_RefusesAnythingElse()
    {
        Assert.Null(VehiclePhoto.DetectContentType("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8));
        Assert.Null(VehiclePhoto.DetectContentType("RIFF\0\0\0\0WAVE"u8));
        Assert.Null(VehiclePhoto.DetectContentType([]));
    }
}
