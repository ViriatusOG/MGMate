using MGMate.Core.Models;

namespace MGMate.Core.Helpers;

/// <summary>
/// The covers a new warranty list starts with: MG Motor UK's warranty statement for cars registered
/// on or after 1 September 2019 (issue of 1 January 2025), limited to what applies to an electric
/// car. Petrol-only items (clutch, drive belts, oil, air and fuel filters, spark plugs, exhaust) are
/// left out. Owners elsewhere, or with an extended warranty, edit the list.
/// </summary>
public static class WarrantyDefaults
{
    private const double KmPerMile = 1.609344;

    private sealed record Cover(string Name, int Months, int? Miles, string? Notes = null, bool Featured = false);

    private static readonly Cover[] MgUk =
    [
        new("Vehicle", 84, 80_000, "Years 4 to 7 need a full MG service history", Featured: true),
        new("High voltage battery", 96, 100_000, "Repaired or replaced if it drops below 70% of its original capacity", Featured: true),
        new("Anti-perforation", 84, null, "Rust-through of body panels. Needs a yearly body inspection by an MG repairer", Featured: true),
        new("Paint", 36, 60_000),
        new("Cosmetic paint, incl. alloy wheels", 36, null),
        new("Infotainment, convenience and driver aids", 36, 45_000, "Third-party smartphone apps are not covered"),
        new("Charging door", 36, 60_000),
        new("Suspension and steering", 36, 45_000, "Wheel bearings, ball joints, bushes, track rod ends, dampers, struts, rubber parts"),
        new("Brake discs and linings", 12, 15_000),
        new("Fuses, bulbs, key fob battery, seat covers", 12, 15_000),
        new("12V battery", 12, null, "Claims capped at £150"),
        new("EV charging cable, cosmetic trim", 12, null),
        new("Wear and tear items", 12, null, "Until 12 months or the first paid main service, whichever is sooner"),
        new("Pollen filter, parking brake adjust", 12, 15_000, "Until the first scheduled service; match it to your service plan"),
        new("Wiper blades, air con re-gas", 6, 7_500),
        new("Headlamp and panel hinge adjustment", 6, 7_500),
        new("Glass crack", 3, 1_000),
        new("Wheel alignment and balance", 3, 1_000),
        new("Tyres", 0, null, "Not covered by MG"),
    ];

    /// <summary>A fresh copy of the default covers for <paramref name="vehicleId"/>.</summary>
    public static List<WarrantyCover> For(int vehicleId) =>
        MgUk.Select((c, i) => new WarrantyCover
        {
            VehicleId = vehicleId,
            Name = c.Name,
            Notes = c.Notes,
            Months = c.Months,
            DistanceKm = c.Miles * KmPerMile,
            Featured = c.Featured,
            SortOrder = i,
        }).ToList();
}
