using MGMate.Core.Interfaces;
using MGMate.Core.Models;

namespace MGMate.Data.Demo;

public sealed class DemoVehicleRepository : IVehicleRepository
{
    public static readonly Vehicle DemoVehicle = new()
    {
        Id = 1,
        Vin = "DEMOVIN0000012345",
        Model = "MG ZS EV",
        Series = "ZS EV",
        SaicUser = "demo@example.com",
        CreatedAt = DateTime.UtcNow.AddMonths(-6),
        // BType 1: a ZS EV takes a charge target, so the demo shows that control.
        ConfigJson = """{"hw_version":"MG_BEV_1.0","BType":"1"}""",
        // A made-up plate in the current Dutch format; the demo's RDW client answers for any plate.
        LicensePlate = "ZZ123Z",
        RegistrationDate = DateTime.UtcNow.Date.AddMonths(-6),
        DeliveryDate = DateTime.UtcNow.Date.AddMonths(-6).AddDays(10),
        DeliveryOdometerKm = 24,
    };

    public Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Vehicle>>([DemoVehicle]);

    public Task<Vehicle?> GetByVinAsync(string vin, CancellationToken ct = default) =>
        Task.FromResult<Vehicle?>(vin == DemoVehicle.Vin ? DemoVehicle : null);

    public Task<Vehicle> GetOrCreateByVinAsync(string vin, string? saicUser = null, CancellationToken ct = default) =>
        Task.FromResult(DemoVehicle);

    public Task SetModelAsync(int vehicleId, string model, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetConfigValueAsync(int vehicleId, string key, string value, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetParkedAsync(
        int vehicleId, DateTime parkedAt, double? latitude, double? longitude, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetLastMessageIdAsync(int vehicleId, string messageId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetOwnershipDatesAsync(
        int vehicleId, DateTime? registrationDate, DateTime? deliveryDate, double? deliveryOdometerKm, CancellationToken ct = default)
    {
        if (vehicleId != DemoVehicle.Id) return Task.CompletedTask;
        DemoVehicle.RegistrationDate = registrationDate;
        DemoVehicle.DeliveryDate = deliveryDate;
        DemoVehicle.DeliveryOdometerKm = deliveryOdometerKm;
        return Task.CompletedTask;
    }

    // The one setting the demo keeps: entering a plate is how the RDW card is tried out.
    public Task SetLicensePlateAsync(int vehicleId, string? licensePlate, CancellationToken ct = default)
    {
        if (vehicleId == DemoVehicle.Id) DemoVehicle.LicensePlate = licensePlate;
        return Task.CompletedTask;
    }
}
