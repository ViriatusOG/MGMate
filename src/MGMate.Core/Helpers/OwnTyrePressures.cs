using System.Text.Json;

namespace MGMate.Core.Helpers;

/// <summary>
/// The cold pressures an account set for its tyres under Settings &gt; Your car, a summer and a
/// winter pair, and which season it switched to. Kept with the browser's "ui" settings as
/// <c>tyrePressures</c>; read here so the API can hand the pair in use to clients that have no
/// copy of those settings, such as the Android app.
/// </summary>
public sealed record OwnTyrePressures(string Season, double FrontBar, double RearBar)
{
    public const string SettingsSection = "ui";
    public const string SettingsKey = "tyrePressures";

    // Well outside anything a car tyre is set to, so a typo or a value from another unit is ignored.
    private const double MinBar = 1.0;
    private const double MaxBar = 5.0;

    /// <summary>The pair for the season the account switched to, or null when it set none (or set nonsense).</summary>
    public static OwnTyrePressures? From(IReadOnlyDictionary<string, JsonElement> settings)
    {
        if (!settings.TryGetValue(SettingsSection, out var section)
            || section.ValueKind != JsonValueKind.Object
            || !section.TryGetProperty(SettingsKey, out var own)
            || own.ValueKind != JsonValueKind.Object)
            return null;

        var season = own.TryGetProperty("season", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString()
            : null;
        if (season is not ("summer" or "winter")) return null;

        if (!own.TryGetProperty(season, out var pair) || pair.ValueKind != JsonValueKind.Object) return null;
        var front = Bar(pair, "frontBar");
        var rear = Bar(pair, "rearBar");
        return front is { } f && rear is { } r ? new OwnTyrePressures(season, f, r) : null;
    }

    private static double? Bar(JsonElement pair, string name) =>
        pair.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var bar)
        && bar is >= MinBar and <= MaxBar
            ? bar
            : null;
}
