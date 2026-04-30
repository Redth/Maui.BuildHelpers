using DotnetNx.Core;

namespace DotnetNx.Core.Tests;

public sealed class HostOperatingSystemsTests
{
    [Theory]
    [InlineData("net10.0-ios", "macos")]
    [InlineData("net10.0-maccatalyst", "macos")]
    [InlineData("net10.0-tvos", "macos")]
    [InlineData("net10.0-macos", "macos")]
    [InlineData("net10.0-windows10.0.19041.0", "windows")]
    public void InferFromTargetFramework_routes_platform_tfms_to_required_host(string targetFramework, string expectedHost)
    {
        Assert.Equal([expectedHost], HostOperatingSystems.InferFromTargetFramework(targetFramework));
    }

    [Fact]
    public void InferFromTargetFramework_keeps_plain_managed_and_android_projects_buildable_everywhere()
    {
        Assert.Equal(["linux", "macos", "windows"], HostOperatingSystems.InferFromTargetFrameworks(["net10.0", "net10.0-android"]));
    }

    [Fact]
    public void ToTags_adds_any_tag_when_all_hosts_are_supported()
    {
        Assert.Equal(["os:linux", "os:macos", "os:windows", "os:any"], HostOperatingSystems.ToTags(["linux", "macos", "windows"]));
    }
}
