using DotnetNx.Tool;

namespace DotnetNx.Tool.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void Run_prints_help()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Program.Run(["--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("nxdn export-env", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void Run_rejects_unknown_command()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Program.Run(["nope"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown command 'nope'", error.ToString());
    }

    [Fact]
    public void ConfigureNx_write_creates_missing_plugin_configuration()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "package.json",
            """
            {
              "devDependencies": {
                "@nx/dotnet": "22.7.0",
                "@redth/dotnet-nx": "0.1.0"
              }
            }
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Program.Run(["configure-nx", "--workspace", workspace.Root, "--write"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        var nxJson = File.ReadAllText(Path.Combine(workspace.Root, "nx.json"));
        Assert.Contains("\"@nx/dotnet\"", nxJson);
        Assert.Contains("\"@redth/dotnet-nx\"", nxJson);
    }

    [Fact]
    public void ConfigureNx_without_write_fails_when_plugins_are_missing()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write("nx.json", "{}");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Program.Run(["configure-nx", "--workspace", workspace.Root], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("nx.json does not contain a plugins array", error.ToString());
    }

    [Fact]
    public void ConfigureNx_write_configures_selector_tags_without_discarding_existing_options()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.Write(
            "nx.json",
            """
            {
              "plugins": [
                "@nx/dotnet",
                {
                  "plugin": "@redth/dotnet-nx",
                  "options": {
                    "nxdnPath": "./tools/nxdn"
                  }
                }
              ]
            }
            """);
        workspace.Write(
            "package.json",
            """
            {
              "devDependencies": {
                "@nx/dotnet": "22.7.0",
                "@redth/dotnet-nx": "0.1.0"
              }
            }
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = Program.Run(
            [
                "configure-nx",
                "--workspace", workspace.Root,
                "--write",
                "--selector-tags", "platform,host",
                "--host-target", "test",
                "--include-inferred-host-selectors", "false",
            ],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        var nxJson = File.ReadAllText(Path.Combine(workspace.Root, "nx.json"));
        Assert.Contains("\"nxdnPath\": \"./tools/nxdn\"", nxJson);
        Assert.Contains("\"platform\"", nxJson);
        Assert.Contains("\"host\"", nxJson);
        Assert.Contains("\"hostTarget\": \"test\"", nxJson);
        Assert.Contains("\"includeInferredHostSelectors\": false", nxJson);
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
            var root = Path.Combine(Path.GetTempPath(), "dotnetnx-tool-tests", Guid.NewGuid().ToString("N"));
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
