---
name: playwright-mstest
description: Use when authoring, modifying, or debugging browser automation, Playwright driver configuration, Reqnroll BDD scenarios, or MSTest tests in this suite.
---

# Playwright .NET, Reqnroll, and MSTest

- The driver, hooks and config reader come from the `HardPoint.Testing` package: `HardPoint.Testing.Driver.PlaywrightDriver`, `.Driver.BasePage`, `.Driver.BaseHook`, `.Config.ConfigReader`. Extend them from `Tests/`; driver lifecycle fixes belong in the framework repository (https://github.com/gromozeka1/playwright-dotnet-mstest-reqnroll).
- Respect Central Package Management: define package versions in `Directory.Packages.props`, never in individual `.csproj` files.
- In browser tests, prefer accessible locators (`GetByRole`, `GetByLabel`, `BasePage` helpers), auto-waiting actions, and web-first assertions (`Assertions.Expect(...)`) over fixed delays or brittle CSS/XPath selectors.
- Tag every scenario with mandatory test-type (`@ui` or `@api`) and environment (`@local`, `@dev`, etc.) tags to satisfy `BaseHook.VerifyMandatoryScenarioTags`.
- Ensure scenario independence: use Background cleanup steps and `[AfterScenario]` deletion hooks to keep the SUT database clean.
- Keep screenshots, HAR recordings, and test report artifacts (`TestReports.html`) out of tracked source.
- Build with `dotnet build TestAutomation.slnx`; execute tests against the running local/Compose SUT using `dotnet test Tests/Tests.csproj`.
