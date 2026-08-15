# DotnetNx

DotnetNx is a .NET-first companion to the official `@nx/dotnet` plugin.

`@nx/dotnet` owns task inference: it evaluates MSBuild and creates the standard `build`, `test`, `run`, `pack`, `publish`, `restore`, `clean`, and `watch` targets. DotnetNx adds project-system facts that are useful to Nx consumers but are not currently exposed by that plugin:

- Structured, evaluated metadata for each target framework.
- Project capabilities such as test, executable, packable, publishable, and tool.
- Runtime identifiers, package IDs, project type, and technology metadata.
- Explicit Nx tags authored in MSBuild.
- Target-specific host requirements with explicit versus inferred provenance.
- Optional, namespaced selector tags for the few metadata dimensions a workspace needs to query with Nx.

The implementation is intentionally split this way so task commands, inputs, outputs, caching, dependencies, and configurations remain the responsibility of `@nx/dotnet`.

## Layout

```text
DotnetNx/
  src/
    DotnetNx.Core/       MSBuild evaluation and structured project metadata.
    DotnetNx.MSBuild/    NuGet package with MSBuild task/targets.
    DotnetNx.Tool/       nxdn .NET global tool.
  npm/
    dotnet-nx/           Thin Nx plugin that invokes nxdn.
  actions/
    setup-nxdn/          Install nxdn and export resolver environment.
    setup-cache/         Cache Nx and common .NET output paths.
    configure-nx/        Validate or write Nx plugin configuration.
    doctor/              Run diagnostics and emit metadata.
    affected-info/       Report affected projects.
    affected-matrix/     Build a target-specific host matrix.
    run-affected/        Run an affected target with optional host selection.
    run-target/          Run one Nx project target.
  tests/
```

## Metadata model

`nxdn project-metadata` evaluates every project and target framework through MSBuild. The plugin publishes the result under `metadata.dotnetNx` and contributes native Nx fields where they have established meanings:

- `projectType`: `application` for executable projects, otherwise `library`.
- `metadata.technologies`: `.NET`, project language, and detected SDK technologies such as MAUI.
- `tags`: explicit project-wide tags plus any opt-in selector projection.
- `metadata.dotnetNx.capabilities`: evaluated project capabilities.
- `metadata.dotnetNx.configurations`: per-framework metadata, runtime identifiers, conditional tags, capabilities, and host requirements.
- `metadata.dotnetNx.targetHostRequirements`: requirements that are valid for every evaluated configuration of a target.

Capabilities are structured facts, not automatic `type:*` tags. For example, a project can be executable, testable, packable, and publishable at the same time. Nx targets remain the authoritative representation of what the project can do.

## Explicit Nx tags from MSBuild

Use `NxTags` or `NxTag` for intentional workspace taxonomy such as scope, ownership, domain, or architectural layer:

```xml
<PropertyGroup>
  <NxTags>scope:client;owner:devflow</NxTags>
</PropertyGroup>

<ItemGroup>
  <NxTag Include="requires:emulator"
         Condition="'$(TargetFramework)' == 'net10.0-android'" />
</ItemGroup>
```

Tags conditioned on a target framework stay on that configuration. DotnetNx only promotes a tag to the native project `tags` field when it applies to every evaluated configuration. This avoids changing “the Android configuration requires an emulator” into “the whole project requires an emulator.”

## Target-specific host requirements

Host compatibility belongs to an executable target, not to a project in the abstract. A build, test, run, and publish operation can have different requirements.

Declare known requirements with target-specific MSBuild properties:

```xml
<PropertyGroup>
  <NxBuildHosts>macos;windows</NxBuildHosts>
  <NxTestHosts>macos</NxTestHosts>
  <NxRunHosts>macos</NxRunHosts>
  <NxPublishHosts>macos</NxPublishHosts>
  <NxPackHosts>linux;macos;windows</NxPackHosts>
  <NxRestoreHosts>linux;macos;windows</NxRestoreHosts>
</PropertyGroup>
```

Supported host values are `linux`, `macos`, `windows`, `any`, and `all`. `NxBuildableOn` remains readable for migration but is deprecated in favor of `NxBuildHosts`.

When `NxBuildHosts` is absent, DotnetNx records advisory build-host compatibility inferred from each evaluated target platform and runtime identifier. It does not infer test, run, or publish requirements because those frequently depend on devices, emulators, signing, native toolchains, or workloads that a target framework alone cannot prove.

For an unqualified Nx target, project-level compatibility uses the intersection of all evaluated configurations:

- `net10.0;net10.0-ios` has an advisory shared build host of macOS.
- `net10.0-ios;net10.0-windows` has no shared build host and therefore receives no project-level build-host requirement.

The per-framework facts remain available so a future framework-specific target configuration can represent each branch independently.

Host metadata describes compatibility; it does not verify that required workloads, SDKs, devices, signing assets, or native dependencies are installed.

## Optional selector tags

Nx can inspect arbitrary metadata, but its project selector filters by name, directory, or tag. DotnetNx therefore supports an explicit projection of selected metadata into namespaced tags.

