using System.Text.Json;
using MGMate.Core.Helpers;

namespace MGMate.Tests;

public class OwnCarNameTests
{
    private static IReadOnlyDictionary<string, JsonElement> Ui(string json) =>
        new Dictionary<string, JsonElement> { ["ui"] = JsonDocument.Parse(json).RootElement };

    [Fact]
    public void From_TakesTheNameTheAccountSet()
    {
        Assert.Equal("MG4 EV 2026", OwnCarName.From(Ui("""{"carName": "  MG4 EV 2026 "}""")));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"carName": null}""")]
    [InlineData("""{"carName": ""}""")]
    [InlineData("""{"carName": "   "}""")]
    [InlineData("""{"carName": 2026}""")]
    [InlineData("""{"carName": "A name that goes on for far longer than any car's name would"}""")]
    public void From_IgnoresWhatIsMissingOrNonsense(string json)
    {
        Assert.Null(OwnCarName.From(Ui(json)));
    }

    [Fact]
    public void From_DropsControlCharacters()
    {
        Assert.Equal("MG4 EV", OwnCarName.From(Ui("""{"carName": "MG4\u0000 EV\n"}""")));
    }

    [Fact]
    public void From_WithNoUiSettings_IsNull()
    {
        Assert.Null(OwnCarName.From(new Dictionary<string, JsonElement>()));
    }
}
