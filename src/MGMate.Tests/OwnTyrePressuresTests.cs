using System.Text.Json;
using MGMate.Api.Endpoints;
using MGMate.Core.Configuration;
using MGMate.Core.Helpers;

namespace MGMate.Tests;

public class OwnTyrePressuresTests
{
    private static IReadOnlyDictionary<string, JsonElement> Ui(string json) =>
        new Dictionary<string, JsonElement> { ["ui"] = JsonDocument.Parse(json).RootElement };

    private const string Both = """
        {"tyrePressures": {"season": "SEASON",
          "summer": {"frontBar": 2.48, "rearBar": 2.48},
          "winter": {"frontBar": 2.62, "rearBar": 2.7}}}
        """;

    [Fact]
    public void From_TakesThePairForTheSeasonSwitchedTo()
    {
        Assert.Equal(new OwnTyrePressures("winter", 2.62, 2.7), OwnTyrePressures.From(Ui(Both.Replace("SEASON", "winter"))));
        Assert.Equal(new OwnTyrePressures("summer", 2.48, 2.48), OwnTyrePressures.From(Ui(Both.Replace("SEASON", "summer"))));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"tyrePressures": null}""")]
    [InlineData("""{"tyrePressures": {"season": "spring", "spring": {"frontBar": 2.5, "rearBar": 2.5}}}""")]
    [InlineData("""{"tyrePressures": {"season": "summer"}}""")]
    [InlineData("""{"tyrePressures": {"season": "summer", "summer": {"frontBar": 36, "rearBar": 36}}}""")]
    [InlineData("""{"tyrePressures": {"season": "summer", "summer": {"frontBar": "2.5", "rearBar": 2.5}}}""")]
    public void From_IgnoresWhatIsMissingOrNonsense(string json)
    {
        Assert.Null(OwnTyrePressures.From(Ui(json)));
    }

    [Fact]
    public void From_WithNoUiSettings_IsNull()
    {
        Assert.Null(OwnTyrePressures.From(new Dictionary<string, JsonElement>()));
    }

    [Fact]
    public void Dto_UsesTheAccountsOwnPressuresOverTheDeployments()
    {
        var own = new OwnTyrePressures("winter", 2.62, 2.7);

        var dto = TyrePressureThresholdsDto.From(TyrePressureThresholds.Default, own);

        Assert.Equal(2.62, dto.RecommendedBar);
        Assert.Equal(2.62, dto.RecommendedFrontBar);
        Assert.Equal(2.7, dto.RecommendedRearBar);
        Assert.Equal("winter", dto.Season);
        Assert.Equal(2.5, dto.DefaultRecommendedBar);
        Assert.Equal(TyrePressureThresholds.Default.LowBar, dto.LowBar);
    }

    [Fact]
    public void Dto_WithoutOwnPressures_UsesTheDeploymentsForFrontAndRear()
    {
        var dto = TyrePressureThresholdsDto.From(TyrePressureThresholds.Default, null);

        Assert.Equal(2.5, dto.RecommendedFrontBar);
        Assert.Equal(2.5, dto.RecommendedRearBar);
        Assert.Null(dto.Season);
    }
}
