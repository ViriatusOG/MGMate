namespace MGMate.Core.Models;

/// <summary>
/// One part of a car's warranty, such as "Paint, 3 years or 60,000 miles". Every cover runs from
/// the car's <see cref="Vehicle.RegistrationDate"/>, and its distance limit is an odometer reading,
/// not a distance driven since then. The list starts as the manufacturer's defaults and the owner
/// can change it.
/// </summary>
public class WarrantyCover
{
    public int Id { get; set; }
    public int VehicleId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>How long the cover lasts from registration. 0 means the part is not covered at all.</summary>
    public int Months { get; set; }

    /// <summary>The odometer reading at which the cover ends, in km. Null when distance doesn't matter.</summary>
    public double? DistanceKm { get; set; }

    /// <summary>Shown as a large card at the top of the warranty section.</summary>
    public bool Featured { get; set; }

    /// <summary>Order within the list when two covers end on the same day.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class WarrantyLimits
{
    public const int NameMaxLength = 200;
    public const int NotesMaxLength = 500;
    public const int MaxMonths = 360;
    public const double MaxDistanceKm = 2_000_000;
    public const int MaxCovers = 100;
}
