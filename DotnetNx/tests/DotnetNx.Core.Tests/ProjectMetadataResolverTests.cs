using DotnetNx.Core;

namespace DotnetNx.Core.Tests;

public sealed class ProjectMetadataResolverTests
{
    [Fact]
    public void ResolveWorkspace_exposes_structured_metadata_and_explicit_tags()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <NxBuildHosts>macos</NxBuildHosts>
                <NxTags>scope:client;owner:devflow</NxTags>
              </PropertyGroup>
            </Project>
            """);
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0-ios18.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <IsPublishable>true</IsPublishable>
                <RuntimeIdentifier>ios-arm64</RuntimeIdentifier>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
            </Project>
            """);

        var metadata = new ProjectMetadataResolver().ResolveWorkspace(workspace.Root);

        var project = Assert.Single(metadata.Projects);
        Assert.Equal("application", project.ProjectType);
        Assert.Equal(["dotnet", "C#", "maui"], project.Technologies);
        Assert.True(project.Capabilities.IsExecutable);
        Assert.True(project.Capabilities.IsPublishable);
        Assert.Equal(["owner:devflow", "scope:client"], project.Tags);

        var configuration = Assert.Single(project.Configurations);
        Assert.Equal("net10.0-ios18.0", configuration.TargetFramework);
        Assert.Equal("ios", configuration.Framework?.Platform);
        Assert.Equal("18.0", configuration.Framework?.PlatformVersion);
        Assert.Equal(["ios-arm64"], configuration.RuntimeIdentifiers);

        var hostRequirement = Assert.Single(project.TargetHostRequirements);
        Assert.Equal("build", hostRequirement.Target);
        Assert.Equal(["macos"], hostRequirement.Hosts);
        Assert.Equal(DotnetMetadataSource.Explicit, hostRequirement.Source);
        Assert.Equal("Directory.Build.props", hostRequirement.SourceFile);
        Assert.Empty(metadata.Diagnostics);
    }

    [Fact]
    public void ResolveWorkspace_keeps_conditioned_tags_at_configuration_scope()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0;net10.0-android35.0</TargetFrameworks>
                <NxTags>scope:client</NxTags>
              </PropertyGroup>
              <ItemGroup>
                <NxTag Include="requires:emulator"
                       Condition="'$(TargetFramework)' == 'net10.0-android35.0'" />
              </ItemGroup>
            </Project>
            """);

        var project = Assert.Single(new ProjectMetadataResolver().ResolveWorkspace(workspace.Root).Projects);

        Assert.Equal(["scope:client"], project.ExplicitTags);
        Assert.Equal(["scope:client"], project.Tags);
        var managed = Assert.Single(project.Configurations, configuration => configuration.TargetFramework == "net10.0");
        var android = Assert.Single(project.Configurations, configuration => configuration.TargetFramework == "net10.0-android35.0");
        Assert.Equal(["scope:client"], managed.ExplicitTags);
        Assert.Equal(["requires:emulator", "scope:client"], android.ExplicitTags);
    }

    [Fact]
    public void ResolveWorkspace_infers_capabilities_without_turning_them_into_tags()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/Tests/Tests.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>true</IsPackable>
                <PackageId>Contoso.Tests</PackageId>
                <PackAsTool>true</PackAsTool>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
              </ItemGroup>
            </Project>
            """);

        var project = Assert.Single(new ProjectMetadataResolver().ResolveWorkspace(workspace.Root).Projects);

        Assert.True(project.Capabilities.IsTest);
        Assert.True(project.Capabilities.IsPackable);
        Assert.True(project.Capabilities.IsTool);
        Assert.Equal(["contoso.tests"], project.Capabilities.PackageIds);
        Assert.Empty(project.Tags);
    }

    [Fact]
    public void ResolveWorkspace_intersects_hosts_for_the_unqualified_build_target()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0;net10.0-ios18.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);

        var project = Assert.Single(new ProjectMetadataResolver().ResolveWorkspace(workspace.Root).Projects);

        var requirement = Assert.Single(project.TargetHostRequirements);
        Assert.Equal("build", requirement.Target);
        Assert.Equal(["macos"], requirement.Hosts);
        Assert.Equal(DotnetMetadataSource.Inferred, requirement.Source);
    }

    [Fact]
    public void ResolveWorkspace_does_not_publish_a_host_when_frameworks_have_no_shared_host()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-ios18.0;net10.0-windows10.0.19041.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);

        var project = Assert.Single(new ProjectMetadataResolver().ResolveWorkspace(workspace.Root).Projects);

        Assert.Empty(project.TargetHostRequirements);
        Assert.Contains(project.Diagnostics, diagnostic => diagnostic.Code == "DNX003");
    }

    [Fact]
    public void ResolveWorkspace_keeps_target_specific_explicit_host_requirements()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <NxBuildHosts>linux;macos;windows</NxBuildHosts>
                <NxTestHosts>linux</NxTestHosts>
                <NxPublishHosts>windows</NxPublishHosts>
              </PropertyGroup>
            </Project>
            """);

        var project = Assert.Single(new ProjectMetadataResolver().ResolveWorkspace(workspace.Root).Projects);

        Assert.Equal(3, project.TargetHostRequirements.Count);
        Assert.Equal(["linux", "macos", "windows"], project.TargetHostRequirements.Single(r => r.Target == "build").Hosts);
        Assert.Equal(["linux"], project.TargetHostRequirements.Single(r => r.Target == "test").Hosts);
        Assert.Equal(["windows"], project.TargetHostRequirements.Single(r => r.Target == "publish").Hosts);
        Assert.All(project.TargetHostRequirements, requirement =>
            Assert.Equal(DotnetMetadataSource.Explicit, requirement.Source));
    }

    [Fact]
    public void ResolveWorkspace_warns_for_legacy_nxbuildableon()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <NxBuildableOn>macos</NxBuildableOn>
              </PropertyGroup>
            </Project>
            """);

        var project = Assert.Single(new ProjectMetadataResolver().ResolveWorkspace(workspace.Root).Projects);

        Assert.Equal(["macos"], Assert.Single(project.TargetHostRequirements).Hosts);
        Assert.Contains(project.Diagnostics, diagnostic => diagnostic.Code == "DNX002");
    }

    [Fact]
    public void ResolveWorkspace_reports_invalid_explicit_hosts()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <NxBuildHosts>beos</NxBuildHosts>
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
