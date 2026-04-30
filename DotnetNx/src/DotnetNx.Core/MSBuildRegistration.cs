using Microsoft.Build.Locator;

namespace DotnetNx.Core;

public static class MSBuildRegistration
{
    private static readonly object Gate = new();

    public static void EnsureRegistered()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (Gate)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }
}
