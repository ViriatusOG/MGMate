using System.Globalization;
using MGMate.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace MGMate.Tests;

/// <summary>
/// Resolves the resx-backed localizers the same way the hosts do. A marker type in the wrong
/// namespace makes the localizer silently return the resource keys, which for English keys
/// that equal their values is invisible, so these tests pick keys whose text differs from the
/// key and also check that the resource was found.
/// </summary>
public class LocalizedStringsTests
{
    private static IStringLocalizer<T> Resolve<T>() =>
        new ServiceCollection()
            .AddLogging()
            .AddLocalization(o => o.ResourcesPath = "Resources")
            .BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<T>>();

    private static T WithUiCulture<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try { return action(); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void NotificationStrings_ResolveEnglishByDefault()
    {
        var strings = WorkerLocalizer.Notifications();

        var title = WithUiCulture("en", () => strings["EngineStartTitle"]);

        Assert.False(title.ResourceNotFound);
        Assert.Equal("Engine started", title.Value);
    }

    [Fact]
    public void NotificationStrings_FallBackToEnglish_ForALanguageThatIsNotShipped()
    {
        var strings = WorkerLocalizer.Notifications();

        var title = WithUiCulture("nl", () => strings["EngineStartTitle"]);

        Assert.False(title.ResourceNotFound);
        Assert.Equal("Engine started", title.Value);
    }

    [Fact]
    public void WidgetStrings_ResolveEnglish()
    {
        var strings = Resolve<WidgetStrings>();

        var locked = WithUiCulture("en", () => strings["Locked"]);

        Assert.False(locked.ResourceNotFound);
    }
}
