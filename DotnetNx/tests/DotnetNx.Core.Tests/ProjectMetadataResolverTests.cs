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
        Assert.Equal(["os:macos"], project.Tags);
        Assert.Equal("explicit", project.Resolution);
        Assert.Empty(metadata.Diagnostics);
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
