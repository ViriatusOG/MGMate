using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using MGMate.Data;
using MGMate.Data.Repositories;
using MGMate.Worker.Mqtt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MGMate.Tests;

// The gateway publishes the time of each failed sign-in on account/lastLoginError (retained) and
// tries again every fifteen minutes. The user should hear about an outage once.
public sealed class AccountStatusHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 20, 0, 0, TimeSpan.Zero);
    private const string Prefix = "saic/user@example.com/account/";

    private readonly ServiceProvider _services;
    private readonly FakePushSender _push = new();
    private readonly AccountStatusHandler _handler;
    private readonly GatewaySignInState _signIn = new();

    public AccountStatusHandlerTests()
    {
        var root = new InMemoryDatabaseRoot();
        var name = Guid.NewGuid().ToString();
        _services = new ServiceCollection()
            .AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(name, root))
            .AddScoped<INotificationRepository, NotificationRepository>()
            .BuildServiceProvider();
        _handler = new AccountStatusHandler(
            NullLogger.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(),
            _push,
            WorkerLocalizer.Notifications(),
            new FixedTimeProvider(Now),
            _signIn);
    }

    public void Dispose() => _services.Dispose();

    private Task<bool> ReceiveAsync(string subtopic, string payload, bool retained = false) =>
        _handler.TryHandleAsync(new MqttMessage(Prefix + subtopic, payload, retained), TestContext.Current.CancellationToken);

    private async Task AlreadyAlertedAtAsync(DateTimeOffset at)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AppNotifications.Add(new AppNotification
        {
            Title = "MG sign-in failed",
            Category = NotificationCategories.GatewayLogin,
            CreatedAt = at.UtcDateTime,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FailedSignIn_SendsTheAlert()
    {
        var claimed = await ReceiveAsync("lastLoginError", "2026-10-11T19:59:00+00:00");

        Assert.True(claimed);
        Assert.Equal(NotificationCategories.GatewayLogin, Assert.Single(_push.Sent).Category);
    }

    // Retained: the broker's copy of an old failure, replayed when the Worker subscribes.
    [Fact]
    public async Task RetainedFailure_SendsNothing()
    {
        await ReceiveAsync("lastLoginError", "2026-10-01T08:00:00+00:00", retained: true);

        Assert.Empty(_push.Sent);
    }

    [Fact]
    public async Task RetryInTheSameOutage_SendsNothingMore()
    {
        await ReceiveAsync("lastLogin", "2026-10-11T10:00:00+00:00", retained: true);
        await AlreadyAlertedAtAsync(Now.AddMinutes(-15));

        await ReceiveAsync("lastLoginError", "2026-10-11T19:59:00+00:00");

        Assert.Empty(_push.Sent);
    }

    // The gateway signed in again after the last alert, so this failure starts a new outage.
    [Fact]
    public async Task FailureAfterTheGatewaySignedInAgain_SendsTheAlert()
    {
        await AlreadyAlertedAtAsync(Now.AddDays(-2));
        await ReceiveAsync("lastLogin", "2026-10-10T09:00:00+00:00");

        await ReceiveAsync("lastLoginError", "2026-10-11T19:59:00+00:00");

        Assert.Single(_push.Sent);
    }

    [Fact]
    public void OutageStart_WithoutAKnownSignIn_LooksBackOneDay()
    {
        Assert.Equal((Now - AccountStatusHandler.UnknownLoginWindow).UtcDateTime, _handler.OutageStart("user@example.com"));
    }

    [Fact]
    public async Task OtherAccountTopics_AreLeftForTheNextHandler()
    {
        Assert.False(await ReceiveAsync("gatewayVersion", "0.12.0"));
        Assert.False(await _handler.TryHandleAsync(
            new MqttMessage("saic/user/vehicles/FAKEVN00000000001/drivetrain/soc", "80", false),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FailureAfterTheLastSignIn_ReadsAsFailing()
    {
        await ReceiveAsync("lastLogin", "2026-10-11T18:00:00Z", retained: true);
        await ReceiveAsync("lastLoginError", "2026-10-11T19:00:00Z");

        var state = _signIn.Current;
        Assert.True(state.Failing);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 18, 0, 0, TimeSpan.Zero), state.LastSignInAt);
    }

    [Fact]
    public async Task RetainedFailureBeforeALaterSignIn_ReadsAsFine()
    {
        // The broker hands a new Worker both retained topics: an old failure, fixed since.
        await ReceiveAsync("lastLoginError", "2026-10-11T07:44:00Z", retained: true);
        await ReceiveAsync("lastLogin", "2026-10-11T08:08:00Z", retained: true);

        Assert.False(_signIn.Current.Failing);
    }

    [Fact]
    public async Task FailureWithNoSignInSeen_ReadsAsFailing()
    {
        await ReceiveAsync("lastLoginError", "2026-10-11T19:00:00Z", retained: true);

        Assert.True(_signIn.Current.Failing);
        Assert.Null(_signIn.Current.LastSignInAt);
    }

    [Fact]
    public void NothingHeard_ReadsAsFine() => Assert.False(_signIn.Current.Failing);
}
