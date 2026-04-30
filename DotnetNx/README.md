# DotnetNx

DotnetNx is a .NET-first home for Nx and `@nx/dotnet` improvements aimed at MAUI-heavy monorepos.

The starting point is the experimental work from `dotnet/maui-labs#204`: affected builds, Nx cache trials, MSBuild SDK resolver setup, and per-project host OS routing. This folder moves those ideas into reusable packages and actions so consuming repositories do not need checked-in `nx` wrappers, custom JavaScript resolver scripts, root `package.json` scripts, or hand-maintained per-project Nx metadata.

## Goals

- Keep MSBuild, project evaluation, SDK resolver discovery, and host routing logic in C#/.NET.
- Use JavaScript only where Nx requires a Node plugin entry point.
- Provide `nxdn` as the stable command-line entrypoint for .NET repositories invoking Nx.
- Provide a NuGet package for MSBuild props, targets, and tasks.
- Provide composite GitHub Actions so workflows can call common affected-build flows without copying large YAML blocks.

## Layout

```text
DotnetNx/
  src/
    DotnetNx.Core/       Shared project metadata, host OS routing, and SDK resolver logic.
    DotnetNx.MSBuild/    NuGet package with MSBuild task/targets.
    DotnetNx.Tool/       nxdn .NET global tool.
  npm/
    dotnet-nx/           Thin Nx plugin that shells out to nxdn.
  actions/
    setup-nxdn/          Composite action for tool setup and resolver environment export.
    affected-info/       Composite action for affected project reporting.
    run-affected/        Composite action for running affected Nx targets.
  tests/
    DotnetNx.Core.Tests/
    DotnetNx.Tool.Tests/
```

## `NxBuildableOn`

Projects can declare which GitHub runner host OSes can build them:

```xml
<PropertyGroup>
  <NxBuildableOn>macos</NxBuildableOn>
</PropertyGroup>
```

Supported values are `linux`, `macos`, `windows`, `any`, and `all`. Values can be separated by semicolons, commas, or whitespace.

When `NxBuildableOn` is unset, DotnetNx evaluates target frameworks and infers initial defaults:

- `-ios`, `-maccatalyst`, `-tvos`, and `-macos` target framework suffixes route to `macos`.
- `-windows` target framework suffixes route to `windows`.
- Plain managed projects and Android projects default to `linux`, `macos`, and `windows`.
- Projects buildable on all supported hosts also receive `os:any`.

Unlike the trial JavaScript plugin, this is based on MSBuild evaluation, so values imported from `Directory.Build.props`, package props, SDK props, and conditional property groups are resolved through MSBuild.

## `nxdn`

`nxdn` is a .NET global tool intended to wrap Nx invocation:

```bash
nxdn export-env --format github
nxdn project-metadata --workspace .
nxdn nx -- affected -t build --base=<sha> --head=<sha>
nxdn affected -- -t test --base=<sha> --head=<sha> --projects=tag:os:macos
nxdn diagnose
```

The tool locates the selected .NET SDK, computes MSBuild SDK resolver environment variables, and then invokes Nx with that environment applied.

## Nx plugin

The npm package under `npm/dotnet-nx` is deliberately small. It implements Nx's `createNodesV2` surface and delegates metadata generation to:

```bash
nxdn project-metadata --workspace <repo>
```

That keeps MSBuild evaluation and SDK path discovery in .NET while still satisfying Nx's Node plugin model.

## GitHub Actions

Initial composite actions are provided under `actions/`:

- `setup-nxdn`: setup .NET/Node, install `DotnetNx.Tool`, and export resolver environment.
- `affected-info`: compute and summarize affected projects.
- `run-affected`: run an affected target with optional `os:<host>` filtering.

Example workflow shape:

```yaml
steps:
  - uses: actions/checkout@v6
    with:
      fetch-depth: 0
  - uses: ./DotnetNx/actions/setup-nxdn
  - id: affected
    uses: ./DotnetNx/actions/affected-info
  - uses: ./DotnetNx/actions/run-affected
    with:
      target: build
      base: ${{ steps.affected.outputs.base }}
      head: ${{ steps.affected.outputs.head }}
      os-tag: macos
```

## Publishing

The repository includes two top-level workflows for DotnetNx:

- `DotnetNx CI` validates the .NET projects, `nxdn project-metadata`, the npm plugin, and NuGet packing on PRs and pushes that touch DotnetNx files.
- `Publish DotnetNx packages` is a manual `workflow_dispatch` workflow that publishes all DotnetNx NuGet packages to `https://nuget.pkg.github.com/Redth/index.json` and publishes `@redth/dotnet-nx` to `https://npm.pkg.github.com`.

Run the publish workflow from GitHub Actions with the package version to publish, for example `0.1.0-alpha.1`. The workflow uses `GITHUB_TOKEN` with `packages: write` permission, so no additional package secret is required for publishing to this repository owner.

## Migration notes from `maui-labs#204`

- Replace `eng/nx/nx-msbuild-resolvers.js` with `nxdn export-env` or `nxdn nx`.
- Replace regex-based `NxBuildableOn` tag extraction with the DotnetNx plugin backed by MSBuild evaluation.
- Replace checked-in `nx` wrapper scripts with `nxdn nx -- ...` where practical.
- Replace full copied workflows with the composite actions in this folder.
- Keep repository-specific exclusions in consuming workflow inputs until they can be represented as project metadata.
