using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotnetNx.Core;

namespace DotnetNx.Tool;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            WriteHelp(output);
            return 0;
        }

        try
        {
            var command = args[0];
            var commandArgs = args.Skip(1).ToList();
            return command switch
            {
                "export-env" => ExportEnvironment(commandArgs, output, error),
                "project-metadata" => WriteProjectMetadata(commandArgs, output),
                "diagnose" => Diagnose(commandArgs, output),
                "configure-nx" => ConfigureNx(commandArgs, output),
                "nx" => RunNx(commandArgs),
                "affected" => RunNxWithPrefix("affected", commandArgs),
                "show-projects" => RunNxWithPrefix("show projects", commandArgs),
                _ => UnknownCommand(command, error),
            };
        }
        catch (Exception ex)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int ExportEnvironment(List<string> args, TextWriter output, TextWriter error)
    {
        var workspaceRoot = GetWorkspace(args);
        var format = TakeOption(args, "--format") ?? "shell";
        var resolver = new DotnetSdkResolver();
        var environment = resolver.Resolve(workspaceRoot);

        switch (format)
        {
            case "json":
                output.WriteLine(JsonServices.Serialize(environment));
                return 0;
            case "github":
                WriteGithubEnvironment(environment, output, error);
                return 0;
            case "shell":
                output.Write(EnvironmentWriters.ToShellExports(environment.Variables));
                return 0;
            default:
                throw new ArgumentException($"Unsupported export format '{format}'. Use shell, github, or json.");
        }
    }

    private static int WriteProjectMetadata(List<string> args, TextWriter output)
    {
        var workspaceRoot = GetWorkspace(args);
        var projectFiles = TakeOptions(args, "--project");
        var resolver = new ProjectMetadataResolver();
        var metadata = resolver.ResolveWorkspace(workspaceRoot, projectFiles.Count == 0 ? null : projectFiles);

        output.WriteLine(JsonServices.Serialize(metadata));
        return metadata.Diagnostics.Any(diagnostic => diagnostic.Severity == DotnetNxDiagnosticSeverity.Error) ? 1 : 0;
    }

    private static int Diagnose(List<string> args, TextWriter output)
    {
        var workspaceRoot = GetWorkspace(args);
        var sdkEnvironment = new DotnetSdkResolver().Resolve(workspaceRoot);
        var projects = ProjectMetadataResolver.DiscoverProjectFiles(workspaceRoot);

        output.WriteLine("DotnetNx diagnostics");
        output.WriteLine($"Workspace: {Path.GetFullPath(workspaceRoot)}");
        output.WriteLine($"dotnet: {sdkEnvironment.DotnetPath}");
        output.WriteLine($"DOTNET_ROOT: {sdkEnvironment.DotnetRoot}");
        output.WriteLine($"SDK: {sdkEnvironment.SdkVersion}");
        output.WriteLine($"SDK directory: {sdkEnvironment.SdkDirectory}");
        output.WriteLine($"MSBuild SDK resolvers: {sdkEnvironment.SdkResolversDirectory}");
        output.WriteLine($"MSBuild SDKs: {sdkEnvironment.SdksDirectory}");
        output.WriteLine($"Projects discovered: {projects.Count}");
        return 0;
    }

    private static int ConfigureNx(List<string> args, TextWriter output)
    {
        var workspaceRoot = GetWorkspace(args);
        var write = TakeFlag(args, "--write");
        var dotnetPlugin = TakeOption(args, "--dotnet-plugin") ?? "@nx/dotnet";
        var plugin = TakeOption(args, "--plugin") ?? "@redth/dotnet-nx";
        var requiredPlugins = new[] { dotnetPlugin, plugin };
        var nxJsonPath = Path.Combine(workspaceRoot, "nx.json");
        var root = ReadNxJson(nxJsonPath, write);

        var plugins = root["plugins"] as JsonArray;
        if (plugins is null)
        {
            if (!write)
            {
                throw new InvalidOperationException($"nx.json does not contain a plugins array. Run 'nxdn configure-nx --write' to add {dotnetPlugin} and {plugin}.");
            }

            plugins = [];
            root["plugins"] = plugins;
        }

        var missing = requiredPlugins
            .Where(requiredPlugin => !ContainsPlugin(plugins, requiredPlugin))
            .ToArray();
        if (missing.Length > 0 && !write)
        {
            throw new InvalidOperationException($"nx.json is missing required plugin entries: {string.Join(", ", missing)}. Run 'nxdn configure-nx --write' to add them.");
        }

        foreach (var missingPlugin in missing)
        {
            plugins.Add(missingPlugin);
        }

        if (missing.Length > 0)
        {
            Directory.CreateDirectory(workspaceRoot);
            File.WriteAllText(nxJsonPath, root.ToJsonString(JsonServices.Options) + Environment.NewLine);
            output.WriteLine($"Updated {Path.GetRelativePath(Environment.CurrentDirectory, nxJsonPath)} with {string.Join(", ", missing)}.");
        }
        else
        {
            output.WriteLine("nx.json already contains required DotnetNx plugin entries.");
        }

        WarnIfPackageMissing(workspaceRoot, dotnetPlugin, output);
        WarnIfPackageMissing(workspaceRoot, plugin, output);
        return 0;
    }

    private static int RunNx(List<string> args)
    {
        var workspaceRoot = GetWorkspace(args);
        var forwardedArgs = StripDoubleDash(args);
        return InvokeNx(workspaceRoot, forwardedArgs);
    }

    private static int RunNxWithPrefix(string prefix, List<string> args)
    {
        var workspaceRoot = GetWorkspace(args);
        var forwardedArgs = prefix
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Concat(StripDoubleDash(args))
            .ToArray();

        return InvokeNx(workspaceRoot, forwardedArgs);
    }

    private static int InvokeNx(string workspaceRoot, IReadOnlyList<string> nxArgs)
    {
        var sdkEnvironment = new DotnetSdkResolver().Resolve(workspaceRoot);
        var nxCommand = ResolveNxCommand(workspaceRoot);

        var startInfo = new ProcessStartInfo(nxCommand.FileName)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetFullPath(workspaceRoot),
        };

        foreach (var argument in nxCommand.PrefixArguments.Concat(nxArgs))
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in sdkEnvironment.Variables)
        {
            startInfo.Environment[key] = value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start Nx through '{nxCommand.FileName}'.");
        process.WaitForExit();
        return process.ExitCode;
    }

    private static NxCommand ResolveNxCommand(string workspaceRoot)
    {
        var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var nxWrapper = Path.Combine(fullWorkspaceRoot, ".nx", "nxw.js");
        if (File.Exists(nxWrapper))
        {
            var node = FindExecutable("node")
                ?? throw new FileNotFoundException("Could not find node. Install Node.js or add node to PATH before invoking Nx.");
            return new NxCommand(node, [nxWrapper]);
        }

        var localNx = Path.Combine(
            fullWorkspaceRoot,
            "node_modules",
            ".bin",
            OperatingSystem.IsWindows() ? "nx.cmd" : "nx");
        if (File.Exists(localNx))
        {
            return new NxCommand(localNx, []);
        }

        var globalNx = FindExecutable(OperatingSystem.IsWindows() ? "nx.cmd" : "nx");
        if (globalNx is not null)
        {
            return new NxCommand(globalNx, []);
        }

        throw new FileNotFoundException("Could not find Nx. Install Nx locally in the workspace, use a .nx/nxw.js wrapper, or add nx to PATH.");
    }

    private static void WriteGithubEnvironment(DotnetSdkResolverEnvironment environment, TextWriter output, TextWriter error)
    {
        var githubEnvironmentFile = Environment.GetEnvironmentVariable("GITHUB_ENV");
        if (string.IsNullOrWhiteSpace(githubEnvironmentFile))
        {
            output.Write(EnvironmentWriters.ToGithubEnvironmentFile(environment.Variables));
            return;
        }

        var githubPathFile = Environment.GetEnvironmentVariable("GITHUB_PATH");
        if (!string.IsNullOrWhiteSpace(githubPathFile))
        {
            File.AppendAllText(githubPathFile, environment.DotnetRoot + Environment.NewLine);
        }

        var includePath = string.IsNullOrWhiteSpace(githubPathFile);
        File.AppendAllText(githubEnvironmentFile, EnvironmentWriters.ToGithubEnvironmentFile(environment.Variables, includePath));
        error.WriteLine($"Exported DotnetNx resolver environment to {githubEnvironmentFile}.");
    }

    private static string GetWorkspace(List<string> args) =>
        Path.GetFullPath(TakeOption(args, "--workspace") ?? Environment.CurrentDirectory);

    private static string? TakeOption(List<string> args, string name)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Missing value for {name}.");
            }

            var value = args[index + 1];
            args.RemoveRange(index, 2);
            return value;
        }

        return null;
    }

    private static List<string> TakeOptions(List<string> args, string name)
    {
        var values = new List<string>();
        while (true)
        {
            var value = TakeOption(args, name);
            if (value is null)
            {
                return values;
            }

            values.Add(value);
        }
    }

    private static bool TakeFlag(List<string> args, string name)
    {
        var index = args.FindIndex(arg => string.Equals(arg, name, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        args.RemoveAt(index);
        return true;
    }

    private static string[] StripDoubleDash(IReadOnlyList<string> args)
    {
        if (args.Count > 0 && args[0] == "--")
        {
            return args.Skip(1).ToArray();
        }

        return args.ToArray();
    }

    private static string? FindExecutable(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static JsonObject ReadNxJson(string nxJsonPath, bool write)
    {
        if (!File.Exists(nxJsonPath))
        {
            if (!write)
            {
                throw new FileNotFoundException($"Could not find nx.json at {nxJsonPath}. Run 'nxdn configure-nx --write' to create it.");
            }

            return new JsonObject
            {
                ["analytics"] = false,
            };
        }

        var node = JsonNode.Parse(File.ReadAllText(nxJsonPath), documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        return node as JsonObject
            ?? throw new InvalidOperationException($"Expected nx.json to contain a JSON object: {nxJsonPath}");
    }

    private static bool ContainsPlugin(JsonArray plugins, string plugin)
    {
        foreach (var entry in plugins)
        {
            if (entry is null)
            {
                continue;
            }

            if (entry is JsonValue value &&
                value.TryGetValue<string>(out var pluginName) &&
                string.Equals(pluginName, plugin, StringComparison.Ordinal))
            {
                return true;
            }

            if (entry is JsonObject pluginObject &&
                pluginObject["plugin"] is JsonValue objectPluginValue &&
                objectPluginValue.TryGetValue<string>(out var objectPluginName) &&
                string.Equals(objectPluginName, plugin, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void WarnIfPackageMissing(string workspaceRoot, string packageName, TextWriter output)
    {
        var packageJsonPath = Path.Combine(workspaceRoot, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            output.WriteLine($"Warning: package.json was not found; ensure {packageName} is installed in the Nx workspace.");
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        if (HasDependency(document.RootElement, "dependencies", packageName) ||
            HasDependency(document.RootElement, "devDependencies", packageName) ||
            HasDependency(document.RootElement, "peerDependencies", packageName))
        {
            return;
        }

        output.WriteLine($"Warning: package.json does not list {packageName}; install it before running Nx.");
    }

    private static bool HasDependency(JsonElement root, string propertyName, string packageName) =>
        root.TryGetProperty(propertyName, out var dependencies) &&
        dependencies.ValueKind == JsonValueKind.Object &&
        dependencies.TryGetProperty(packageName, out _);

    private static int UnknownCommand(string command, TextWriter error)
    {
        error.WriteLine($"Unknown command '{command}'.");
        error.WriteLine("Run 'nxdn --help' for usage.");
        return 1;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("nxdn - .NET-first Nx helpers");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  nxdn export-env [--workspace <path>] [--format shell|github|json]");
        output.WriteLine("  nxdn project-metadata [--workspace <path>] [--project <path>]...");
        output.WriteLine("  nxdn diagnose [--workspace <path>]");
        output.WriteLine("  nxdn configure-nx [--workspace <path>] [--write] [--plugin <name>] [--dotnet-plugin <name>]");
        output.WriteLine("  nxdn nx [--workspace <path>] -- <nx args>");
        output.WriteLine("  nxdn affected [--workspace <path>] -- <nx affected args>");
        output.WriteLine("  nxdn show-projects [--workspace <path>] -- <nx show projects args>");
    }

    private sealed record NxCommand(string FileName, IReadOnlyList<string> PrefixArguments);
}
