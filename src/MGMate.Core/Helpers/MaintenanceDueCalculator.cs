using MGMate.Core.Models;

namespace MGMate.Core.Helpers;

public enum MaintenanceDueStatus
{
    Unknown,
    Ok,
    DueSoon,
    Overdue,
}

public static class MaintenanceDueStatusNames
{
    /// <summary>The status as the API and the widget name it: ok, dueSoon, overdue or unknown.</summary>
    public static string ApiName(this MaintenanceDueStatus status) => status switch
    {
        MaintenanceDueStatus.Ok => "ok",
        MaintenanceDueStatus.DueSoon => "dueSoon",
        MaintenanceDueStatus.Overdue => "overdue",
        _ => "unknown",
    };
}

/// <summary>Where an item's interval is counted from.</summary>
public enum MaintenanceBaselineSource
{
    None,
    Service,
    Registration,
}

public readonly record struct MaintenanceBaseline(MaintenanceBaselineSource Source, DateTime? Date, double? OdometerKm)
{
    /// <summary>As the API names it: service, registration, or null when there is nothing to count from.</summary>
    public string? ApiName => Source switch
    {
        MaintenanceBaselineSource.Service => "service",
        MaintenanceBaselineSource.Registration => "registration",
        _ => null,
    };
}

public readonly record struct MaintenanceDueResult(
    MaintenanceDueStatus Status,
    DateTime? NextDueDate,
    double? NextDueOdometerKm,
    int? DaysRemaining,
    double? KmRemaining);

/// <summary>
/// Computes due status for a maintenance item from its optional distance/time interval.
/// When both intervals are set, status is "whichever comes first": the worse of the two
/// independently-evaluated dimensions wins. A dimension that can't be evaluated (missing
/// interval or baseline) is excluded rather than treated as "worst", so a km-only item is
/// judged purely on distance instead of collapsing to Unknown because no date interval exists.
/// </summary>
public static class MaintenanceDueCalculator
{
    public const double DueSoonFractionElapsed = 0.9;
    private const double DaysPerMonth = 30.44;

    /// <summary>
    /// What an item counts from: its latest service when one is known, otherwise the car's first
    /// registration at 0 km, the way MG counts the first service of a new car.
    /// </summary>
    public static MaintenanceBaseline BaselineFor(MaintenanceItem item, Vehicle? vehicle)
    {
        if (item.LastServiceDate is not null || item.LastServiceOdometerKm is not null)
            return new(MaintenanceBaselineSource.Service, item.LastServiceDate, item.LastServiceOdometerKm);
        if (vehicle?.RegistrationDate is { } registered)
            return new(MaintenanceBaselineSource.Registration, registered, 0);
        return new(MaintenanceBaselineSource.None, null, null);
    }

    /// <summary>An item's due status, counted from <see cref="BaselineFor"/>.</summary>
    public static MaintenanceDueResult Calculate(
        MaintenanceItem item, Vehicle? vehicle, double? currentOdometerKm, DateTime nowUtc)
    {
        var baseline = BaselineFor(item, vehicle);
        return Calculate(item.IntervalKm, item.IntervalMonths, baseline.Date, baseline.OdometerKm, currentOdometerKm, nowUtc);
    }

    public static MaintenanceDueResult Calculate(
        double? intervalKm,
        int? intervalMonths,
        DateTime? lastServiceDate,
        double? lastServiceOdometerKm,
        double? currentOdometerKm,
        DateTime nowUtc)
    {
        double? nextDueOdometerKm = null;
        if (intervalKm is > 0 && lastServiceOdometerKm is not null)
            nextDueOdometerKm = lastServiceOdometerKm + intervalKm;

        double? kmRemaining = null;
        MaintenanceDueStatus? kmStatus = null;
        if (nextDueOdometerKm is not null && currentOdometerKm is not null)
        {
            kmRemaining = nextDueOdometerKm - currentOdometerKm;
            var kmElapsed = currentOdometerKm.Value - lastServiceOdometerKm!.Value;
            kmStatus = ClassifyFraction(kmElapsed / intervalKm!.Value);
        }

        DateTime? nextDueDate = null;
        int? daysRemaining = null;
        MaintenanceDueStatus? dateStatus = null;
        if (intervalMonths is > 0 && lastServiceDate is not null)
        {
            nextDueDate = lastServiceDate.Value.AddMonths(intervalMonths.Value);
            daysRemaining = (int)(nextDueDate.Value.Date - nowUtc.Date).TotalDays;
            var intervalDays = intervalMonths.Value * DaysPerMonth;
            var daysElapsed = (nowUtc.Date - lastServiceDate.Value.Date).TotalDays;
            dateStatus = ClassifyFraction(daysElapsed / intervalDays);
        }

        var status = CombineStatus(kmStatus, dateStatus);
        return new MaintenanceDueResult(status, nextDueDate, nextDueOdometerKm, daysRemaining, kmRemaining);
    }

    private static MaintenanceDueStatus ClassifyFraction(double fractionElapsed)
    {
        if (fractionElapsed >= 1.0) return MaintenanceDueStatus.Overdue;
        if (fractionElapsed >= DueSoonFractionElapsed) return MaintenanceDueStatus.DueSoon;
        return MaintenanceDueStatus.Ok;
    }

    private static MaintenanceDueStatus CombineStatus(MaintenanceDueStatus? kmStatus, MaintenanceDueStatus? dateStatus)
    {
        if (kmStatus is null && dateStatus is null) return MaintenanceDueStatus.Unknown;
        if (kmStatus is null) return dateStatus!.Value;
        if (dateStatus is null) return kmStatus.Value;
        return (MaintenanceDueStatus)Math.Max((int)kmStatus.Value, (int)dateStatus.Value);
    }
}
