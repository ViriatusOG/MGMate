using System.Net;
using System.Text.Json;
using MGMate.Api.Endpoints;
using MGMate.Core.Configuration;
using MGMate.Core.Interfaces;
using MGMate.Core.Models;
using MGMate.Data;
using MGMate.Data.Repositories;
using MGMate.Data.Services;
using MGMate.Worker;
using MGMate.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace MGMate.Tests;

/// <summary>The DVLA as a test wants it, counting what it is asked.</summary>
internal sealed class FakeDvlaClient : IDvlaClient
{
    public DvlaVehicle? Vehicle { get; set; } =
        new("Taxed", new DateOnly(2027, 8, 1), "No details held by DVLA", null, "MG", "GREY", "ELECTRICITY", """{"make":"MG"}""");

    public DvlaUnavailableException? Failure { get; set; }

    public List<string> Requests { get; } = [];

    public Task<DvlaVehicle?> GetVehicleAsync(string plate, CancellationToken ct = default)
    {
        Requests.Add(plate);
        return Failure is { } failure ? throw failure : Task.FromResult(Vehicle);
    }
}

internal static class DvlaTestServices
{
    // Not a key: the tests never reach the DVLA.
    public static readonly DvlaOptions Enabled = new() { ApiKey = "test-key" };

    public static ServiceProvider Build(IDvlaClient client, DvlaOptions options, TimeProvider time)
    {
        var root = new InMemoryDatabaseRoot();
        var name = Guid.NewGuid().ToString();
        return new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(name, root))
            .AddScoped<IVehicleRepository, VehicleRepository>()
            .AddScoped<IDvlaRepository, DvlaRepository>()
            .AddScoped<INotificationRepository, NotificationRepository>()
            .AddScoped<DvlaRefresher>()
            .AddSingleton(client)
            .AddSingleton(options)
            .AddSingleton(time)
            .BuildServiceProvider();
    }

    public static async Task<Vehicle> AddVehicleAsync(ServiceProvider services, string? plate, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vehicle = new Vehicle { Vin = RdwTestServices.Vin, LicensePlate = plate };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        return vehicle;
    }

    public static async Task<DvlaVehicleInfo?> RefreshAsync(ServiceProvider services, Vehicle vehicle, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DvlaRefresher>().RefreshAsync(vehicle, ct);
    }
}

