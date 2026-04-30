using DotnetNx.Core;

namespace DotnetNx.Core.Tests;

public sealed class DotnetSdkResolverTests
{
    [Fact]
    public void GetSdkDirectory_resolves_selected_sdk_from_dotnet_list_output()
    {
        var sdkDirectory = DotnetSdkResolver.GetSdkDirectory(
            """
            9.0.305 [/usr/local/share/dotnet/sdk]
            10.0.203 [/usr/local/share/dotnet/sdk]
            """,
            "10.0.203");

        Assert.Equal(Path.Combine("/usr/local/share/dotnet/sdk", "10.0.203"), sdkDirectory);
    }
}
