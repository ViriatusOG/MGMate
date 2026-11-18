using System.Text.Json;

namespace MGMate.Core.Helpers;

/// <summary>
/// The name the account gave its car under Settings > Your car. MG names a car its own way (a UK
/// MG4 EV of 2026 can report "MG4 ELECTRIC 2025"), so the owner may write the name they know it
/// by. It is kept in the account's synced settings, and the vehicle list serves it in place of
/// the reported model, so the web app and the Android app show the same name.
/// </summary>
public static class OwnCarName
{
    public const string SettingsSection = "ui";
    public const string SettingsKey = "carName";
    public const int MaxLength = 40;

    /// <summary>The name the account set, or null when it set none (or set nonsense).</summary>
    public static string? From(IReadOnlyDictionary<string, JsonElement> settings)
    {
        if (!settings.TryGetValue(SettingsSection, out var section)
            || section.ValueKind != JsonValueKind.Object
            || !section.TryGetProperty(SettingsKey, out var value)
            || value.ValueKind != JsonValueKind.String)
            return null;

        var name = new string((value.GetString() ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return name.Length is > 0 and <= MaxLength ? name : null;
    }
}
