using DotnetNx.Core;

namespace DotnetNx.Core.Tests;

public sealed class HostOperatingSystemsTests
{
    [Theory]
    [InlineData("ios", "macos")]
    [InlineData("maccatalyst", "macos")]
    [InlineData("tvos", "macos")]
    [InlineData("macos", "macos")]
    [InlineData("windows", "windows")]
    public void InferBuildHosts_routes_platform_tfms_to_advisory_host(string platform, string expectedHost)
    {
        Assert.Equal([expectedHost], HostOperatingSystems.InferBuildHosts(platform, []));
    }

    [Fact]
    public void InferBuildHosts_keeps_plain_managed_and_android_projects_advisory_on_all_hosts()
    {
        Assert.Equal(
            ["linux", "macos", "windows"],
            HostOperatingSystems.InferBuildHosts("android", []));
    }

    [Fact]
    public void InferBuildHosts_uses_runtime_identifier_when_platform_is_neutral()
    {
        Assert.Equal(
            ["linux"],
            HostOperatingSystems.InferBuildHosts(string.Empty, ["linux-arm64"]));
    }

    [Fact]
    public void Intersect_returns_hosts_shared_by_every_configuration()
    {
        Assert.Equal(
            ["macos"],
            HostOperatingSystems.Intersect(
                [
                    ["linux", "macos", "windows"],
                    ["macos"],
                ]));
    }
}
