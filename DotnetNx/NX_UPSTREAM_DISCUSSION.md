# Proposed Nx feature discussion

Published as [nrwl/nx discussion #36676](https://github.com/nrwl/nx/discussions/36676).

## Title

`@nx/dotnet`: expose structured evaluated MSBuild metadata and framework/RID-specific target variants

## Body

### Description

Would the Nx team be open to extending the existing `@nx/dotnet` MSBuild analyzer so it exposes more of the evaluated project-system model and can represent framework/RID-specific task variants?

The current analyzer already has the right architecture: a thin Nx plugin invokes a C# analyzer that constructs an MSBuild `ProjectGraph`, evaluates project properties and items, and generates Nx targets with commands, inputs, outputs, caching, and dependencies.

We have been prototyping an additive metadata layer in [`Redth/Maui.BuildHelpers/DotnetNx`](https://github.com/Redth/Maui.BuildHelpers/tree/main/DotnetNx). That work started from cross-platform .NET and MAUI CI needs, but the useful concepts are general .NET project-system concepts rather than MAUI-specific behavior.

After comparing the prototype with Nx core and first-party inferred-task plugins, we think the upstream opportunity is narrower and more Nx-aligned than upstreaming our CLI wrapper, companion plugin, or Actions.

### Proposed capabilities

#### 1. Structured evaluated .NET metadata

Expose a stable, namespaced metadata model from the existing MSBuild evaluation, potentially under `metadata.dotnet`.

At project scope:

- Inferred Nx `projectType` when the analyzer can confidently distinguish an executable application from a library.
- Technologies such as `.NET`, project language, and detected SDK technologies.
- Capabilities including test, executable, packable, publishable, and tool.
- Package IDs where applicable.

Per evaluated target framework:

- Target framework short name.
- Framework identifier and version.
- Target platform identifier and version.
- Runtime identifier(s).
- Configuration-specific capabilities.
- Provenance for explicit versus inferred values.

The intent is not to mirror every MSBuild property. It is to expose a small model that affects how projects and tasks are understood by Nx.

This must come from evaluated MSBuild state rather than XML parsing so SDK defaults, imports, `Directory.Build.props`, conditions, and inner builds are handled correctly.

#### 2. Keep capabilities aligned with Nx targets

Facts such as “testable,” “packable,” “publishable,” and “runnable” are overlapping capabilities rather than mutually exclusive project types.

The generated targets should remain authoritative:

- A `test` target represents testability.
- A `pack` target represents packaging.
- A `publish` target represents publication/deployment preparation.
- A `run` target represents executability.

Structured metadata can make those evaluated facts inspectable, while `projectType` remains limited to Nx’s native `application`/`library` distinction.

We do not propose automatically generating broad `type:*` tags for these facts.

#### 3. Framework- and RID-specific target configurations

Multi-targeted projects expose a correctness issue that project-level metadata alone cannot solve.

For example, a project targeting both iOS and Windows may have no host capable of running an unqualified `dotnet build` across every target framework. Treating it as independently “macOS-compatible” and “Windows-compatible” at project scope is misleading.

Where command semantics are known, `@nx/dotnet` could represent framework/RID variants as Nx target configurations or generated target variants:

```text
build
build:net10.0
build:net10.0-ios
publish:net10.0-ios:ios-arm64
```

The exact naming is open for discussion. The important part is that each variant has correct:

- Command arguments.
- Inputs and outputs.
- Cache identity.
- Dependencies.
- Target framework and runtime identifier metadata.

Debug/Release should continue to use normal Nx target configurations rather than tags.

#### 4. Explicit tags from evaluated MSBuild

Some repositories intentionally keep project taxonomy in `Directory.Build.props` and project files. It may be useful for the analyzer to support explicit Nx tags supplied through evaluated MSBuild properties/items:

```xml
<PropertyGroup>
  <NxTags>scope:client;owner:devflow</NxTags>
</PropertyGroup>

<ItemGroup>
  <NxTag Include="requires:emulator"
         Condition="'$(TargetFramework)' == 'net10.0-android'" />
</ItemGroup>
```

A conditioned value should remain associated with that framework configuration. It should only be promoted to a project-wide tag when it applies to every evaluated configuration.

This keeps tags developer-authored and suitable for architecture/boundary rules.

#### 5. Optional selector projection

Nx project selection can filter arbitrary dimensions through tags, while custom metadata is primarily inspectable. There may therefore be value in an opt-in projection of a small allow-list of metadata dimensions into namespaced selector tags:

```text
dotnet:tfm:net10.0-ios
dotnet:platform:ios
dotnet:rid:ios-arm64
```

We would not enable this by default, and would avoid high-cardinality projections such as package IDs or every evaluated property.

This portion may be better left to a companion plugin if inferred tags do not fit first-party plugin conventions. The structured metadata is independently useful.

### Host and environment requirements

Cross-platform repositories also need to route tasks to compatible agents, but a target framework alone cannot prove that a task is runnable in an environment.

Host requirements vary by target:

- Build may support some cross-targeting.
- Test and run may require a runtime, device, or emulator.
- Publish may require an RID, native toolchain, signing, or packaging support.
- Any operation may require workloads or SDKs not installed on a runner.

We therefore do **not** propose a project-wide “buildable on OS” flag inferred solely from TFMs.

A future model could expose explicit target/configuration requirements or executor constraints. Until Nx has such a concept, this information can remain namespaced target metadata consumed by CI tooling. Inference should be clearly advisory and retain its provenance.

### Motivation

Cross-platform .NET repositories repeatedly need to answer:

- Which frameworks and runtime identifiers does this affected project contain?
- Which affected task variant is valid on this agent?
- Which projects expose test, run, pack, or publish capabilities?
- Which outputs belong to a framework/RID-specific invocation?
- Which metadata came from an explicit repository decision versus analyzer inference?

These facts already exist in evaluated MSBuild state. Exposing them through `@nx/dotnet` avoids parallel XML parsing and lets Nx model task variants more accurately.

.NET MAUI is one example because a single workspace commonly contains Android, Apple, Windows, test, tool, package, and platform-neutral projects. The proposal is not MAUI-specific and should not special-case MAUI.

### Non-goals

This proposal does not ask Nx to adopt:

- Our `nxdn` CLI wrapper.
- Our `@redth/dotnet-nx` companion plugin.
- GitHub Actions or CI matrix generation.
- Automatic installation or detection of workloads, devices, signing assets, or native toolchains.
- A tag for every MSBuild property.
- MAUI-specific task behavior.

### Prior implementation and lessons

The current prototype demonstrates:

- MSBuild-evaluated per-framework metadata.
- Runtime identifiers and capability data.
- Explicit versus inferred provenance.
- Configuration-scoped explicit tags.
- Target-specific host requirements.
- Intersection semantics before lifting host compatibility to project scope.
- Optional, namespaced selector-tag projection.

It also helped identify approaches we no longer recommend:

- Flattening conditional framework facts into project-wide tags.
- Treating test/package/tool capabilities as exclusive project types.
- Inferring project-wide OS compatibility by unioning target-framework hosts.
- Using tags as the source of truth for target behavior.

We would be happy to split this into small contributions against the existing analyzer. Would maintainers prefer to begin with the structured metadata model, `projectType`/technology improvements, or framework/RID-specific target configuration design?

### Related work

- [Discussion #35837](https://github.com/nrwl/nx/discussions/35837) covers current `@nx/dotnet` configuration, output-path, runtime, and restore limitations.
- [Issue #33474](https://github.com/nrwl/nx/issues/33474) and [PR #33662](https://github.com/nrwl/nx/pull/33662) cover runtime argument forwarding for build and publish.
- [Discussion #36483](https://github.com/nrwl/nx/discussions/36483) demonstrates target atomization and target-group metadata for .NET tests.
- [Discussion #36468](https://github.com/nrwl/nx/discussions/36468) and [PR #36469](https://github.com/nrwl/nx/pull/36469) demonstrate richer evaluated MSBuild facts feeding native dependency nodes, cache inputs, and affected traversal.
