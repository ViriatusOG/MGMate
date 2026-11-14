using System.Text.RegularExpressions;

namespace MGMate.Core.Helpers;

/// <summary>
/// The car's name as the app shows it. The gateway announces a car as its model, model year and
/// MG's colour name run together ("MG4 EV 2026 Andes Grey"). MG's colour name is its own internal
/// one and need not match the brochure (a UK MG4 in Camden Grey reports Andes Grey), so the name
/// stops at the year; the colour is the owner's choice under Settings > Your car. The gateway's raw
/// model and colour are still kept on the vehicle record and shown on the Debug page.
/// </summary>
public static partial class VehicleModelName
{
    // Everything up to and including a four-digit model year, when words (the colour) follow it.
    [GeneratedRegex(@"^(?<name>.*?\b(?:19|20)\d{2})\s+[^\d\s].*$")]
    private static partial Regex ColourAfterYear();

    public static string WithoutColour(string model)
    {
        var trimmed = model.Trim();
        var match = ColourAfterYear().Match(trimmed);
        return match.Success ? match.Groups["name"].Value : trimmed;
    }
}
