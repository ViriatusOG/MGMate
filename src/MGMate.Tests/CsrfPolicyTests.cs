using MGMate.Api.Security;

namespace MGMate.Tests;

public class CsrfPolicyTests
{
    private static readonly string[] Allowed = ["https://app.mgmate.io", "https://www.mgmate.io"];

    [Theory]
    [InlineData("https://app.mgmate.io")]
    [InlineData("https://www.mgmate.io")]
    [InlineData("HTTPS://APP.MGMATE.IO")]
    [InlineData("Https://App.Mgmate.Io")]
    public void IsOriginAllowed_KnownOrigin_ReturnsTrue(string origin)
    {
        Assert.True(CsrfPolicy.IsOriginAllowed(origin, Allowed));
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("https://mgmate.io.evil.com")]
    [InlineData("http://app.mgmate.io")]
    [InlineData("https://app.mgmate.io.evil.com")]
    public void IsOriginAllowed_UnknownOrigin_ReturnsFalse(string origin)
    {
        Assert.False(CsrfPolicy.IsOriginAllowed(origin, Allowed));
    }

    [Fact]
    public void IsOriginAllowed_EmptyAllowedList_ReturnsFalse()
    {
        Assert.False(CsrfPolicy.IsOriginAllowed("https://app.mgmate.io", []));
    }
}
