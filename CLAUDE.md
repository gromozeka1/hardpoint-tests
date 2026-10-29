# CLAUDE.md - Assistant Guidelines

## Solution Overview

This repository is the HardPoint test suite: a .NET 10 solution (`TestAutomation.slnx`) with one
project, `Tests/`, built on the **HardPoint.Testing** package with Reqnroll, MSTest and Playwright.
The framework's source lives in
[playwright-dotnet-mstest-reqnroll](https://github.com/gromozeka1/playwright-dotnet-mstest-reqnroll);
the system under test (ASP.NET Core WebApp on `localhost:5168`, ProductAPI on `localhost:5044`)
runs from Docker Hub images and its source lives in
[sut-hardpoint](https://github.com/gromozeka1/sut-hardpoint).

### Layout

- `Tests/`: feature files (`Features/`), step definitions (`Steps/`), page objects (`Pages/`),
  hooks (`Hooks/`), API and availability helpers (`Support/`), models (`Models/`), and the
  config files (`appsettings.json`, `appsettings.CI.json`, `reqnroll.json`, `test.runsettings`).
- `Tests/AssemblyInfo.cs`: the assembly-level `[assembly: Parallelize]` attribute.
- `docker-compose.yml`: the SUT from published images (`SUT_TAG`, default `1.0.0`).
- `Directory.Packages.props`: central versions, including the pinned `HardPoint.Testing` version.

---

## Essential Commands

```sh
dotnet build TestAutomation.slnx
pwsh Tests/bin/Debug/net10.0/playwright.ps1 install chromium
docker compose up -d
dotnet test Tests/Tests.csproj
TEST_Headless=true dotnet test Tests/Tests.csproj
docker compose down
```

Pack the framework into the sibling `localfeed` folder (registered per machine with
`dotnet nuget add source ../localfeed --name localfeed` — never in `nuget.config`, where a
missing folder breaks cold restores) and bump the pinned version when a new package version is
needed, or run against its sources with
`dotnet test Tests/Tests.csproj -p:UseLocalFramework=true` — no packing involved.

---

## Key Development Rules

### Central Package Management (CPM)

- Versions live in `Directory.Packages.props`. **Never** put a `Version` attribute on a
  `<PackageReference>` in `Tests.csproj`.

### Framework boundary

- Use what the package provides: `HardPoint.Testing.Driver.PlaywrightDriver`, `BasePage`,
  `BaseHook`, `ConfigReader`/`TestSettings`, `ArtifactDirectory`, `ScenarioTags`. Do not
  reimplement driver, config or reporting plumbing in this repository.
- Page objects inherit `HardPoint.Testing.Driver.BasePage` and use ARIA role locators.
- Assembly-level attributes stay here, not in the package.

### Test conventions

- Exactly one test-type tag (`@ui` or `@api`) and at least one environment tag (`@local`,
  `@dev`, `@qa`, `@stage`, `@prod`) per scenario; `enforceScenarioTags` is on.
- Step classes are `sealed`, `[Binding]`, with primary-constructor injection; inter-step state
  goes in `ScenarioContext`. `missingOrPendingStepsOutcome` is `Error`, so unbound steps fail.
- Scenarios must stay parallel-safe: clean up in `Background:` steps, delete in the
  `[AfterScenario]` hook, and use `ProductNames` for unique product names.

### Style

Follow `.editorconfig`: 4 spaces, file-scoped namespaces, LF endings, nullable and implicit
usings enabled.

See `AGENTS.md` for the full guidance.