No inferred selector tags are enabled by default:

```json
{
  "plugins": [
    "@nx/dotnet",
    {
      "plugin": "@redth/dotnet-nx",
      "options": {
        "selectorTags": [
          "target-framework",
          "platform",
          "runtime-identifier",
          "host"
        ],
        "selectorTagPrefix": "dotnet",
        "hostTarget": "build",
        "includeInferredHostSelectors": false
      }
    }
  ]
}
```

Available projections are:

| Dimension | Example |
| --- | --- |
| `target-framework` | `dotnet:tfm:net10.0-ios` |
| `platform` | `dotnet:platform:ios` |
| `runtime-identifier` | `dotnet:rid:ios-arm64` |
| `host` | `dotnet:host:build:macos` |

Host selectors use only explicit requirements unless `includeInferredHostSelectors` is enabled. Inferred host compatibility is advisory and may not reflect the tools installed on a runner.

Prefer the smallest projection needed by a real query. High-cardinality facts such as package IDs and framework versions remain structured metadata rather than becoming tags.

## `nxdn`

`nxdn` is the stable .NET entry point used by the plugin and Actions:

```bash
nxdn export-env --format github
nxdn project-metadata --workspace .
nxdn diagnose
nxdn nx -- affected -t build --base=<sha> --head=<sha>
nxdn show-projects -- --affected --base=<sha> --head=<sha> --json
```

It locates the selected .NET SDK, computes MSBuild SDK resolver variables, and invokes Nx with that environment. The wrapper is local integration infrastructure; it is not part of the proposed upstream Nx feature.

## Nx plugin

Install both plugins because they have separate responsibilities:

```json
{
  "plugins": [
    "@nx/dotnet",
    "@redth/dotnet-nx"
  ]
}
```

The npm entry point implements Nx’s `createNodesV2` surface and delegates evaluation to:

```bash
nxdn project-metadata --workspace <repo>
```

Set `DOTNET_NX_NXDN` or the plugin `nxdnPath` option when `nxdn` is not on `PATH`.

## GitHub Actions

Minimal setup:

```yaml
permissions:
  contents: read
  packages: read

steps:
  - uses: actions/checkout@v6
    with:
      fetch-depth: 0
  - uses: Redth/Maui.BuildHelpers/DotnetNx/actions/setup-nxdn@v0.3
```

Configure explicit tags only:

```yaml
- uses: Redth/Maui.BuildHelpers/DotnetNx/actions/configure-nx@v0.3
  with:
    write: true
```

Enable build-host selectors for a host matrix:

```yaml
- uses: Redth/Maui.BuildHelpers/DotnetNx/actions/configure-nx@v0.3
  with:
    write: true
    selector-tags: host
    host-target: build
    include-inferred-host-selectors: false
```

With `include-inferred-host-selectors: false`, projects need an explicit `NxBuildHosts` declaration to enter the matrix. Set it to `true` only when advisory TFM/RID inference is acceptable for the repository.

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
        with:
          target: build

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
          host: ${{ matrix.host }}
          projects-json: ${{ toJson(matrix.projects) }}
```

The matrix assigns each affected project to the first compatible host in `host-preference` (Linux, macOS, then Windows by default), so projects compatible with several hosts run only once. It fails by default if any affected project is unrouted. `affected-matrix` and `run-affected` must use the same target and selector prefix.

Run a known project target:

```yaml
- uses: Redth/Maui.BuildHelpers/DotnetNx/actions/run-target@v0.3
  with:
    project: Microsoft.Maui.DevFlow.Agent.IntegrationTests.Android
    target: test
    configuration: debug
```

Both `run-target` and `run-affected` accept:

- `env`: multiline `NAME=VALUE` entries exported before `nxdn` runs.
- `script`: a Bash setup script sourced before `nxdn` runs.

## Publishing

`DotnetNx CI` validates the .NET projects, structured metadata output, npm plugin, and NuGet packages. `Publish DotnetNx packages` publishes the NuGet and npm packages through a manual workflow.

## Upstream direction

The proposed contribution to Nx is documented in [NX_UPSTREAM_DISCUSSION.md](NX_UPSTREAM_DISCUSSION.md). It focuses on structured MSBuild metadata and framework/RID-specific target modeling, not on upstreaming `nxdn`, this companion plugin, or the GitHub Actions.

## Migration from the earlier prototype

- Replace `NxBuildableOn` with target-specific `NxBuildHosts`, `NxTestHosts`, `NxRunHosts`, or `NxPublishHosts`.
- Replace automatic `os:*`, `tfm:*`, `platform:*`, and `type:*` assumptions with structured `metadata.dotnetNx`.
- Enable only the selector-tag dimensions a consuming workflow actually queries.
- Replace `os-tag` on `run-affected` with `host`.
- Use `${{ matrix.host }}` rather than `${{ matrix.osTag }}` in affected matrices.
- Keep target commands, cache settings, outputs, dependencies, and Debug/Release configurations owned by `@nx/dotnet`.
