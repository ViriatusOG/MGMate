using Microsoft.Extensions.Configuration;

namespace MGMate.Core.Configuration;

/// <summary>
/// Whether the car's UK registration is looked up at the DVLA's Vehicle Enquiry Service, and how
/// often (DVLA_API_KEY, DVLA_REFRESH_HOURS). The DVLA issues one key per applicant, so every
/// deployment brings its own: without one nothing is asked. The plate is the one thing sent.
/// </summary>
public sealed class DvlaOptions
{
    public const string Url = "https://driver-vehicle-licensing.api.gov.uk/vehicle-enquiry/v1/vehicles";

    public const int DefaultRefreshHours = 24;

    /// <summary>Sent as x-api-key. Null switches the lookups off.</summary>
    public string? ApiKey { get; init; }

    /// <summary>How old a lookup may get before the Worker asks again. One hour at the least.</summary>
    public int RefreshHours { get; init; } = DefaultRefreshHours;

    /// <summary>Set only by demo mode, where a stand-in answers and no key is needed.</summary>
    public bool Demo { get; init; }

    public bool Enabled => Demo || ApiKey is not null;

    public TimeSpan RefreshInterval => TimeSpan.FromHours(RefreshHours);

    public static DvlaOptions From(IConfiguration configuration) => new()
    {
        ApiKey = configuration["Dvla:ApiKey"] is { } key && !string.IsNullOrWhiteSpace(key) ? key.Trim() : null,
        RefreshHours = Math.Max(1, configuration.IntegerOrDefault("Dvla:RefreshHours", DefaultRefreshHours)),
    };
}
