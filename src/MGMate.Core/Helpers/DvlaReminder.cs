namespace MGMate.Core.Helpers;

/// <summary>
/// When road tax or an MOT is worth a reminder: a month ahead, a week ahead, and once it has run
/// out, the same steps as the APK's. The DVLA's dates are days on the UK calendar, so days are
/// counted between calendar dates in London and never as 24-hour spans.
/// </summary>
public static class DvlaReminder
{
    private const string UkZoneId = "Europe/London";

    /// <summary>The zone the DVLA's dates are days in.</summary>
    public static TimeZoneInfo UkZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(UkZoneId);

    /// <summary>Days from today in the UK to <paramref name="date"/>: 0 on that day, negative once it has passed.</summary>
    public static int DaysLeft(DateOnly date, DateTime utcNow) =>
        date.DayNumber - ZoneTime.DateOf(utcNow, UkZone).DayNumber;

    /// <summary>A new car's first MOT falls due three years after it was first registered.</summary>
    public static DateOnly FirstMotDue(DateOnly registeredOn) => registeredOn.AddYears(3);
}
