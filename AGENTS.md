# AGENTS.md

## Repository purpose

This repository is a monorepo for related but independent projects, tools, plugins, packages, experiments, and reusable build assets that help .NET MAUI projects build more cleanly in scenarios that are not implicitly supported by the .NET SDK, the MAUI SDK, or `dotnet` tooling.

Examples of efforts that belong here include:

- Improving `@nx/dotnet` integration for MAUI-oriented repositories.
- Providing reusable GitHub Actions and workflow building blocks.
- Supporting project-reference patterns such as linking apps to other projects.
- Packaging NuGet helpers, MSBuild targets, or CLI tools that smooth over gaps in common MAUI build workflows.
- Capturing experiments that may later become standalone packages, templates, actions, or documentation.

The projects in this repo should be treated as related by mission, not necessarily by implementation. Prefer loose coupling between subprojects unless shared code or shared build infrastructure is clearly justified.

## Working in this repo

- Keep changes scoped to the specific project, package, plugin, action, or experiment being updated.
- Preserve independence between subprojects; avoid adding cross-project dependencies without a clear build or maintenance benefit.
- Follow the conventions already present in the area you are editing, including naming, layout, package metadata, test style, and workflow structure.
- Update local documentation when behavior, usage, inputs, outputs, or build expectations change.
- Prefer reusable build primitives over one-off fixes when the same MAUI build gap is likely to appear in multiple projects.
- Do not assume every subproject uses the same language, framework, package manager, or release process.

## Validation guidance

- Run the most specific existing validation for the area changed, such as package tests, build targets, action checks, or sample project builds.
- If a change affects shared build infrastructure, validate at least one representative consumer when practical.
- Do not introduce new tooling requirements unless the affected subproject already uses them or the change explicitly adds and documents that requirement.

## Design principles

- Make unsupported or awkward MAUI build scenarios explicit, repeatable, and easier to diagnose.
- Favor small, composable helpers that can be adopted independently.
- Keep CI, local builds, and package behavior aligned so fixes work outside a single developer machine.
- Document assumptions around SDK versions, workload requirements, platform constraints, and repository layout.
- Optimize for clear failure modes over hidden fallbacks when a build scenario cannot be supported automatically.
