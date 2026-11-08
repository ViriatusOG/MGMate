namespace MGMate.Core.Configuration;

/// <summary>How MGMate introduces itself to the public services it asks things.</summary>
public static class UpstreamHttp
{
    /// <summary>
    /// The User-Agent for every outbound request. Nominatim's usage policy asks for one that
    /// identifies the application well enough to reach whoever runs it, and the other services
    /// (Overpass, Open Charge Map, Valhalla, GitHub) are better served by it too.
    /// </summary>
    public const string UserAgent = "MGMate/1.0 (+https://github.com/ViriatusOG/MGMate)";
}