/// <summary>
/// The DVLA client against the example answer the DVLA publishes in its own API guide
/// (Fixtures/Dvla): no real plate and no real key is in these tests.
/// </summary>
public class DvlaApiClientTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dvla", name));

    private static DvlaApiClient Client(FakeHttpHandler handler, DvlaOptions? options = null) =>
        new(new FakeHttpClientFactory(handler), options ?? DvlaTestServices.Enabled, NullLogger<DvlaApiClient>.Instance);

    [Fact]
    public async Task Vehicle_ReadsTheFieldsTheCardShows_AndKeepsTheWholeAnswer()
    {
        var handler = new FakeHttpHandler(Fixture("vehicle-example.json"));

        var vehicle = await Client(handler).GetVehicleAsync("ABC1234", TestContext.Current.CancellationToken);

        Assert.NotNull(vehicle);
        Assert.Equal("Untaxed", vehicle.TaxStatus);
        Assert.Equal(new DateOnly(2007, 1, 1), vehicle.TaxDueDate);
        Assert.Equal("No details held by DVLA", vehicle.MotStatus);
        Assert.Null(vehicle.MotExpiryDate);
        Assert.Equal("ROVER", vehicle.Make);
        Assert.Equal("BLUE", vehicle.Colour);
        Assert.Equal("PETROL", vehicle.FuelType);
        using var record = JsonDocument.Parse(vehicle.RecordJson);
        Assert.Equal(19, record.RootElement.EnumerateObject().Count());
        Assert.Equal(2494, record.RootElement.GetProperty("engineCapacity").GetInt32());
        Assert.Equal("2016-12-25", record.RootElement.GetProperty("dateOfLastV5CIssued").GetString());
    }

    [Fact]
    public async Task Vehicle_SendsOnlyThePlate_WithTheKeyInTheHeader()
    {
        var handler = new FakeHttpHandler(Fixture("vehicle-example.json"));

        await Client(handler).GetVehicleAsync("ABC1234", TestContext.Current.CancellationToken);

        Assert.Equal(DvlaOptions.Url, handler.RequestUris.Single());
        Assert.Equal("""{"registrationNumber":"ABC1234"}""", handler.LastRequestBody);
        Assert.Equal(["test-key"], handler.RequestHeaders.Single().GetValues("x-api-key"));
    }

    [Theory]
    [InlineData("""{"motStatus":"Valid","motExpiryDate":"2027-03-04"}""")]
    [InlineData("""{"motStatus":"Valid","motExpiry":"2027-03-04"}""")]
    public async Task Vehicle_ReadsTheMotDate_UnderEitherName(string body)
    {
        var vehicle = await Client(new FakeHttpHandler(body)).GetVehicleAsync("ABC1234", TestContext.Current.CancellationToken);

        Assert.Equal(new DateOnly(2027, 3, 4), vehicle!.MotExpiryDate);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Vehicle_UnknownPlate_IsNotFound(HttpStatusCode status)
    {
        var handler = new FakeHttpHandler("""{"errors":[{"status":"404","code":"404","title":"Vehicle Not Found"}]}""", status);

        Assert.Null(await Client(handler).GetVehicleAsync("ZZ99ZZZ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Vehicle_RefusedKey_SaysSo()
    {
        var handler = new FakeHttpHandler("""{"message":"Forbidden"}""", HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<DvlaUnavailableException>(
            () => Client(handler).GetVehicleAsync("ABC1234", TestContext.Current.CancellationToken));

        Assert.Contains("DVLA_API_KEY", ex.Message);
        Assert.DoesNotContain("test-key", ex.Message);
    }

    [Fact]
    public async Task Vehicle_WithoutAKey_AsksNothing()
    {
        var handler = new FakeHttpHandler("{}");

        await Assert.ThrowsAsync<DvlaUnavailableException>(
            () => Client(handler, new DvlaOptions()).GetVehicleAsync("ABC1234", TestContext.Current.CancellationToken));

        Assert.Equal(0, handler.CallCount);
    }
}

public class DvlaOptionsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Dvla_IsOffWithoutAKey(string? configured)
    {
        var dvla = DvlaOptions.From(Config(("Dvla:ApiKey", configured)));

        Assert.False(dvla.Enabled);
        Assert.Null(dvla.ApiKey);
    }

    [Fact]
    public void Dvla_TakesTheVariables()
    {
        var configuration = new ConfigurationBuilder()
            .AddMGMateEnvironment(new Dictionary<string, string?>
            {
                ["DVLA_API_KEY"] = " test-key ",
                ["DVLA_REFRESH_HOURS"] = "12",
            })
            .Build();

        var dvla = DvlaOptions.From(configuration);

        Assert.True(dvla.Enabled);
        Assert.Equal("test-key", dvla.ApiKey);
        Assert.Equal(12, dvla.RefreshHours);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("often", 24)]
    public void Dvla_RefreshHours_IsAnHourAtTheLeast(string configured, int expected)
    {
        Assert.Equal(expected, DvlaOptions.From(Config(("Dvla:RefreshHours", configured))).RefreshHours);
    }
}

// One lookup, saved: what is kept, and what stays when the DVLA cannot be asked.
public class DvlaRefresherTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Lookup_SavesTheAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new FakeDvlaClient();
        await using var services = DvlaTestServices.Build(client, DvlaTestServices.Enabled, new FixedTimeProvider(Start));
        var vehicle = await DvlaTestServices.AddVehicleAsync(services, "AB12CDE", ct);

        var info = await DvlaTestServices.RefreshAsync(services, vehicle, ct);

        Assert.NotNull(info);
        Assert.True(info.Found);
        Assert.Equal("AB12CDE", info.LicensePlate);
        Assert.Equal("Taxed", info.TaxStatus);
        Assert.Equal(new DateOnly(2027, 8, 1), info.TaxDueDate);
        Assert.Equal("""{"make":"MG"}""", info.RecordJson);
        Assert.Equal(Start.UtcDateTime, info.FetchedAt);
        Assert.Null(info.LastError);
        Assert.Equal(["AB12CDE"], client.Requests);
    }

    [Fact]
    public async Task UnknownPlate_IsSavedAsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new FakeDvlaClient { Vehicle = null };
        await using var services = DvlaTestServices.Build(client, DvlaTestServices.Enabled, new FixedTimeProvider(Start));
        var vehicle = await DvlaTestServices.AddVehicleAsync(services, "ZZ99ZZZ", ct);

        var info = await DvlaTestServices.RefreshAsync(services, vehicle, ct);

        Assert.False(info!.Found);
        Assert.Null(info.TaxStatus);
    }

    [Fact]
    public async Task Failure_KeepsTheLastAnswer_AndTheReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new FixedTimeProvider(Start);
        var client = new FakeDvlaClient();
        await using var services = DvlaTestServices.Build(client, DvlaTestServices.Enabled, time);
        var vehicle = await DvlaTestServices.AddVehicleAsync(services, "AB12CDE", ct);
        await DvlaTestServices.RefreshAsync(services, vehicle, ct);

        time.Advance(TimeSpan.FromHours(25));
        client.Failure = new DvlaUnavailableException("The DVLA could not be reached");
        var info = await DvlaTestServices.RefreshAsync(services, vehicle, ct);

        Assert.Equal("Taxed", info!.TaxStatus);
        Assert.Equal(Start.UtcDateTime, info.FetchedAt);
        Assert.Equal("The DVLA could not be reached", info.LastError);
    }

    [Fact]
    public async Task WithoutAKey_OrAPlate_NothingIsAsked()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = new FakeDvlaClient();
        await using var off = DvlaTestServices.Build(client, new DvlaOptions(), new FixedTimeProvider(Start));
        var vehicle = await DvlaTestServices.AddVehicleAsync(off, "AB12CDE", ct);
        await using var on = DvlaTestServices.Build(client, DvlaTestServices.Enabled, new FixedTimeProvider(Start));
        var plateless = await DvlaTestServices.AddVehicleAsync(on, null, ct);

        Assert.Null(await DvlaTestServices.RefreshAsync(off, vehicle, ct));
        Assert.Null(await DvlaTestServices.RefreshAsync(on, plateless, ct));
        Assert.Empty(client.Requests);
    }
}

