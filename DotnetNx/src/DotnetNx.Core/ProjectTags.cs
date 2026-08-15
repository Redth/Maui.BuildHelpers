namespace DotnetNx.Core;

internal static class ProjectTags
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string> values)
    {
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            foreach (var tag in Split(value))
            {
                tags.Add(tag);
            }
        }

        return tags.ToArray();
    }

    private static IEnumerable<string> Split(string value) =>
        value
            .Split([';', ',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part));
}
