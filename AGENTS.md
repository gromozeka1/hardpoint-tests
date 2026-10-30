# Repository Guidelines & Agent Instructions

## Solution Architecture & Module Organization

This repository contains the test suite only: a .NET 10 solution (`TestAutomation.slnx`)
pinned by `global.json`, with one project. The framework is consumed as the
**HardPoint.Testing** NuGet package; the SUT runs from published Docker Hub images
(`gromozeka1/hardpoint-webapp`, `gromozeka1/hardpoint-productapi`), whose source lives in
the separate [gromozeka1/sut-hardpoint](https://github.com/gromozeka1/sut-hardpoint)
repository.

| Project | Type | Description |
|---|---|---|
| `Tests/` | MSTest / Reqnroll | BDD automated test suite: Gherkin feature files (`Features/`), step definitions (`Steps/`), page objects (`Pages/`), hooks (`Hooks/`), API helpers (`Support/`), and test models (`Models/`). |

### Dependency Flow & Isolation

- `Tests` references the `HardPoint.Testing` **package**, pinned centrally in
  `Directory.Packages.props` (never a `Version` attribute on the reference itself).
- `Tests` has **no compile-time reference** to the SUT; it talks to the services solely over
  HTTP (Playwright API request context and the browser).
- `Tests` mirrors API models in `Tests/Models/` (POCOs) rather than sharing assemblies.
- Local orchestration is defined in `docker-compose.yml`, which pulls the SUT images from
  Docker Hub (version pinned via `SUT_TAG`, default `1.0.0`).
- The package comes from nuget.org; bump the pinned version after the framework publishes a
  new one.
- Framework work skips the release loop entirely: `-p:UseLocalFramework=true` swaps the package
  for a `ProjectReference` to the sibling framework checkout (`-p:LocalFrameworkPath` to
  override the location). Leave it off for CI and package-behaviour checks.

---

## Build, Test, and Development Commands

Always run commands from the repository root using the .NET 10 SDK:

```sh
# Build the solution
dotnet build TestAutomation.slnx

# Install browser binaries (after build, once per machine/update)
pwsh Tests/bin/Debug/net10.0/playwright.ps1 install chromium

# Start the SUT services
docker compose up -d

# Run the suite (SUT must be running)
dotnet test Tests/Tests.csproj

# Headless, as on CI
TEST_Headless=true dotnet test Tests/Tests.csproj

# Against the framework sources instead of the package (no packing)
dotnet test Tests/Tests.csproj -p:UseLocalFramework=true

# Stop the SUT (preserves the productapi-data volume)
docker compose down
```

---

## Test Automation Guidelines

### 1. Driver and page objects
- `PlaywrightDriver` comes from the package and is injected by Reqnroll's DI container (one
  instance per scenario). Use `playwrightDriver.Page` and `playwrightDriver.ApiRequestContext`;
  never construct browsers or pages.
- API-only (`@api`) scenarios never start a browser.
- All page objects extend `HardPoint.Testing.Driver.BasePage` and inject `PlaywrightDriver`
  through a primary constructor.
- Prefer ARIA role locators (`Link`, `Button`, `Row`, `Text`, `GetByRole`, `GetByLabel`) over
  CSS/XPath; reserve CSS for elements without accessible semantics (e.g. `#Price`).

### 2. Step definitions and inter-step state
- Mark step classes `sealed` and annotate with `[Binding]`.
- Inject dependencies (`ScenarioContext`, page objects, helpers) via primary constructors.
- Pass state through `ScenarioContext.Set<T>` / `Get<T>` / `TryGetValue<T>`; never store
  scenario state in step class instance fields.
- `Tests/reqnroll.json` sets `runtime.missingOrPendingStepsOutcome` to `Error`: an unbound or
  pending step fails the run instead of being skipped. Use `[StepDefinition("...")]` when the
  same text appears under more than one keyword.

### 3. Mandatory scenario tags
Every scenario needs exactly one test-type tag (`@ui` or `@api`) and at least one environment
tag (`@local`, `@dev`, `@qa`, `@stage`, `@prod`). Enforcement is on via
`"enforceScenarioTags": true` in `appsettings.json`, and runs in the package's `BaseHook` at
`Order = int.MinValue`.

### 4. Parallelism
`Tests/AssemblyInfo.cs` declares `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`.
It must live in this assembly — an assembly-level attribute in the package would configure the
package, not the suite.

### 5. Isolation and cleanup
- Scenarios must be independent and parallel-safe.
- Pre-run cleanup: a `Background:` step, `Given I ensure "{name}" data is cleaned up if already exists`.
- Post-run cleanup: `Hooks.DeleteCreatedProduct` deletes what the scenario created.
- `Tests/Support/ProductNames` suffixes every created product with a per-scenario token;
  call `Register(name)` when creating, `Actual(name)` / `ActualIn(message)` when asserting.

### 6. Assertions
- UI: Playwright web-first assertions (`Expect(...)`) for auto-retrying checks.
- API: assert status codes and deserialize JSON with `AwesomeAssertions` or MSTest asserts.

---

## SUT Integration & Contracts

- WebApp `http://localhost:5168`; ProductAPI `http://localhost:5044`
  (`GET/POST/PUT/DELETE api/products`); inside Compose the WebApp reaches ProductAPI at
  `http://productapi:8080/`.
- `Tests/appsettings.json` — local, headed, `slowMo` 500. `Tests/appsettings.CI.json` —
  headless. `ConfigReader` picks the CI file when `CI` is `true`/`1`.
- `TEST_<Setting>` environment variables override the file (`TEST_Headless=true`,
  `TEST_ApplicationUrl=...`, `TEST_APIRequestNewContextOptions__BaseURL=...`); an override only
  changes a key the file already contains.
- `outputDirectory` is empty, so artifacts land in the test output directory:
  `TestReports.html`, `screenshots/`, `traces/` and `sut-*.har`.
- `ProductType` is mirrored from the SUT with a deliberate spelling difference
  (`PERIPHARALS` here and in ProductAPI, `PERIPHERALS` in WebApp). Keep all three aligned;
  changing the SUT side needs a new image release and a `SUT_TAG` bump.

---

## Coding Style & Standards

Follow `.editorconfig`: 4 spaces, file-scoped namespaces, LF endings, nullable and implicit
usings enabled, braces on new lines; `PascalCase` types and public members, `_camelCase`
private fields.

---

## Commit & PR Guidelines

- Imperative, concise commit messages.
- Do not commit `bin/`, `obj/`, SQLite runtime files, screenshots, HAR/trace files or report
  output (`TestReports.html`).
