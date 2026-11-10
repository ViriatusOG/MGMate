using MGMate.Api.Authentication;
using Microsoft.Extensions.Configuration;

namespace MGMate.Tests;

public class PasswordLoginOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    // A session carries the stamp of the credentials it was started with, and is refused once
    // they change: the stamp has to move with either of them, and with nothing else.
    [Fact]
    public void CredentialStamp_ChangesWithThePasswordOrTheUsername_AndOnlyThen()
    {
        static PasswordLoginOptions With(string username, string password) =>
            PasswordLoginOptions.Resolve(Config(("Auth:Username", username), ("Auth:Password", password)), oidcEnabled: false);

        var stamp = With("garage", "garage-secret").CredentialStamp;

        Assert.Equal(stamp, With("garage", "garage-secret").CredentialStamp);
        Assert.NotEqual(stamp, With("garage", "another-secret").CredentialStamp);
        Assert.NotEqual(stamp, With("someone", "garage-secret").CredentialStamp);
        Assert.DoesNotContain("garage-secret", stamp);
    }

    [Fact]
    public void Resolve_PrefersDedicatedAuthCredentials()
    {
        var options = PasswordLoginOptions.Resolve(
            Config(
                ("Auth:Username", "garage"),
                ("Auth:Password", "garage-secret"),
                ("SAIC_USER", "mg@example.com"),
                ("SAIC_PASSWORD", "mg-secret")),
            oidcEnabled: false);

        Assert.Equal("garage", options.Username);
        Assert.Equal("garage-secret", options.Password);
        Assert.True(options.Enabled);
    }

    [Fact]
    public void Resolve_FallsBackToTheMgAccount_SoExistingInstallsKeepWorking()
    {
        var options = PasswordLoginOptions.Resolve(
            Config(("SAIC_USER", "mg@example.com"), ("SAIC_PASSWORD", "mg-secret")),
            oidcEnabled: false);

        Assert.Equal("mg@example.com", options.Username);
        Assert.True(options.Enabled);
    }

    [Fact]
    public void Resolve_IgnoresBlankValues()
    {
        var options = PasswordLoginOptions.Resolve(
            Config(
                ("Auth:Username", "   "),
                ("Auth:Password", ""),
                ("SAIC_USER", "mg@example.com"),
                ("SAIC_PASSWORD", "mg-secret")),
            oidcEnabled: false);

        Assert.Equal("mg@example.com", options.Username);
    }

    [Fact]
    public void Resolve_DisablesPasswordLogin_WhenOidcIsConfigured()
    {
        var options = PasswordLoginOptions.Resolve(
            Config(("Auth:Username", "garage"), ("Auth:Password", "garage-secret")),
            oidcEnabled: true);

        Assert.False(options.Enabled);
        // Still reported as configured, which is what the startup log distinguishes.
        Assert.True(options.Configured);
        Assert.False(options.ExplicitlyConfigured);
    }

    [Fact]
    public void Resolve_KeepsPasswordLoginAlongsideOidc_WhenExplicitlyEnabled()
    {
        var options = PasswordLoginOptions.Resolve(
            Config(
                ("Auth:Username", "garage"),
                ("Auth:Password", "garage-secret"),
                ("Auth:PasswordLoginEnabled", "true")),
            oidcEnabled: true);

        Assert.True(options.Enabled);
        Assert.True(options.ExplicitlyConfigured);
    }

    [Fact]
    public void Resolve_TurnsPasswordLoginOff_WhenExplicitlyDisabledWithoutOidc()
    {
        var options = PasswordLoginOptions.Resolve(
            Config(
                ("Auth:Username", "garage"),
                ("Auth:Password", "garage-secret"),
                ("Auth:PasswordLoginEnabled", "false")),
            oidcEnabled: false);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void Resolve_CannotEnablePasswordLoginWithoutCredentials()
    {
        var options = PasswordLoginOptions.Resolve(
            Config(("Auth:PasswordLoginEnabled", "true")),
            oidcEnabled: true);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void Resolve_IgnoresABlankToggle()
    {
        // Docker passes the variable through as an empty string when the user leaves it unset.
        var options = PasswordLoginOptions.Resolve(
            Config(
                ("Auth:Username", "garage"),
                ("Auth:Password", "garage-secret"),
                ("Auth:PasswordLoginEnabled", "")),
            oidcEnabled: false);

        Assert.True(options.Enabled);
        Assert.False(options.ExplicitlyConfigured);
    }

    [Fact]
    public void Resolve_IsNotEnabled_WithoutCredentials()
    {
        var options = PasswordLoginOptions.Resolve(Config(), oidcEnabled: false);

        Assert.False(options.Enabled);
        Assert.False(options.Configured);
    }
}
