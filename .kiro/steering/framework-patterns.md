# Framework Patterns & Conventions

## PlaywrightDriver

`HardPoint.Testing.Driver.PlaywrightDriver` — the single source of truth for Playwright resource lifecycle. It is registered in Reqnroll's DI container (one instance per scenario) and shared by page objects, step definitions, and the `ProductsApi` helper.

- **Browser access:** Use `playwrightDriver.Page` (synchronous `IPage`). Do not instantiate browsers or pages anywhere else.
- **API access:** Use `playwrightDriver.ApiRequestContext` (`Task<IAPIRequestContext>`). The base URL is set in `appsettings.json` → `apiOptions.BaseURL`.
- **Screenshots:** Call `playwrightDriver.TakeScreenshotAsPathAsync(fileName)` for named screenshots; they are written to `screenshots/` under the artifact directory.
- **Disposal:** `PlaywrightDriver.Dispose` is called by `BaseHook.AfterTestRun`. Never dispose it manually in step definitions or page objects.

## BasePage

`HardPoint.Testing.Driver.BasePage` — all page objects must extend this. It injects `PlaywrightDriver` via primary constructor and exposes `protected IPage Page`.

Built-in locator helpers (prefer these over raw `Page` calls for common ARIA roles):

```csharp
Link(string name)   // Page.GetByRole(AriaRole.Link, new() { Name = name })
Button(string name) // Page.GetByRole(AriaRole.Button, new() { Name = name })
Row(string name)    // Page.GetByRole(AriaRole.Row, new() { Name = name })
Text(string text)   // Page.GetByText(text, new() { Exact = true })

ClickLinkAsync(string name)   // async helper
ClickButtonAsync(string name) // async helper
```

Prefer ARIA-role locators (`GetByRole`, `GetByLabel`) over CSS selectors. Use CSS selectors (`Page.Locator("#id")`) only when no semantic ARIA locator is available (e.g. `#Price`).

## BaseHook

`HardPoint.Testing.Driver.BaseHook` — the abstract Reqnroll `[Binding]` base class. Concrete hook classes in `Tests/Hooks/` must extend it.

Hook execution order for each scenario:

| Order | Method | Condition |
|---|---|---|
| `int.MinValue` | `VerifyMandatoryScenarioTags` | when `enforceScenarioTags` is `true` |
| 10 | `StartReportScenario` | always |
| 20 | `OpenApplication` (navigates to `ApplicationUrl`) | `@ui` scenarios only |
| — | `ReportStep` `[AfterStep]` | always |
| — | `AttachScreenshotOnFailure` `[AfterScenario]` | `@ui` scenarios only |
| — | `AfterTestRun` (static) | once after all tests |

**Do not override these methods** in `Tests/Hooks/Hooks.cs`. Add new `[BeforeScenario]` or `[AfterScenario]` hooks there without conflicting order values.

## Mandatory Scenario Tags

Every scenario **must** carry:
1. Exactly one **test-type tag**: `@ui` or `@api`
2. At least one **environment tag**: `@local`, `@dev`, `@qa`, `@stage`, or `@prod`

`VerifyMandatoryScenarioTags` runs first and throws `ArgumentException` if either group is absent. Tag constants live in `HardPoint.Testing.Constants.ScenarioTags`.

## ExtentReportManager

`HardPoint.Testing.Helpers.ExtentReportManager` — singleton that writes `TestReports.html` to the artifact directory (the test output directory unless `outputDirectory` is set). Initialised in `BaseHook.StartReportScenario` and flushed in `BaseHook.AfterTestRun`. Do not instantiate it directly; let Reqnroll inject it.

For `@ui` scenario failures, it attaches a full-page screenshot to the failing step. For `@api` failures, it records only the error message.

## ConfigReader

`HardPoint.Testing.Config.ConfigReader` — reads `appsettings.json` from the test output directory (`AppContext.BaseDirectory`) and deserialises it into `TestSettings`. `appsettings.CI.json` is used instead when `CI` is `true`/`1`, and `TEST_CONFIG_FILE` replaces the file entirely. Environment variables prefixed `TEST_` override the settings the file already contains (case-insensitive property name match). Example: `TEST_Headless=true` sets `BrowserTypeLaunchOptions.Headless`.

A missing or malformed file fails with a message naming the resolved path and the remedy.

`TestSettings` properties:

| Property | JSON key | Description |
|---|---|---|
| `BrowserType` | `browserType` | `"chromium"`, `"firefox"`, `"webkit"` |
| `ApplicationUrl` | `applicationUrl` | WebApp base URL |
| `BrowserTypeLaunchOptions` | `browserOptions` | Playwright launch options (headless, slowMo, timeout, downloadsPath, tracesDir) |
| `APIRequestNewContextOptions` | `apiOptions` | Playwright API context options (BaseURL, IgnoreHTTPSErrors) |
| `EnforceScenarioTags` | `enforceScenarioTags` | Reject untagged scenarios before they run. `true` in this repository's configs |
| `OutputDirectory` | `outputDirectory` | Where the report, traces, screenshots and HAR files go. Empty means the test output directory |

Never hardcode URLs or browser settings in test code; always read from `ConfigReader.Settings`.

## Dual Reporting

Two reporting mechanisms run in parallel:
- **ExtentReports** (HTML) — driven by `AfterStep` hooks in `BaseHook`. Output: `TestReports.html` in the artifact directory.
- **Allure** — driven automatically by `Allure.Reqnroll`. Config in `Tests/allureConfig.json`. Output: `allure-results/` at repo root level.

Both are configured; do not remove either. The CI workflow uploads `TestReports.html` and `*.png` screenshots on every run, and on failure additionally uploads the report, HAR files, traces and screenshots as `test-failure-artifacts`.

## Trace capture in the test run

`BaseHook` starts a Playwright trace for every `@ui` scenario (`StartTracingAsync` in `PlaywrightDriver`, DOM snapshots and screenshots enabled) and stops it in an `AfterScenario` hook. Passing scenarios discard their trace; failing scenarios keep it as `traces/{scenario-title}-{id}.zip` under the artifact directory, where CI picks it up. Open one with `npx playwright show-trace <file>.zip` or `dotnet playwright show-trace`.

`TestSettings.BrowserTypeLaunchOptions.TracesDir` (`TestResults/Traces` in `appsettings*.json`) only tells Playwright where to stage trace chunks; the exported `.zip` paths above are what the framework and CI use.

## Tracing with playwright-cli

For ad-hoc tracing outside of the automated test run — for example, to capture a detailed network and DOM trace while reproducing a bug manually — use `playwright-cli`:

```bash
playwright-cli open http://localhost:5168
playwright-cli tracing-start

# Reproduce the flow
playwright-cli goto http://localhost:5168/Products/Create
playwright-cli fill e3 "Headphones"
playwright-cli click e9

playwright-cli tracing-stop
```

Trace files are written to `.playwright-cli/traces/` and include DOM snapshots, screenshots, network requests/responses, and console output at every step — more detail than the HAR files the automated suite records. Use `playwright-cli requests` and `playwright-cli console` during an active session for a lighter-weight alternative before committing to a full trace.

See the `#playwright-cli` skill (tracing reference) for details on trace file contents and how to open them.

## Browser Automation Tool Reference

This project has three Playwright-based browser automation tools configured. Use whichever fits the task:

| Tool | Config file | Best for |
|---|---|---|
| `dotnet playwright` (`playwright.ps1`) | — | Installing browsers; opening the Trace Viewer GUI against a `.zip` trace |
| `playwright-cli` | `.playwright/cli.config.json` | Token-efficient agent work; locator discovery; interactive debugging; named sessions |
| Playwright MCP (`@playwright/mcp`) | `.playwright/mcp.config.json` | Persistent browser for deep iterative exploration; C# codegen from live interactions |

### playwright-cli config (`.playwright/cli.config.json`)
- `browserName: chromium`, `headless: false`, `slowMo: 500` — matches `appsettings.json` so CLI sessions feel the same as a test run
- `outputMode: file` — snapshots write to timestamped files, keeping the terminal clean
- `outputDir: Tests/TestResults` — screenshots and traces land beside test artifacts
- `codegen: none` — disabled; this project uses Reqnroll/Gherkin, not raw Playwright TypeScript tests

### Playwright MCP config (`.playwright/mcp.config.json`)
- `isolated: true` — browser profile kept in memory; no stale state between agent sessions
- `codegen: csharp` — actions generate C# Playwright snippets, directly usable as starting points for page objects and step definitions
- `capabilities: ["network", "storage", "testing", "devtools"]` — enables request inspection/mocking, cookie/storage CRUD, web-first assertions, and JS evaluation
- `outputDir: Tests/TestResults/mcp` — MCP screenshots and traces are kept separate from CLI and test-run artifacts
- `headless: false`, `slowMo: 0` — headed so you can observe the MCP agent; no slow-motion delay (MCP sessions are already deliberate)

### Kiro MCP registration (`.kiro/settings/mcp.json`)
Registers the Playwright MCP server with Kiro. The schema uses `"servers"` as the top-level key (not `"mcpServers"`). The server entry uses `"type": "stdio"` with `command` and `args`. The server is started on demand via `npx @playwright/mcp@latest --config .playwright/mcp.config.json`.
