# Test Solution Improvement Suggestions

Ideas for improving the test automation solution, gathered from a review of the
framework, test suite, CI pipeline and local run artifacts. Each item lists the
evidence that prompted it. Ordered by priority within each section.

This file reviews the **suite**, so it belongs here: it lived in the framework repository
before the split. Items that concern the framework itself point at
[playwright-dotnet-mstest-reqnroll](https://github.com/gromozeka1/playwright-dotnet-mstest-reqnroll).

## 🔴 High value — reliability & correctness

### 1. Handle flakiness explicitly (evidence of retries exists) — ⏭️ skipped by decision
The run history recorded `isRetry: true` entries for "Create product and verify the
details", "Delete a product" and "Get an existing product" — these have passed only on
a retry at least once. That history was Allure's and was removed with the tool, but the fact
stands: the pipeline itself has no retry mechanism (`test.runsettings` is empty boilerplate;
no `[Retry]` attribute; no rerun step in CI), so the same flake in CI means a red build.

**Suggestion:** add a scenario-level retry hook for CI runs and/or a
`dotnet test --filter` failed-rerun step, and surface retried-but-passed tests
as *flaky* in the report instead of silently green.

### 2. Remove the seed-data ID dependency — ✅ done

The "Get an existing product" scenario now creates its own product via the
existing `a product exists` step and reads it back, instead of asserting on
seed row `id 1`.
`Tests/Features/ProductApi.feature:5` asserts `GET api/products/1` returns
"Ryzen 7 7800X3D" at price 399. This couples the test to seed row ordering and
to a pristine database — but the `productapi-data` volume persists across local
runs (`docker compose down` keeps it), and any drift (manual deletion, prior
failed cleanup) breaks the assertion.

**Suggestion:** have the scenario create-then-read its own product, or assert
via `?search=` on the name instead of a hardcoded id.

### 3. Fail fast when the SUT is not running — ✅ done

Implemented as a Reqnroll `[BeforeTestRun]` probe in
`Tests/Support/SutAvailability.cs` (MSTest allows only one
`[AssemblyInitialize]` per assembly and the source generator owns it — UTA013).
The generated assembly hook runs the Reqnroll hooks, so an unreachable SUT
aborts the entire run immediately with an actionable message naming the failing
endpoint and the `docker compose up -d` remedy.
If the stack is down, every test fails individually with connection-refused
noise (20 confusing failures instead of one clear message).

**Suggestion:** an assembly-level `[AssemblyInitialize]` / BeforeTestRun probe
of `GET /` and `GET api/products` that aborts with
"SUT not reachable — run `docker compose up -d`".

### 4. Name-based cleanup is a parallel-execution hazard — ✅ done
Method-level parallelism is enabled, and isolation relied on
`Given I ensure "{name}" data is cleaned up if already exists` with fixed names
(`Headphones`, `DuplicateUI`, ...). That only worked while no two scenarios (or
concurrent runs) shared a name — a shared name would delete the other's data
mid-run.

Implemented as `Tests/Support/ProductNames`: a scenario-scoped registry that
suffixes every product a scenario creates (`Headphones 24e4f07d`). Creation
steps call `Register`, steps referring to an existing product call `Actual` (and
`ActualIn` for the duplicate-name error text), so feature files keep their
readable names and seed-data names pass through untouched. Cleanup, duplicate-name
checks and search assertions all use the resolved name, so each scenario only
ever sees its own rows.

## 🟡 CI efficiency & observability

### 5. Cache NuGet and Playwright browsers — ✅ done

The workflow reinstalls Chromium with `--with-deps` on every run — the bulk of
the ~2 minute build job.

`actions/setup-dotnet` now runs with `cache: true` (keyed on the project files,
`Directory.Packages.props` and `global.json`), and `actions/cache@v6` keeps
`~/.cache/ms-playwright` keyed on `Directory.Packages.props`, so a Playwright
version bump invalidates the browser cache. `--with-deps` still runs on every
build: it is a no-op for the browser download on a cache hit but still installs
the Linux libraries the cached browser needs.

### 6. Upload artifacts that actually debug failures — ✅ done

CI uploaded only `TestReports.html` and `*.png`, while the framework produced
**HAR files** (`sut-{guid}.har`) and failure screenshots next to the test DLL.
Traces were configured (`tracesDir` in `appsettings*.json`) but never started,
so no trace was ever written.

`PlaywrightDriver` now starts a Playwright trace for every UI scenario and
keeps it only when the scenario fails (`traces/{title}-{id}.zip`, passed traces
are deleted), and CI gained an `if: failure()` step uploading
`test-failure-artifacts` with the report, HAR files, traces and screenshots.
Download the trace and open it with
`npx playwright show-trace <file>.zip`.

### 7. Generate and publish the Allure report in CI (or drop Allure) — ✅ done
The repo wired `Allure.Reqnroll`, `allurerc.json` and even tracked
`Tests/history.jsonl` (trend data), but CI never ran `allure generate`; only
the Extent HTML was uploaded.

**Resolved:** Allure was removed — the plugin, both configs, the tracked history and the
ignore rules for its output. `TestReports.html` from the framework is the single report CI
publishes, and no second reporting stack needs maintaining.

### 8. Small CI polish
- ✅ `concurrency: cancel-in-progress` — added for pull requests, so a superseded push does not
  keep a runner busy. `main` and scheduled runs are never cancelled: their history is the signal.
- Write pass/fail counts to `$GITHUB_STEP_SUMMARY` for at-a-glance results. Still open; only the
  canary step writes to the summary today.

## 🟢 Test coverage growth

### 9. Contract / schema tests
Validate API responses against the SUT's published OpenAPI document
(the SUT repo ships `ProductAPI.json`). This catches breaking contract changes
between image releases before UI tests stumble on them.

Related: consider **generating `Tests/Models` from the OpenAPI spec** instead of
hand-mirroring — the docs currently warn humans to keep three `ProductType`
enum copies in sync.

### 10. Data-driven API scenarios
`Examples` tables for search/type-filter permutations and validation failures
(400 on unknown `type`, 400 on id-mismatch PUT) would expand coverage without
new step code.

### 11. Accessibility checks
Page objects already use ARIA role locators; adding axe-core
(`@axe-core/playwright` injected via `AddScriptTag`) gives a11y regression
coverage almost for free.

## ⚪ Hygiene

- **`Tests/history.jsonl` was tracked** while every other test output is ignored. ✅
  Resolved: the file was untracked and deleted along with Allure, which was the only thing
  that wrote it.
- **`Tests/test.runsettings` was entirely commented-out boilerplate** and CI never applied it
  (`dotnet test <dll>` ignores `RunSettingsFilePath`). ✅ Resolved: the file, the property and the
  copy item are gone; timeouts and browser settings live in `appsettings*.json`.
- **Two hardcoded `Task.Delay(500)` waits in `HardPoint.Testing/Driver/PlaywrightDriver.cs`**
  (framework repository; formerly `Framework/Driver/PlaywrightDriver.cs:94-96`) — replace with
  condition-based waiting; fixed sleeps are the same problem the suite avoids with web-first
  assertions.
- **Supply-chain hardening (optional):** pin compose images by digest
  (`image: ...@sha256:...`) since Docker Hub tags can be moved; `SUT_TAG`
  already provides the version knob.
- **Developer experience:** `.env.example` now documents `SUT_TAG` for Compose, and filtered runs are
  documented in `README.md` and `AGENTS.md` against what Reqnroll actually generates — friendly
  scenario-title test names, not tag-qualified attribute names, so `dotnet test --filter` matches
  substrings of those names, not `@ui` / `@api`. The old `TestCategory=api` wording was wrong; the
  real behaviour is now described there.

## Suggested order of attack

1. Item **1** — flakiness detection, the only correctness gap still open.
2. Coverage additions (**9–11**), then hygiene as time allows.
