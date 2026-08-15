# @redth/dotnet-nx

Thin Nx companion plugin for structured .NET/MSBuild metadata.

Use it alongside the official `@nx/dotnet` plugin:

```json
{
  "plugins": [
    "@nx/dotnet",
    {
      "plugin": "@redth/dotnet-nx",
      "options": {
        "selectorTags": ["platform"],
        "selectorTagPrefix": "dotnet"
      }
    }
  ]
}
```

`@nx/dotnet` owns inferred targets, commands, inputs, outputs, caching, and dependencies. This plugin delegates MSBuild evaluation to `nxdn project-metadata` and contributes:

- Native `projectType` and `metadata.technologies`.
- Structured capabilities and per-framework metadata under `metadata.dotnetNx`.
- Explicit project-wide `NxTags`/`NxTag` values.
- Optional namespaced selector tags for target framework, platform, runtime identifier, capability, or target-specific host compatibility.

Inferred selector tags are disabled by default. Host selector tags use only explicit target requirements unless `includeInferredHostSelectors` is set.

Set `DOTNET_NX_NXDN` or `nxdnPath` when `nxdn` is not on `PATH`.
