# Writing and Extending Tests

## Where Things Live

```
Tests/
  Features/       ← Gherkin .feature files (one per domain area)
  Steps/          ← Step definition classes (sealed, primary-constructor injected)
  Pages/          ← Page object classes extending BasePage
  Hooks/          ← Hooks.cs extending BaseHook
  Support/        ← Low-level API helpers (ProductsApi)
  Models/         ← Test-layer POCOs mirroring API models
```

## Feature Files

Every feature file must have **both** a test-type tag and an environment tag on the `Feature:` line (or on each `Scenario:`):

```gherkin
@ui @local
Feature: My Feature

@api @local
Feature: My API Feature
```

Use a `Background:` step with `Given I ensure {string} data is cleaned up if already exists` whenever a scenario creates a named entity that might be left over from a prior run:

```gherkin
Background:
  Given I ensure "Headphones" data is cleaned up if already exists
```

Keep scenarios focused and independent. Each scenario must be able to run in any order — cleanup is handled by `Hooks.DeleteCreatedProduct` and the Background step, not by ordering.

Use `DataTable` to pass structured data to steps rather than long step sentences:

```gherkin
Given I create product with following details
  | Name       | Description        | Price | ProductType |
  | Headphones | Noise cancellation |   300 | PERIPHARALS |
```

## Step Definitions

- One class per domain area, placed in `Tests/Steps/`.
- Mark the class `sealed` and `[Binding]`.
- Use C# 12 primary constructor injection — Reqnroll resolves dependencies automatically.
- Store shared state in `ScenarioContext`, not in instance fields.

```csharp
[Binding]
public sealed class ProductSteps(ScenarioContext scenarioContext, ProductPage productPage, ProductListPage productListPage)
{
    [Given("I create product with following details")]
    public async Task GivenICreateProduct(DataTable table)
    {
        Product product = table.CreateInstance<Product>();
        scenarioContext.Set(product);          // share with later steps
        await productPage.CreateProductAsync(product);
        await productPage.ClickCreateAsync();
    }
}
```

Cross-scenario reusable steps go in `ReusableSteps.cs`. Do not duplicate a step binding in more than one class.

Use `AwesomeAssertions` for assertions:

```csharp
_ = response.Status.Should().Be(201);
_ = product.Name.Should().Be("Headphones");
```

For Playwright async assertions use `Assertions.Expect(...)`:

```csharp
await Assertions.Expect(await productListPage.IsProductCreatedAsync(product.Name)).ToBeVisibleAsync();
```

## Page Objects

- Extend `BasePage` and place files in `Tests/Pages/`.
- Use primary constructor injection of `PlaywrightDriver`.
- Await the page once per method (`IPage page = await GetPageAsync();`) and resolve locators from it; use the async `BasePage` helpers (`LinkAsync`, `ButtonAsync`, `RowAsync`, `TextAsync`, `ClickLinkAsync`, `ClickButtonAsync`) where they match. Fall back to `page.Locator(css)` only when no ARIA locator works.
- Expose behaviour methods, not raw locators — except where `Assertions.Expect` needs the locator itself (`ProductRowAsync`, `StockBadgeForProductAsync`).

```csharp
public class ProductPage(PlaywrightDriver playwrightDriver) : BasePage(playwrightDriver)
{
    public async Task CreateProductAsync(Product product)
    {
        IPage page = await GetPageAsync();
        await page.GetByLabel("Name").FillAsync(product.Name);
        await page.Locator("#Price").FillAsync(product.Price.ToString(CultureInfo.InvariantCulture));
        await ClickButtonAsync("Create product");
    }

    public async Task<ILocator> RowForAsync(string name) => await RowAsync(name);
}
```

### Discovering Locators with playwright-cli

When authoring a new page object, use `playwright-cli` to explore the live WebApp and find the right locators before writing C# code. The `.playwright/cli.config.json` is already configured for this project.

```bash
# Open the target page
playwright-cli open http://localhost:5168/Products/Create
playwright-cli snapshot          # lists all interactive elements with refs

# Identify a specific element
playwright-cli find "Product type"
playwright-cli generate-locator e7   # prints the Playwright locator expression

# Try an interaction to confirm the locator works
playwright-cli fill e3 "Test Product"
playwright-cli click e9
```

The `generate-locator` output maps directly to C#. Below, `page` is `await GetPageAsync()`:

| CLI output | C# equivalent |
|---|---|
| `getByRole('textbox', { name: 'Name' })` | `page.GetByLabel("Name")` or `page.GetByRole(AriaRole.Textbox, new() { Name = "Name" })` |
| `getByRole('button', { name: 'Create product' })` | `await ClickButtonAsync("Create product")` via `BasePage` |
| `getByRole('link', { name: 'Add product' })` | `await ClickLinkAsync("Add product")` via `BasePage` |
| `getByRole('combobox', { name: 'Product type' })` | `page.GetByRole(AriaRole.Combobox, new() { Name = "Product type" })` |

