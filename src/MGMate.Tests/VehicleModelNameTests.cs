using MGMate.Core.Helpers;

namespace MGMate.Tests;

public class VehicleModelNameTests
{
    [Theory]
    [InlineData("MG4 EV 2026 Andes Grey", "MG4 EV 2026")]
    [InlineData("HS SUPER HYBRID 2025 Black Pearl", "HS SUPER HYBRID 2025")]
    [InlineData("MG4 EV 2026", "MG4 EV 2026")]
    [InlineData("MG4", "MG4")]
    [InlineData("  MG ZS EV  ", "MG ZS EV")]
    [InlineData("MG5 2024 2025", "MG5 2024 2025")]
    public void Drops_MGs_colour_name_after_the_model_year(string model, string expected) =>
        Assert.Equal(expected, VehicleModelName.WithoutColour(model));
}
