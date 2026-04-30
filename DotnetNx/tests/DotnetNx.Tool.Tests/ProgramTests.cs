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
}
