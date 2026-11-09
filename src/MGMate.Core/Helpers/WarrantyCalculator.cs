namespace MGMate.Core.Helpers;

public enum WarrantyStatus
{
    /// <summary>The registration date isn't known, so nothing can be worked out.</summary>
    Unknown,
    Active,
    EndingSoon,
    Expired,
    NotCovered,
}

public static class WarrantyStatusNames
{
    public static string ApiName(this WarrantyStatus status) => status switch
    {
        WarrantyStatus.Active => "active",
        WarrantyStatus.EndingSoon => "endingSoon",
        WarrantyStatus.Expired => "expired",
        WarrantyStatus.NotCovered => "notCovered",
        _ => "unknown",
    };
}

/// <param name="Status">Where the cover stands.</param>
/// <param name="EndsOn">The day the cover ends by time. Null when unknown or not covered.</param>
/// <param name="EndsAtKm">The odometer reading at which it ends. Null when distance doesn't matter.</param>
/// <param name="DaysRemaining">Days until <paramref name="EndsOn"/>; negative once past.</param>
/// <param name="KmRemaining">Km until <paramref name="EndsAtKm"/>; negative once past. Null without an odometer reading.</param>
/// <param name="TimeUsed">Share of the time cover used, 0 to 1.</param>
/// <param name="DistanceUsed">Share of the distance cover used, 0 to 1.</param>
/// <param name="EndedBy">"time" or "distance" once expired: which limit was reached first.</param>
public readonly record struct WarrantyResult(
    WarrantyStatus Status,
    DateTime? EndsOn,
    double? EndsAtKm,
    int? DaysRemaining,
    double? KmRemaining,
    double? TimeUsed,
    double? DistanceUsed,
    string? EndedBy);

/// <summary>
/// Where a warranty cover stands: it ends at whichever of its time or odometer limit comes first.
/// It is "ending soon" in its last <see cref="EndingSoonDays"/> days or the last
/// <see cref="EndingSoonDistanceFraction"/> of its distance.
/// </summary>
public static class WarrantyCalculator
{
    public const int EndingSoonDays = 30;
    public const double EndingSoonDistanceFraction = 0.1;

    public static WarrantyResult Calculate(
        int months, double? distanceKm, DateTime? registrationDate, double? currentOdometerKm, DateTime nowUtc)
    {
        if (months <= 0)
            return new(WarrantyStatus.NotCovered, null, null, null, null, null, null, null);
        if (registrationDate is not { } start)
            return new(WarrantyStatus.Unknown, null, distanceKm, null, null, null, null, null);

        var endsOn = start.AddMonths(months);
        var today = nowUtc.Date;
        var daysRemaining = (int)Math.Round((endsOn.Date - today).TotalDays);
        var totalDays = Math.Max(1, (endsOn.Date - start.Date).TotalDays);
        var timeUsed = Math.Clamp((today - start.Date).TotalDays / totalDays, 0, 1);

        double? kmRemaining = null, distanceUsed = null;
        if (distanceKm is > 0 && currentOdometerKm is { } odometer)
        {
            kmRemaining = distanceKm.Value - odometer;
            distanceUsed = Math.Clamp(odometer / distanceKm.Value, 0, 1);
        }

        var timeOver = daysRemaining <= 0;
        var distanceOver = kmRemaining is <= 0;
        if (timeOver || distanceOver)
            return new(WarrantyStatus.Expired, endsOn, distanceKm, daysRemaining, kmRemaining, timeUsed, distanceUsed,
                timeOver && !distanceOver ? "time" : !timeOver ? "distance" : "time");

        var soon = daysRemaining <= EndingSoonDays
            || (kmRemaining is { } left && left <= distanceKm!.Value * EndingSoonDistanceFraction);
        return new(soon ? WarrantyStatus.EndingSoon : WarrantyStatus.Active,
            endsOn, distanceKm, daysRemaining, kmRemaining, timeUsed, distanceUsed, null);
    }
}
