using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MGMate.Core.Configuration;
using MGMate.Core.Helpers;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using Microsoft.Extensions.Logging;

namespace MGMate.Data.Services;

/// <summary>
/// The DVLA's Vehicle Enquiry Service: one POST with the plate, answered with the vehicle's tax,
/// MOT and registration record. Nothing but the plate is sent, with the deployment's own key.
/// Without a key it asks nothing at all.
/// </summary>
public sealed class DvlaApiClient(
    IHttpClientFactory httpClientFactory,
    DvlaOptions options,
    ILogger<DvlaApiClient> logger) : IDvlaClient
{
    public const string HttpClientName = "dvla";

    // The DVLA allows a new key two requests a second; one car asked once a day is far below it.
    private readonly UpstreamRateGate _gate = new();
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryAfterThrottle = TimeSpan.FromMinutes(5);
    // A browser waits on the API's lookup; the Worker tries again on its next round.
    private static readonly TimeSpan WaitAtMost = TimeSpan.FromSeconds(20);

    public async Task<DvlaVehicle?> GetVehicleAsync(string plate, CancellationToken ct = default)
    {
        if (options.ApiKey is not { } key)
            throw new DvlaUnavailableException("DVLA lookups need a key (DVLA_API_KEY)");

        using var entry = await _gate.TryEnterAsync(WaitAtMost, failWhenBackingOff: true, ct);
        if (entry.Refusal == GateRefusal.BackingOff)
            throw new DvlaUnavailableException("The DVLA asked to slow down; trying again later");
        if (!entry.Entered)
            throw new DvlaUnavailableException("Another DVLA lookup is still running");

        using var request = new HttpRequestMessage(HttpMethod.Post, DvlaOptions.Url)
        {
            Content = JsonContent.Create(new { registrationNumber = plate }),
        };
        request.Headers.Add("x-api-key", key);

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var answer = await _gate.SendAsync(
                token => client.SendAsync(request, token), MinInterval, honourBackoff: false, RetryAfterThrottle, ct);
            var response = answer.Response;

            if (answer.Backoff is { } backoff)
            {
                logger.LogWarning("DVLA rate-limited ({Status}); pausing lookups for {Seconds}s",
                    (int)response.StatusCode, (int)backoff.TotalSeconds);
                throw new DvlaUnavailableException("The DVLA asked to slow down; trying again later");
            }

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    break;
                case HttpStatusCode.NotFound:
                    return null;
                // The DVLA answers a plate it cannot read as one with 400: a typo, like a 404.
                case HttpStatusCode.BadRequest:
                    return null;
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    throw new DvlaUnavailableException("The DVLA refused the key (DVLA_API_KEY)");
                default:
                    throw new DvlaUnavailableException($"The DVLA answered {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return Parse(json);
        }
        catch (HttpRequestException ex)
        {
            throw new DvlaUnavailableException("The DVLA could not be reached", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new DvlaUnavailableException("The DVLA did not answer in time", ex);
        }
        catch (JsonException ex)
        {
            throw new DvlaUnavailableException("The DVLA answered with something other than data", ex);
        }
    }

    /// <summary>Reads the DVLA's answer, keeping all of it beside the fields MGMate uses.</summary>
    internal static DvlaVehicle Parse(string json)
    {
        if (json.Length > DvlaLimits.RecordMaxLength)
            throw new DvlaUnavailableException("The DVLA answered with something other than data");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new DvlaUnavailableException("The DVLA answered with something other than data");

        return new DvlaVehicle(
            Text(root, "taxStatus"),
            Date(root, "taxDueDate"),
            Text(root, "motStatus"),
            // The DVLA's own pages name this field both ways.
            Date(root, "motExpiryDate") ?? Date(root, "motExpiry"),
            Text(root, "make"),
            Text(root, "colour"),
            Text(root, "fuelType"),
            // Written out again rather than kept as sent, so what is stored is JSON and nothing else.
            JsonSerializer.Serialize(root));
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text)
            ? text.Trim().Truncate(DvlaLimits.TextMaxLength)
            : null;

    private static DateOnly? Date(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
