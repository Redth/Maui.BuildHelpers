using System.Text;

namespace DotnetNx.Core;

public static class EnvironmentWriters
{
    public static string ToShellExports(IReadOnlyDictionary<string, string> variables)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in variables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append("export ")
                .Append(key)
                .Append("=\"")
                .Append(value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal))
                .AppendLine("\"");
        }

        return builder.ToString();
    }

    public static string ToGithubEnvironmentFile(IReadOnlyDictionary<string, string> variables, bool includePath = true)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in variables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!includePath && string.Equals(key, "PATH", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.Append(key).Append('=').AppendLine(value);
        }

        return builder.ToString();
    }
}
