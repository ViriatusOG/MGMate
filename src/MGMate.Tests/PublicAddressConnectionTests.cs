using System.Net;
using MGMate.Worker.Services;

namespace MGMate.Tests;

public class PublicAddressConnectionTests
{
    [Theory]
    [InlineData("142.250.180.10")] // a push service
    [InlineData("2a00:1450:4001:80b::200a")]
    [InlineData("::ffff:142.250.180.10")]
    public void IsPublic_AllowsInternetAddresses(string address) =>
        Assert.True(PublicAddressConnection.IsPublic(IPAddress.Parse(address)));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.18.0.3")] // a Docker network
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")] // cloud metadata
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:192.168.1.10")]
    public void IsPublic_RefusesPrivateAndSpecialAddresses(string address) =>
        Assert.False(PublicAddressConnection.IsPublic(IPAddress.Parse(address)));
}
