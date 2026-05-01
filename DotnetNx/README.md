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
    setup-cache/         Composite action for Nx and .NET build cache paths.
    configure-nx/        Composite action for validating or writing nx.json plugin entries.
    doctor/              Composite action for DotnetNx diagnostics.
    affected-info/       Composite action for affected project reporting.
    affected-matrix/     Composite action for OS-tagged affected matrix outputs.
    run-affected/        Composite action for running affected Nx targets.
    run-target/          Composite action for running a single Nx project target.
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

- `setup-nxdn`: setup .NET/Node, install `DotnetNx.Tool`, and export resolver environment. By default it installs from `https://nuget.pkg.github.com/Redth/index.json` using `github.token`, so consuming workflows need `packages: read`.
- `setup-cache`: restore/cache Nx cache plus common .NET build output paths.
- `configure-nx`: validate or write minimal `nx.json` plugin entries for `@nx/dotnet` and `@redth/dotnet-nx`.
- `doctor`: run `nxdn diagnose`, validate Nx configuration, and optionally emit project metadata JSON.
- `affected-info`: compute and summarize affected projects.
- `affected-matrix`: compute OS-tagged affected project lists and a GitHub Actions matrix.
- `run-affected`: run an affected target with optional `os:<host>` filtering.
- `run-target`: run a specific Nx project target through `nxdn`.

Minimal setup from another repository:

```yaml
permissions:
  contents: read
  packages: read

steps:
  - uses: actions/checkout@v6
    with:
      fetch-depth: 0
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-nxdn@v0.3
    with:
      tool-version: 0.1.0
```

Use `tool-source`, `tool-source-name`, `tool-source-username`, `github-token`, and `tool-package-id` on `setup-nxdn` when installing `nxdn` from a different NuGet feed or package ID.

Validate a consuming repo:

```yaml
steps:
  - uses: actions/checkout@v6
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-nxdn@v0.3
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/doctor@v0.3
```

Write missing Nx plugin configuration:

```yaml
steps:
  - uses: actions/checkout@v6
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-nxdn@v0.3
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/configure-nx@v0.3
    with:
      write: true
```

Example workflow shape:

```yaml
steps:
  - uses: actions/checkout@v6
    with:
      fetch-depth: 0
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-nxdn@v0.3
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-cache@v0.3
  - id: affected
    uses: Redth/Maui.BuildHelpers/DotnetNx/actions/affected-info@v0.3
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/run-affected@v0.3
    with:
      target: build
      base: ${{ steps.affected.outputs.base }}
      head: ${{ steps.affected.outputs.head }}
      os-tag: macos
```

Build an OS-tagged affected matrix:

```yaml
jobs:
  affected:
    runs-on: ubuntu-latest
    outputs:
      matrix: ${{ steps.affected.outputs.matrix }}
      has-work: ${{ steps.affected.outputs.has-work }}
      base: ${{ steps.affected.outputs.base }}
      head: ${{ steps.affected.outputs.head }}
    steps:
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0
      - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-nxdn@v0.3
      - id: affected
        uses: Redth/Maui.BuildHelpers/DotnetNx/actions/affected-matrix@v0.3

  build:
    needs: affected
    if: needs.affected.outputs.has-work == 'true'
    strategy:
      matrix: ${{ fromJson(needs.affected.outputs.matrix) }}
    runs-on: ${{ matrix.runner }}
    steps:
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0
      - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-nxdn@v0.3
      - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/run-affected@v0.3
        with:
          target: build
          base: ${{ needs.affected.outputs.base }}
          head: ${{ needs.affected.outputs.head }}
          os-tag: ${{ matrix.osTag }}
```

Run a specific project target when you already know the Nx project and target:

```yaml
steps:
  - uses: actions/checkout@v6
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-nxdn@v0.3
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/run-target@v0.3
    with:
      project: Microsoft.Maui.DevFlow.Agent.IntegrationTests.Android
      target: test
      env: |
        TEST_CONFIGURATION=Debug
      script: |
        export DEVFLOW_TEST_ANDROID_SERIAL="$(adb devices | awk '/^emulator-[0-9]+[[:space:]]+device$/ { print $1; exit }')"
        echo "Using Android emulator serial: ${DEVFLOW_TEST_ANDROID_SERIAL:-<none found>}"
```

That action runs:

```bash
nxdn nx -- run Microsoft.Maui.DevFlow.Agent.IntegrationTests.Android:test
```

You can also pass the full Nx run id directly:

```yaml
- uses: Redth/Maui.BuildHelpers/DotnetNx/actions/run-target@v0.3
  with:
    run-id: Microsoft.Maui.DevFlow.Agent.IntegrationTests.Android:test
```

Both `run-target` and `run-affected` accept:

- `env`: multiline `NAME=VALUE` entries exported before `nxdn` runs.
- `script`: a Bash setup script sourced in the same shell before `nxdn` runs, useful for computed environment values.

Cache setup defaults to `.nx/cache`, `artifacts/bin`, and `artifacts/obj`, with `extra-paths` for repo-specific additions:

```yaml
- uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-cache@v0.3
  with:
    extra-paths: |
      ~/.nuget/packages
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
