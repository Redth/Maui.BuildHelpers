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

Project tags come from `nxdn project-metadata`, including `NxBuildableOn`, `NxTags`, `NxTag` MSBuild items, and conservative DotnetNx inferred tags such as `os:*`, `tfm:*`, `platform:*`, `type:*`, and `sdk:maui`. The merged tags are written to Nx `tags`; provenance is available under `metadata.dotnetNx.explicitTags` and `metadata.dotnetNx.inferredTags`.
