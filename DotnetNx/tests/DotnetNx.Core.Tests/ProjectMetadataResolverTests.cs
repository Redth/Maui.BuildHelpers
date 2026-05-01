using DotnetNx.Core;

namespace DotnetNx.Core.Tests;

public sealed class ProjectMetadataResolverTests
{
    [Fact]
    public void ResolveWorkspace_uses_msbuild_imports_for_nxbuildableon()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <NxBuildableOn>macos</NxBuildableOn>
              </PropertyGroup>
            </Project>
            """);
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var metadata = new ProjectMetadataResolver().ResolveWorkspace(workspace.Root);

        var project = Assert.Single(metadata.Projects);
        Assert.Equal("src/App/App.csproj", project.ProjectFile);
        Assert.Equal(["macos"], project.BuildableOn);
        Assert.Empty(project.ExplicitTags);
        Assert.Contains("os:macos", project.InferredTags);
        Assert.Contains("os:macos", project.Tags);
        Assert.Equal("explicit", project.Resolution);
        Assert.Empty(metadata.Diagnostics);
    }

    [Fact]
    public void ResolveWorkspace_exposes_explicit_and_inferred_tags()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/Tests/Tests.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>false</IsPackable>
                <IsTestProject>true</IsTestProject>
                <NxTags>scope:maui; type:integration-test
                  device:android</NxTags>
              </PropertyGroup>
              <ItemGroup>
                <NxTag Include="owner:devflow" />
                <NxTag Include="requires:emulator" Condition="'$(TargetFramework)' == 'net10.0'" />
              </ItemGroup>
            </Project>
            """);

        var metadata = new ProjectMetadataResolver().ResolveWorkspace(workspace.Root);

        var project = Assert.Single(metadata.Projects);
        Assert.Equal(
            ["device:android", "owner:devflow", "requires:emulator", "scope:maui", "type:integration-test"],
            project.ExplicitTags);
        Assert.Contains("os:any", project.InferredTags);
        Assert.Contains("os:linux", project.InferredTags);
        Assert.Contains("os:macos", project.InferredTags);
        Assert.Contains("os:windows", project.InferredTags);
        Assert.Contains("tfm:net10.0", project.InferredTags);
        Assert.Contains("type:test", project.InferredTags);
        Assert.Contains("requires:emulator", project.Tags);
        Assert.Contains("tfm:net10.0", project.Tags);
        Assert.Contains("type:test", project.Tags);
        Assert.Empty(metadata.Diagnostics);
    }

    [Theory]
    [InlineData("net10.0-android", "platform:android")]
    [InlineData("net10.0-ios", "platform:ios")]
    [InlineData("net10.0-maccatalyst", "platform:maccatalyst")]
    [InlineData("net10.0-tvos", "platform:tvos")]
    [InlineData("net10.0-macos", "platform:macos")]
    [InlineData("net10.0-windows10.0.19041.0", "platform:windows")]
    public void InferFromTargetFramework_adds_platform_tags(string targetFramework, string expectedTag)
    {
        var tags = NxTags.InferFromTargetFramework(targetFramework);

        Assert.Contains(expectedTag, tags);
        Assert.Contains($"tfm:{targetFramework.ToLowerInvariant()}", tags);
    }

    [Fact]
    public void ResolveWorkspace_reports_invalid_explicit_values()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <NxBuildableOn>beos</NxBuildableOn>
              </PropertyGroup>
            </Project>
            """);

        var metadata = new ProjectMetadataResolver().ResolveWorkspace(workspace.Root);

        Assert.Single(metadata.Projects.Single().Diagnostics, diagnostic =>
            diagnostic.Code == "DNX001" &&
            diagnostic.Severity == DotnetNxDiagnosticSeverity.Error);
        Assert.Single(metadata.Diagnostics, diagnostic => diagnostic.Code == "DNX001");
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TemporaryWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dotnetnx-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TemporaryWorkspace(root);
        }

        public void Write(string relativePath, string contents)
        {
            var fullPath = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
