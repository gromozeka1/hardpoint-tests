# hardpoint-tests

Browser and API test suite for the HardPoint product-management application, built with
Reqnroll, MSTest and Playwright on top of the [HardPoint.Testing](https://github.com/gromozeka1/playwright-dotnet-mstest-reqnroll)
package.

The system under test (SUT) is an ASP.NET Core MVC WebApp backed by an ASP.NET Core
ProductAPI and SQLite, running from pre-built images on Docker Hub. Its source lives in
[gromozeka1/sut-hardpoint](https://github.com/gromozeka1/sut-hardpoint).

## Repository structure

- `Tests/` — Reqnroll feature files (`Features/`), step definitions (`Steps/`), hooks
  (`Hooks/`), page objects, API and availability helpers, and test configuration. UI scenarios
  are tagged `@ui`; API scenarios exercise ProductAPI through Playwright's API request context.
- `Tests/AssemblyInfo.cs` — assembly-level `Parallelize` attribute; it configures this
  assembly, so it has to live here rather than in the framework package.
- `docker-compose.yml` — runs the SUT from its published Docker Hub images.
- `Directory.Packages.props` — central package versions, including the pinned
  `HardPoint.Testing` version.
- `nuget.config` — nuget.org plus the local folder feed used before the package is published.

Targets .NET 10.

## The framework package

`Tests/Tests.csproj` consumes `HardPoint.Testing` as a `PackageReference`; the version is
pinned in `Directory.Packages.props` (central package management — never put a `Version`
attribute on the reference itself).

Until the package is on nuget.org, it is served from the `localfeed` folder next to this
repository:

```sh
# in the framework repository
dotnet pack HardPoint.Testing/HardPoint.Testing.csproj -c Release -o ../localfeed

# here
dotnet restore TestAutomation.slnx
```

Bump the pinned version whenever the framework publishes a new one.

### Working against framework sources

To iterate on the framework without packing a package, build against its project
instead of the package:

```sh
dotnet test Tests/Tests.csproj -p:UseLocalFramework=true
```

The switch swaps the `HardPoint.Testing` `PackageReference` for a `ProjectReference`, so the
framework is rebuilt on every run. It defaults to the sibling checkout at
`../playwright-dotnet-mstest-reqnroll/HardPoint.Testing/HardPoint.Testing.csproj`; point it
elsewhere with `-p:LocalFrameworkPath=<path to HardPoint.Testing.csproj>`. A missing path fails
the build with the resolved path in the message rather than silently falling back to the
package. Leave the switch off for CI and for anything that should prove package behaviour.

## Build and run the tests

Install the .NET 10 SDK, then:

```sh
docker compose up -d
dotnet build TestAutomation.slnx
pwsh Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test Tests/Tests.csproj
```

The `playwright.ps1` step downloads the browser and requires
[PowerShell](https://learn.microsoft.com/powershell/scripting/install/installing-powershell)
(`pwsh`). Run it after the first build and whenever the Playwright package is updated.

Headless runs, as on CI: `TEST_Headless=true dotnet test Tests/Tests.csproj`.

## Configuration

`Tests/appsettings.json` (local, headed, `slowMo` 500) and `Tests/appsettings.CI.json`
(headless) are copied to the test output directory. The framework picks the CI file
when the `CI` environment variable is `true`, as GitHub Actions sets it.

| Key | Value here |
|---|---|
| `applicationUrl` | `http://localhost:5168` — the WebApp |
| `apiOptions.BaseURL` | `http://localhost:5044/api/` — ProductAPI |
| `enforceScenarioTags` | `true` — every scenario must carry an environment tag (`@local`, `@dev`, `@qa`, `@stage`, `@prod`) and a test-type tag (`@ui`, `@api`) |
| `outputDirectory` | empty — artifacts land in the test output directory |

Any setting can be overridden with `TEST_<Setting>` environment variables, for example
`TEST_Headless=true` or `TEST_ApplicationUrl=http://host:5168`. Nested settings are addressed
by leaf name or by a `__`-separated path of property names; collection settings take
comma-separated values. An override only changes a key the config file already contains.

## Run the SUT with Docker Compose

Prerequisites: Docker Desktop or Docker Engine with Compose v2 and the daemon running, with
ports `5044` and `5168` free.

```sh
docker compose up -d     # pulls the published images
docker compose down      # stops them; the productapi-data volume is preserved
```

The images default to release `1.0.0`; override with `SUT_TAG` (for example
`SUT_TAG=latest docker compose up -d`). The WebApp is on `http://localhost:5168`, ProductAPI
on `http://localhost:5044`; inside the Compose network the WebApp reaches ProductAPI at
`http://productapi:8080/`.

## Artifacts

Runs write, under the test output directory (or `outputDirectory` when set):

- `TestReports.html` — Extent report, one block per feature.
- `screenshots/` — screenshots attached to report steps.
- `traces/` — a Playwright trace archive per failed `@ui` scenario; open with
  `playwright show-trace <file>.zip`.
- `sut-*.har` — one HAR per browser context.

## CI

`.github/workflows/dotnet.yml` restores, builds, installs Chromium, starts the SUT, runs the
suite headless, and uploads `TestReports.html` plus failure diagnostics (report, traces,
screenshots, HAR files).

CI restores from nuget.org, so it needs the pinned `HardPoint.Testing` version to be
published there. Until it is, either publish a preview first or add a feed that hosts the
package and authenticate the runner against it.
