# Repository instructions

## Stack and layout

- A .NET 10 solution (`TestAutomation.slnx`) pinned with `global.json`, nullable reference types
  and implicit usings enabled, containing the single test project `Tests/`.
- Central Package Management is enabled in `Directory.Packages.props`. Never specify `Version`
  attributes on `PackageReference` elements in `.csproj` files.
- The test framework is consumed as the `HardPoint.Testing` package (source:
  https://github.com/gromozeka1/playwright-dotnet-mstest-reqnroll). Until it is published to
  nuget.org it restores from the sibling `localfeed` folder configured in `nuget.config`.
- The SUT is not part of this repository: `docker-compose.yml` pulls
  `gromozeka1/hardpoint-productapi` and `gromozeka1/hardpoint-webapp` (version pinned via
  `SUT_TAG`, default `1.0.0`). WebApp is at `http://localhost:5168`, ProductAPI at
  `http://localhost:5044`; inside Compose, WebApp reaches `http://productapi:8080/`. Source:
  https://github.com/gromozeka1/sut-hardpoint.
- `Tests/` holds feature files (`Features/`), step definitions (`Steps/`), page objects
  (`Pages/`), hooks (`Hooks/`), API helpers (`Support/`) and the mirrored models (`Models/`).

## Architecture and layer boundaries

- Reusable framework code belongs in the package, not here. This repository holds only
  application-specific tests and configuration.
- `Tests` has no compile-time reference to the SUT; it talks to the services over HTTP and
  mirrors API models in `Tests/Models/Product.cs`.
- Keep API routes and response semantics in `Tests/Support/ProductsApi.cs` aligned with the SUT
  repository when a contract changes.
- Follow `.editorconfig`: C# uses 4 spaces, file-scoped namespaces, braces on new lines, LF
  line endings.

## Test automation conventions

- Tag every scenario with exactly one test-type tag (`@ui` or `@api`) and at least one
  environment tag (`@local`, `@dev`, `@qa`, `@stage`, `@prod`). Enforcement is enabled through
  `"enforceScenarioTags": true`.
- Page objects must inherit `HardPoint.Testing.Driver.BasePage` and prefer semantic ARIA
  locators (`Link()`, `Button()`, `Row()`, `Text()`, `GetByRole()`, `GetByLabel()`) over CSS.
- Step classes must be `sealed`, annotated with `[Binding]`, and use primary constructors; store
  inter-step data in `ScenarioContext`.
- Keep scenarios parallel-safe: `Background:` cleanup steps before creating entities, the
  `[AfterScenario]` deletion hook afterwards, and `Tests/Support/ProductNames` for unique names.
- Use Playwright web-first assertions (`Assertions.Expect(...)`) for browser tests.

## Build and verification

- Build with `dotnet build TestAutomation.slnx`.
- Install browsers with `pwsh Tests/bin/Debug/net10.0/playwright.ps1 install chromium`.
- Start the SUT with `docker compose up -d`, then run `dotnet test Tests/Tests.csproj`.
- Headless: `TEST_Headless=true dotnet test Tests/Tests.csproj`. Settings can be overridden with
  `TEST_`-prefixed environment variables.
- Do not commit `bin/`, `obj/`, `TestReports.html`, `allure-report/`, `allure-results/`, traces,
  screenshots or HAR files.