Prefer the `BasePage` helpers (`LinkAsync`, `ButtonAsync`, `RowAsync`, `TextAsync`) when they match. Use `page.GetByLabel` for form fields. Fall back to `page.Locator(css)` only when no ARIA locator is available.

For a full command reference, activate the skill with `#playwright-cli`.

## API Support Helper

`Tests/Support/ProductsApi.cs` wraps `PlaywrightDriver.ApiRequestContext` and is the only place in the test project that constructs raw HTTP calls to ProductAPI. Add new endpoint wrappers here rather than calling `ApiRequestContext` directly from step definitions.

```csharp
public async Task<IAPIResponse> GetAsync(int id)
    => await (await Api).GetAsync($"products/{id}");
```

Deserialise responses with the static `ReadAsync(IAPIResponse)` helper:

```csharp
Product product = await ProductsApi.ReadAsync(response);
```

## Contract and accessibility checks

Two cross-cutting guards sit outside the product features. Extend these rather than adding a one-off
assertion to a product scenario:

- **`ApiContract.feature`** (`Tests/Support/ApiContract.cs`) reads the OpenAPI document the ProductAPI
  publishes at `GET /openapi/v1.json` and validates real responses against it with `JsonSchema.Net`.
  Use `GetDeclaredSchemaAsync` to assert something about the declared contract, and
  `GetCompiledSchemaAsync` / `GetArraySchemaAsync` with `FindMismatchAsync` to validate a payload.
- **`Accessibility.feature`** (`Tests/Support/Accessibility.cs`) scans the page a scenario has open.
  Run the `I check the page for accessibility violations` step, then assert either
  `there are no "critical" accessibility violations` or the pinned ceiling
  `the page reports at most {n} accessibility violations`. Lower a ceiling when a finding is fixed;
  raise one only with a stated reason.

Both carry a self-check — a deliberately invalid payload, and a page with a known defect — so a
validator or scanner that silently stopped working cannot look like a pass.

## Models

`Tests/Models/Product.cs` and `ProductType` mirror the SUT's API models (`ProductAPI/Models/` in https://github.com/gromozeka1/sut-hardpoint). They are intentionally separate — the test project does not reference the API project. When the API model changes, update both and bump `SUT_TAG` in `docker-compose.yml` to test against the released image.

Note the existing typo: the enum value is `PERIPHARALS` (not `PERIPHERALS`) in both the API and test models. Match it exactly.

## Adding a New Hook

Add methods to `Tests/Hooks/Hooks.cs` by extending `BaseHook`. Do not re-declare hooks that already exist in `BaseHook` (see `framework-patterns.md`). Use `Order` parameter values between 30–99 for `[BeforeScenario]` to avoid conflicting with the framework's reserved orders (`int.MinValue`, 10, 20).

```csharp
[Binding]
public class Hooks(ProductsApi productsApi) : BaseHook()
{
    [AfterScenario(Order = 50)]
    public async Task MyCleanup(ScenarioContext scenarioContext)
    {
        // cleanup logic
    }
}
```

## Reqnroll Configuration Notes

- `Tests/reqnroll.json` sets `stepDefinitionSkeletonStyle: AsyncRegexAttribute` — generated step skeletons use async methods with Regex attributes.
- `allowRowTests: false` is set — do not use Reqnroll row-test generation.
- `missingOrPendingStepsOutcome` is `Error`, so an unbound or pending step **fails the run** rather than being skipped. Bind the step before running, and use `[StepDefinition("...")]` when the same text appears under more than one keyword (see Step Definitions above).

## Debugging Failing Scenarios with playwright-cli

When a `@ui` scenario fails and the screenshot or ExtentReport is insufficient, use `playwright-cli` to reproduce and diagnose interactively against the running WebApp.

**Explore the page state that triggers the failure:**
```bash
playwright-cli open http://localhost:5168
# navigate to the relevant page manually
playwright-cli snapshot          # inspect current DOM and element refs
playwright-cli console           # check for JS errors
playwright-cli requests          # check for failed network requests to ProductAPI
```

**Reproduce a failing interaction step by step:**
```bash
playwright-cli open http://localhost:5168/Products/Create
playwright-cli snapshot
playwright-cli fill e3 "Headphones"
playwright-cli fill e5 "300"
playwright-cli select e7 "3"        # ProductType combobox — value is the int enum index
playwright-cli click e9             # "Create product" button
playwright-cli snapshot             # did the product appear in the list?
playwright-cli find "Headphones"
```

**Inspect a specific element's attributes** when a locator is ambiguous:
```bash
playwright-cli eval "el => el.getAttribute('id')" e5
playwright-cli eval "el => el.getAttribute('data-testid')" e5
playwright-cli generate-locator e5   # generate the full Playwright locator string
```

**Check what the ProductAPI returned** during a failed UI flow:
```bash
playwright-cli requests              # lists all network requests on the current page
playwright-cli request 3             # show request/response details for index 3
```

Activate the `#playwright-cli` skill for full command and session management reference.