// The Worker's round: when it asks, and which pushes come of what the DVLA said.
public class DvlaSyncServiceTests
{
    private static readonly IStringLocalizer<NotificationStrings> Strings = WorkerLocalizer.Notifications();
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 16);

    private sealed class OnePass(IServiceScopeFactory scopes, FakePushSender push, DvlaOptions options, TimeProvider time)
        : DvlaSyncService(NullLogger<DvlaSyncService>.Instance, scopes, push, options, Strings, time)
    {
        public Task RunAsync(CancellationToken ct) => RunOnceAsync(ct);
    }

    private static DvlaVehicleInfo Info(string? tax, DateOnly? taxDue, string? mot, DateOnly? motExpiry) =>
        new() { Found = true, TaxStatus = tax, TaxDueDate = taxDue, MotStatus = mot, MotExpiryDate = motExpiry };

    private static string[] Categories(DvlaVehicleInfo info) =>
        DvlaSyncService.BuildAlerts(info, Start.UtcDateTime, Strings).Select(a => a.Category).ToArray();

    [Fact]
    public void TaxedForMonths_AndTooNewForAnMot_NeedsNothing()
    {
        Assert.Empty(Categories(Info("Taxed", Today.AddDays(250), "No details held by DVLA", null)));
    }

    [Theory]
    [InlineData(30, "dvla-tax-due-30-20261216")]
    [InlineData(8, "dvla-tax-due-30-20261124")]
    [InlineData(7, "dvla-tax-due-7-20261123")]
    [InlineData(0, "dvla-tax-due-7-20261116")]
    public void TaxRunningOut_IsRemindedAMonthAndAWeekAhead(int daysLeft, string expected)
    {
        Assert.Equal([expected], Categories(Info("Taxed", Today.AddDays(daysLeft), null, null)));
    }

    [Fact]
    public void Untaxed_IsSaid_AndSornIsLeftAlone()
    {
        Assert.Equal(["dvla-untaxed"], Categories(Info("Untaxed", Today.AddDays(-3), null, null)));
        Assert.Empty(Categories(Info("SORN", null, null, null)));
    }

    [Fact]
    public void Mot_IsRemindedAhead_AndOnceItHasRunOut()
    {
        Assert.Equal(["dvla-mot-due-30-20261206"], Categories(Info(null, null, "Valid", Today.AddDays(20))));
        Assert.Equal(["dvla-mot-expired"], Categories(Info(null, null, "Valid", Today.AddDays(-1))));
        Assert.Equal(["dvla-mot-expired"], Categories(Info(null, null, "Not valid", null)));
    }

    [Fact]
    public void DueBody_NamesTheDateAndTheDays()
    {
        var alert = Assert.Single(DvlaSyncService.BuildAlerts(Info("Taxed", Today.AddDays(20), null, null), Start.UtcDateTime, Strings));

        Assert.Equal("Road tax due soon", alert.Title);
        Assert.Contains("6 December 2026", alert.Body);
        Assert.Contains("20 days", alert.Body);
    }

    [Fact]
    public async Task Round_AsksOnceADay_AndPushesEachReminderOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new FixedTimeProvider(Start);
        var client = new FakeDvlaClient
        {
            Vehicle = new("Taxed", Today.AddDays(20), "No details held by DVLA", null, "MG", "GREY", "ELECTRICITY", "{}"),
        };
        await using var services = DvlaTestServices.Build(client, DvlaTestServices.Enabled, time);
        await DvlaTestServices.AddVehicleAsync(services, "AB12CDE", ct);
        var push = new FakePushSender();
        var service = new OnePass(services.GetRequiredService<IServiceScopeFactory>(), push, DvlaTestServices.Enabled, time);

        await service.RunAsync(ct);
        time.Advance(TimeSpan.FromHours(1));
        await service.RunAsync(ct);

        Assert.Single(client.Requests);
        Assert.Equal("dvla-tax-due-30-20261206", Assert.Single(push.Sent).Category);

        time.Advance(TimeSpan.FromHours(24));
        await service.RunAsync(ct);

        Assert.Equal(2, client.Requests.Count);
        Assert.Single(push.Sent);
    }

    [Fact]
    public async Task WithoutAKey_TheRoundAsksNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new FixedTimeProvider(Start);
        var client = new FakeDvlaClient();
        var off = new DvlaOptions();
        await using var services = DvlaTestServices.Build(client, off, time);
        await DvlaTestServices.AddVehicleAsync(services, "AB12CDE", ct);
        var service = new OnePass(services.GetRequiredService<IServiceScopeFactory>(), new FakePushSender(), off, time);

        await service.RunAsync(ct);

        Assert.Empty(client.Requests);
    }
}

