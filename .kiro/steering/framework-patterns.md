# Framework Patterns & Conventions

## PlaywrightDriver

`HardPoint.Testing.Driver.PlaywrightDriver` — the single source of truth for Playwright resource lifecycle. It is registered in Reqnroll's DI container (one instance per scenario) and shared by page objects, step definitions, and the `ProductsApi` helper.

- **Browser access:** `IPage page = await playwrightDriver.GetPageAsync();` — page access is asynchronous end to end. There is no synchronous `Page` property (removed in `HardPoint.Testing` `0.1.0-preview.2`); `Browser` and `BrowserContext` are `Task`-returning for the same reason. Do not instantiate browsers or pages anywhere else.
- **API access:** `await playwrightDriver.ApiRequestContext` (`Task<IAPIRequestContext>`). The base URL is set in `appsettings.json` → `apiOptions.BaseURL`.
- **Screenshots:** Call `await playwrightDriver.TakeScreenshotAsPathAsync(fileName)` for named screenshots; they are written to `screenshots/` under the artifact directory.
- **Tracing:** `StartTracingAsync(title)` / `StopTracingAsync(keepTrace)`; `BaseHook` starts a trace for every `@ui` scenario and keeps it only when the scenario fails.
- **Disposal:** `BaseHook.AfterTestRun` calls `DisposeAsync()`, which closes the page, browser context, browser, API context and driver in that order; a synchronous `Dispose()` remains as an `IDisposable` adapter. Never dispose it manually in step definitions or page objects.

## BasePage

`HardPoint.Testing.Driver.BasePage` — all page objects must extend this. It injects `PlaywrightDriver` via primary constructor and exposes `protected Task<IPage> GetPageAsync()`. Every helper is asynchronous:

```csharp
Task<IPage> GetPageAsync()
Task<ILocator> LinkAsync(string name)     // GetByRole(AriaRole.Link,   new() { Name = name })
Task<ILocator> ButtonAsync(string name)   // GetByRole(AriaRole.Button, new() { Name = name })
Task<ILocator> RowAsync(string name)      // GetByRole(AriaRole.Row,    new() { Name = name })
Task<ILocator> TextAsync(string text)     // GetByText(text, new() { Exact = true })
Task ClickLinkAsync(string name)
Task ClickButtonAsync(string name)
```

Await the page once per method and resolve locators from it; CSS is the fallback when no ARIA
locator fits:

```csharp
public class ProductPage(PlaywrightDriver playwrightDriver) : BasePage(playwrightDriver)
{
    public async Task CreateProductAsync(Product product)
    {
        IPage page = await GetPageAsync();
        await page.GetByLabel("Name").FillAsync(product.Name);
        await page.Locator("#Price").FillAsync(product.Price.ToString());   // CSS fallback — no label
        await ClickButtonAsync("Create product");                          // BasePage helper
    }
}
```

Prefer ARIA-role locators (`GetByRole`, `GetByLabel`) over CSS selectors.

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

## Reporting

**ExtentReports** (HTML) is the suite's only report, driven by `AfterStep` hooks in `BaseHook`.
Output: `TestReports.html` in the artifact directory. The CI workflow uploads `TestReports.html`
and `*.png` screenshots on every run, and on failure additionally uploads the report, HAR files,
traces and screenshots as `test-failure-artifacts`.

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
