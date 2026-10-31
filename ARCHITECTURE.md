# hardpoint-tests — architecture

This repository is the **test suite only**: a .NET 10 solution (`TestAutomation.slnx`) containing
one project, `Tests/`, written with Reqnroll (Gherkin) + MSTest + Playwright.

It contains no framework code and no application code. Both live elsewhere:

| Repo | Relationship |
|---|---|
| [playwright-dotnet-mstest-reqnroll](https://github.com/gromozeka1/playwright-dotnet-mstest-reqnroll) | Source of the **`HardPoint.Testing`** package this solution consumes from nuget.org |
| [sut-hardpoint](https://github.com/gromozeka1/sut-hardpoint) | Source of the SUT, consumed here only as published Docker Hub images |

Start with [README.md](README.md) for commands; this file explains shape, boundaries and the rules
that are actually enforced.

---

## 1. Layout

| Path | Contents |
|---|---|
| `Tests/Features/` | Gherkin: `Product.feature` (8 `@ui`), `ProductApi.feature` (22 `@api`), `ApiContract.feature` (3 `@api`), `Accessibility.feature` (3 `@ui`) — **36 total** |
| `Tests/Steps/` | Step definitions — `sealed`, `[Binding]`, primary-constructor injection |
| `Tests/Pages/` | Page objects extending `HardPoint.Testing.Driver.BasePage` |
| `Tests/Hooks/` | `Hooks.cs` extends `BaseHook`, adds only suite-specific cleanup |
| `Tests/Support/` | `ProductsApi` (the only raw HTTP wrapper), `ProductNames` (unique names), `SutAvailability` (fail-fast probe) |
| `Tests/Models/` | POCOs mirroring the API models — deliberately not shared assemblies |
| `Tests/AssemblyInfo.cs` | The assembly-level `[assembly: Parallelize]` — must live here, not in the package |
| `Tests/appsettings.json`, `appsettings.CI.json`, `reqnroll.json` | Test configuration |
| `docker-compose.yml`, `docker-compose.pinned.yml`, `.env.example` | The SUT from published images (`SUT_TAG`, default `1.0.0`); the pinned overlay fixes them by digest, which is what CI runs |
| `Directory.Packages.props` | Central Package Management, including the single `HardPointTestingVersion` pin |

## 2. Dependency direction

```
Tests ──PackageReference──▶ HardPoint.Testing   (nuget.org; -p:UseLocalFramework=true
                                                 swaps in a ProjectReference to the sibling checkout)
Tests ──HTTP──────────────▶ ProductAPI          (Playwright APIRequestContext, :5044)
Tests ──browser───────────▶ WebApp              (Playwright, :5168)
```

`Tests` has **no compile-time reference** to either SUT service, and the SUT is never built here —
CI pulls images. API models are mirrored in `Tests/Models/` on purpose.

### The two pins that keep this repo honest

| Pin | Where | Default | Bump when |
|---|---|---|---|
| `HardPointTestingVersion` | `Directory.Packages.props` | `0.1.0-preview.2` | A new framework package is published |
| `SUT_TAG` | `docker-compose.yml` / `.env` | `1.0.0` | A new SUT image changes behaviour or contract |

### Working against framework sources

```sh
dotnet test Tests/Tests.csproj -p:UseLocalFramework=true
dotnet test Tests/Tests.csproj -p:UseLocalFramework=true -p:LocalFrameworkPath=<path to HardPoint.Testing.csproj>
```

This swaps the package for a `ProjectReference` so the framework rebuilds on every run. A missing
path **fails the build** with the resolved path in the message rather than silently falling back to
the package. Leave the switch off for CI and for anything that must prove package behaviour.

## 3. What the suite drives

| Service | URL | Interface |
|---|---|---|
| WebApp | `http://localhost:5168` | Browser, Playwright |
| ProductAPI | `http://localhost:5044` | `api/products` via `ProductsApi` + `IAPIRequestContext` |

Inside Compose the WebApp reaches ProductAPI at `http://productapi:8080/`; the suite only ever
talks to the host ports above.

Contract summary the suite assumes: `search` (case-insensitive name substring) and `type` query
parameters on the list endpoint; `409 Conflict` + `ProblemDetails` on duplicate names; `400` on
validation failures and unknown `type`; `Stock` on every product; enum values serialize as numbers.

`Tests/Models/ProductType` mirrors the API's deliberate spelling `PERIPHARALS` (the WebApp uses
`PERIPHERALS`). If the SUT side changes, both repos move together and `SUT_TAG` is bumped.

## 4. The framework boundary

Comes from the package — do not reimplement here:

`PlaywrightDriver` (one instance per scenario, injected by Reqnroll's DI), `BasePage`, `BaseHook`,
`ConfigReader`/`TestSettings`, `ExtentReportManager`, `ArtifactDirectory`, `ScenarioTags`.

Stays here — the package cannot do it for you:

- `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]` in `Tests/AssemblyInfo.cs`. An
  assembly-level attribute inside the package would configure the package, and this suite would
  silently run serially.
- The tagging policy: `"enforceScenarioTags": true`. The package defaults it to `false` so it
  imposes no policy on anyone else.

## 5. Lifecycle and hooks

`Hooks.cs` extends `BaseHook` and adds only cleanup. Do not override these, and pick `Order`
values 30–99 so your hooks don't collide with the reserved ones:

| Order | Hook | Condition |
|---|---|---|
| `int.MinValue` | `VerifyMandatoryScenarioTags` | when `enforceScenarioTags` is `true` |
| 10 | `StartReportScenario` | always |
| 20 | `OpenApplication` (navigates to `applicationUrl`) | `@ui` only |
| — | `ReportStep` (`[AfterStep]`) | always |
| — | `AttachScreenshotOnFailure` (`[AfterScenario]`) | `@ui` only |
| — | `AfterTestRun` (static) | once, disposes the driver and flushes the report |

`@api` scenarios never launch a browser; the driver creates the API request context lazily.
Never dispose the driver yourself.

## 6. Parallelism and isolation

Method-level parallelism is on, which makes isolation a hard requirement rather than a nicety:

- **Unique names**: `Support/ProductNames` suffixes every product a scenario creates with a
  per-scenario token (`Headphones 24e4f07d`). Creation steps call `Register`, steps referring to an
  existing product call `Actual` / `ActualIn`. Feature files keep readable names; each scenario only
  ever sees its own rows. Seed-data names pass through untouched.
- **Pre-cleanup**: a `Background:` step
  `Given I ensure "{name}" data is cleaned up if already exists`.
- **Post-cleanup**: `Hooks.DeleteCreatedProduct` via `ScenarioContext`.
- Never depend on execution order, shared files, or another scenario's data.

## 7. Conventions that are enforced, not suggested

- Every scenario carries **exactly one** test-type tag (`@ui` or `@api`) and **at least one**
  environment tag (`@local`, `@dev`, `@qa`, `@stage`, `@prod`) — on the `Feature:` or `Scenario:`
  line.
- `Tests/reqnroll.json` sets `missingOrPendingStepsOutcome: "Error"`, so an unbound or pending step
  **fails the run**. Use `[StepDefinition("...")]` when the same text appears under two keywords.
- Inter-step state goes in `ScenarioContext` (`Set<T>` / `Get<T>` / `TryGetValue<T>`), never in step
  class fields.
- Locators prefer ARIA: `Link`, `Button`, `Row`, `Text`, `GetByRole`, `GetByLabel`. CSS only where
  no accessible semantics exist (e.g. `#Price`).
- UI assertions use Playwright web-first `Assertions.Expect(...)`; API assertions use
  `AwesomeAssertions` or MSTest asserts.
- `Tests/Support/ProductsApi.cs` is the only place that builds raw HTTP calls — add endpoint
  wrappers there, not in step definitions.

## 8. Configuration

`Tests/appsettings.json` (local, headed, `slowMo` 500) and `Tests/appsettings.CI.json` (headless).
The framework picks the CI file when `CI` is `true`, as GitHub Actions sets it. Both are copied to
the test output directory.

| Key | Value here |
|---|---|
| `applicationUrl` | `http://localhost:5168` |
| `apiOptions.BaseURL` | `http://localhost:5044/api/` |
| `enforceScenarioTags` | `true` |
| `outputDirectory` | empty — artifacts land in the test output directory |

Any setting is overridable with `TEST_<Setting>`: `TEST_Headless=true`,
`TEST_ApplicationUrl=http://host:5168`, nested via `__`
(`TEST_APIRequestNewContextOptions__BaseURL=...`). Collection settings take comma-separated values.
**An override only changes a key the file already contains.**

## 9. Running

```sh
docker compose up -d                                              # SUT first
dotnet build TestAutomation.slnx
pwsh Tests/bin/Debug/net10.0/playwright.ps1 install chromium       # once per machine/update
dotnet test Tests/Tests.csproj
TEST_Headless=true dotnet test Tests/Tests.csproj                 # as on CI
docker compose down                                               # keeps productapi-data
```

`playwright.ps1` downloads the browser and needs PowerShell (`pwsh`). Re-run it after any
Playwright package update.

## 10. Filtered runs — read before building CI logic on a filter

Reqnroll emits each scenario as one MSTest test **named after the scenario title**. `dotnet test
--filter` therefore matches friendly-name substrings, **not** the `@ui` / `@api` tags.
`fullyqualifiedname~api` happens to match a subset of scenarios — it is matching the word in titles
and step text, not the tag, so the set it selects is arbitrary and changes as titles change.

Always check the shape first:

```sh
dotnet test Tests/Tests.csproj --list-tests
dotnet test Tests/Tests.csproj --filter "fullyqualifiedname~<substring>" --no-build
```

Use filters for local debugging only. When a stable subset is genuinely needed, give it its own
feature file, project or tag convention and run that.

## 11. Artifacts

Written to the test output directory (`outputDirectory` is empty here):

| Artifact | Contents |
|---|---|
| `TestReports.html` | ExtentReports HTML, one block per feature — the suite's only report |
| `traces/` | One Playwright trace per **failed** `@ui` scenario; passing traces are discarded |
| `screenshots/` | Screenshots attached to report steps |
| `sut-*.har` | One HAR per browser context |

Open a trace with `playwright show-trace <file>.zip`.

## 12. CI (`.github/workflows/dotnet.yml`)

Restores, builds `Tests.csproj`, installs Chromium, starts the SUT with `docker compose up --pull
always --wait`, health-checks both services, runs the suite headless, and uploads
`TestReports.html` plus `*.png` on every run — plus, on failure, the report, traces, screenshots
and HAR files as `test-failure-artifacts`. The SUT is never built in CI.

A **weekly scheduled run** restores the newest *published* framework prerelease by overriding
`HardPointTestingVersion` from the environment, so a framework release that breaks this suite
surfaces before anyone bumps the pin. That run is the canary for the whole three-repo arrangement.

## 13. Conventions

- **Central Package Management.** Versions live only in `Directory.Packages.props`. Never put a
  `Version` attribute on a `PackageReference` in `Tests.csproj`.
- **Style** (`.editorconfig`): 4 spaces, file-scoped namespaces, LF endings, nullable and implicit
  usings enabled, braces on new lines. `PascalCase` public members, `_camelCase` private fields.
- **Commits**: imperative and concise. Do not commit `bin/`, `obj/`, `TestReports.html`, traces,
  screenshots or HAR files.

## 14. Documentation consistency

Three stale claims in `.kiro/steering/*` were corrected on 2026-10-31. They are worth recording
because each had a silent-failure shape — follow the old text and your code compiles, runs, and
misleads:

| Was documented | Correct behaviour |
|---|---|
| `playwrightDriver.Page` (synchronous `IPage`), synchronous `Link`/`Button`/`Row`/`Text` helpers | Page access is async end to end: `await GetPageAsync()`, and `LinkAsync`/`ButtonAsync`/`RowAsync`/`TextAsync` on `BasePage`. Removed in `HardPoint.Testing` `0.1.0-preview.2`. |
| Seed data applied by `EnsureCreated()`; "Get an existing product" depends on seed `id 1` | The SUT calls `Database.Migrate()`; that scenario creates its own product ([Tests/Features/ProductApi.feature:4-10](Tests/Features/ProductApi.feature#L4-L10)) |
| Missing step bindings produce `Pending`, not an error | `Tests/reqnroll.json` sets `missingOrPendingStepsOutcome: "Error"` — an unbound step fails the run |

`.github/copilot-instructions.md` and `.github/instructions/tests.instructions.md` were corrected in
the same pass; they restate the same API surface.

When the framework's public surface changes, these files rot with it: `.kiro/steering/*` and
`.github/**` duplicate what the package documents, so a rename in `HardPoint.Testing` is a
documentation change in this repository too.

## 15. Open work

[SUGGESTIONS.md](SUGGESTIONS.md) is the live backlog. Reliability items (seed-ID dependency,
SUT fail-fast probe, parallel-safe names, CI caching, failure artifacts, Allure removal) are done.
Remaining: **coverage growth** — contract/schema tests against the SUT's `ProductAPI.json`,
data-driven `Examples` tables for filter/validation permutations, and axe-core accessibility
checks. Scenario-level flakiness detection is skipped by decision.

## 16. Where else to read

| Question | File |
|---|---|
| Commands, configuration, artifacts | [README.md](README.md) |
| Agent-facing rules | [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md) |
| How to write features, steps, page objects | [.kiro/steering/test-authoring.md](.kiro/steering/test-authoring.md) |
| SUT endpoints, Compose, CI detail | [.kiro/steering/sut-and-deployment.md](.kiro/steering/sut-and-deployment.md) |
| Framework internals and hook order | [.kiro/steering/framework-patterns.md](.kiro/steering/framework-patterns.md) |
| Improvement backlog | [SUGGESTIONS.md](SUGGESTIONS.md) |