public class DvlaEndpointsTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Dto_CountsDaysAndPassesTheWholeRecordOn()
    {
        var vehicle = new Vehicle { Vin = "V", LicensePlate = "AB12CDE", RegistrationDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc) };
        var info = new DvlaVehicleInfo
        {
            LicensePlate = "AB12CDE",
            Found = true,
            TaxStatus = "Taxed",
            TaxDueDate = new DateOnly(2026, 12, 1),
            MotStatus = "No details held by DVLA",
            RecordJson = """{"wheelplan":"2 AXLE RIGID BODY"}""",
            FetchedAt = Now,
        };

        var dto = DvlaEndpoints.ToDto(vehicle, info, Now);

        Assert.Equal(new DateOnly(2029, 7, 31), dto.FirstMotDue);
        Assert.Equal(15, dto.Info!.TaxDaysLeft);
        Assert.Null(dto.Info.MotDaysLeft);
        Assert.Equal("2 AXLE RIGID BODY", dto.Info.Record.GetProperty("wheelplan").GetString());
    }

    [Fact]
    public void Dto_LeavesOutALookupForAnotherPlate()
    {
        var vehicle = new Vehicle { Vin = "V", LicensePlate = "AB12CDE" };
        var info = new DvlaVehicleInfo { LicensePlate = "ZZ99ZZZ", Found = true };

        var dto = DvlaEndpoints.ToDto(vehicle, info, Now);

        Assert.Null(dto.Info);
        Assert.Null(dto.FirstMotDue);
    }
}
