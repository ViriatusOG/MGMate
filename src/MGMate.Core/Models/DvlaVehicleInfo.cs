namespace MGMate.Core.Models;

/// <summary>
/// What the DVLA last said about a vehicle's UK registration: whether it is taxed and until when,
/// its MOT, and the rest of the record. One row per vehicle, written by whichever process looked
/// last (the API right after the plate is entered, the Worker on its daily round).
/// </summary>
public class DvlaVehicleInfo
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    /// <summary>The plate this was looked up for; a row for any other plate is about another car.</summary>
    public string LicensePlate { get; set; } = string.Empty;

    /// <summary>False when the DVLA has no vehicle under the plate.</summary>
    public bool Found { get; set; }

    /// <summary>"Taxed", "Untaxed", "SORN" or "Not Taxed for on Road Use", in the DVLA's words.</summary>
    public string? TaxStatus { get; set; }

    public DateOnly? TaxDueDate { get; set; }

    /// <summary>"Valid", "Not valid", "No details held by DVLA" or "No results returned".</summary>
    public string? MotStatus { get; set; }

    public DateOnly? MotExpiryDate { get; set; }

    public string? Make { get; set; }

    public string? Colour { get; set; }

    public string? FuelType { get; set; }

    /// <summary>
    /// The DVLA's whole answer as it came, so a field MGMate does not read today (emissions, type
    /// approval, the V5C date and whatever the DVLA adds later) is there for the day it does.
    /// </summary>
    public string RecordJson { get; set; } = "{}";

    /// <summary>When the DVLA last answered. Null until it has.</summary>
    public DateTime? FetchedAt { get; set; }

    /// <summary>Why the last attempt failed, or null when it did not.</summary>
    public string? LastError { get; set; }
}

/// <summary>The DVLA's answer for a plate: the fields MGMate reads, and the answer itself.</summary>
public sealed record DvlaVehicle(
    string? TaxStatus,
    DateOnly? TaxDueDate,
    string? MotStatus,
    DateOnly? MotExpiryDate,
    string? Make,
    string? Colour,
    string? FuelType,
    string RecordJson);

/// <summary>The DVLA could not be asked, or did not answer usably. The message is kept as the lookup's last error.</summary>
public sealed class DvlaUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public static class DvlaLimits
{
    public const int TextMaxLength = 64;
    public const int ErrorMaxLength = 256;

    /// <summary>The DVLA's answer is some twenty short fields; anything far beyond that is not it.</summary>
    public const int RecordMaxLength = 8000;
}
