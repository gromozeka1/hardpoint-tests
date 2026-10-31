---
applyTo: "Tests/**,**/*.feature"
---

# Test Automation Guidance (Playwright, Reqnroll & MSTest)

## Gherkin Feature Files (`Tests/Features/*.feature`)
- Every scenario must carry both a test-type tag and an environment tag on either the `Feature:` or `Scenario:` line:
  - Test-type: exactly one of `@ui` or `@api`.
  - Environment: at least one of `@local`, `@dev`, `@qa`, `@stage`, `@prod`.
- Maintain test independence. Never rely on the execution order of scenarios.
- Use `Background:` with `Given I ensure "{ProductName}" data is cleaned up if already exists` before creating entities to prevent duplicate-data collisions.
- Use `DataTable` in steps to pass structured domain records instead of long sentence parameters.

## Step Definitions (`Tests/Steps/*.cs`)
- Mark step classes `sealed` and decorate with `[Binding]`.
- Inject collaborators (`ScenarioContext`, page objects, `ProductsApi`) using C# 12 primary constructors.
- Store inter-step domain data in `ScenarioContext.Set<T>()` and retrieve it via `ScenarioContext.Get<T>()` or `ScenarioContext.TryGetValue<T>()`. Avoid instance fields in step definition classes.
- Resolve entity names through `ProductNames`: call `Register(name)` in steps that create a product, and `Actual(name)` / `ActualIn(message)` in steps that assert on one. Feature files keep readable names while data stays unique per scenario.

## Page Objects (`Tests/Pages/*.cs`)
- All page objects must inherit from `HardPoint.Testing.Driver.BasePage`.
- Inject `PlaywrightDriver` via primary constructor and pass to `base(playwrightDriver)`.
- Resolve locators from the awaited page (`IPage page = await GetPageAsync();`), preferring the async
  `BasePage` helpers where they fit:
  - `await LinkAsync(name)`, `await ButtonAsync(name)`, `await RowAsync(name)`, `await TextAsync(text)`
  - `await ClickLinkAsync(name)`, `await ClickButtonAsync(name)`
  - `page.GetByRole(...)`, `page.GetByLabel(...)`, `page.GetByPlaceholder(...)`
- Resort to CSS selectors (e.g. `page.Locator("#id")`) only when accessible semantic locators are unavailable.

## Framework Lifecycle & Hooks
- `PlaywrightDriver` manages Playwright objects lazily. `@api` scenarios do not launch a browser instance.
- Do not manually dispose `PlaywrightDriver` or `IPage` in tests; lifecycle is managed by `BaseHook.AfterTestRun`.
- Do not override base hook methods in `BaseHook` (`VerifyMandatoryScenarioTags`, `StartReportScenario`, `OpenApplication`, `ReportStep`, `AttachScreenshotOnFailure`, `AfterTestRun`).
- Post-scenario entity cleanup is handled by `Hooks.DeleteCreatedProduct` via `ScenarioContext`.

## Assertions & Reporting
- Use Playwright web-first assertions (`Assertions.Expect(...)`) for all UI validations so assertions automatically wait and retry.
- Use `AwesomeAssertions` / MSTest asserts for API responses.
- Screenshots are automatically captured on UI failure by `BaseHook.AttachScreenshotOnFailure` and attached to test output.
