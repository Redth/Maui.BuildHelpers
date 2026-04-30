# @redth/dotnet-nx

Thin Nx plugin for DotnetNx.

Nx plugin entry points run in Node, so this package intentionally keeps JavaScript small and delegates MSBuild-aware work to the `nxdn` .NET global tool.

```json
{
  "plugins": [
    "@nx/dotnet",
    "@redth/dotnet-nx"
  ]
}
```

Set `DOTNET_NX_NXDN` or the plugin `nxdnPath` option when `nxdn` is not on `PATH`.
