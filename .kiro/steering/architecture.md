# Solution Architecture

## Projects

| Project | Role |
|---|---|
| `Tests/` | Reqnroll/MSTest BDD tests: feature files, step definitions, page objects, hooks, support helpers, test models |

The test framework is consumed as the **HardPoint.Testing** NuGet package (source:
https://github.com/gromozeka1/playwright-dotnet-mstest-reqnroll). It provides the Playwright
driver, configuration reader, `BasePage`, `BaseHook`, ExtentReports integration, artifact paths
and scenario tag constants. It is not part of this repository.

The SUT (`WebApp`, `ProductAPI`) is **not** in this repository either: it runs from Docker Hub
images (`gromozeka1/hardpoint-webapp`, `gromozeka1/hardpoint-productapi`; source:
https://github.com/gromozeka1/sut-hardpoint).

Solution file: `TestAutomation.slnx` (.NET 10, `global.json` pins the SDK).

## Dependency Flow

```
Tests  ──nuget package──▶  HardPoint.Testing   (pinned in Directory.Packages.props;
                                                -p:UseLocalFramework=true to build
                                                it from the sibling checkout instead)
Tests  ──HTTP (runtime)──▶  ProductAPI   (via Playwright APIRequestContext, localhost:5044)
Tests  ──browser (runtime)──▶  WebApp    (via Playwright, localhost:5168)
```

`Tests` has no compile-time reference to either SUT service. The test project mirrors the API's
`Product` / `ProductType` models in `Tests/Models/` rather than sharing them.

## Key Design Patterns

- **One driver per scenario.** Reqnroll's DI container creates `PlaywrightDriver`; page objects,
  step definitions and `ProductsApi` share it. `BaseHook.AfterTestRun` disposes it.
- **Lazy resources.** A browser is only launched for `@ui` scenarios; `@api` scenarios use the
  API request context alone.
- **Hook inheritance.** `Hooks.cs` extends `BaseHook` from the package and adds only the
  test-specific cleanup hook.
- **Parallel by assembly attribute.** `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`
  is declared in `Tests/AssemblyInfo.cs` — assembly-level attributes in the package would
  configure the package, not this suite.
- **Artifacts under one directory.** The package's `ArtifactDirectory` writes
  `TestReports.html`, `traces/`, `screenshots/` and `sut-*.har` to the test output directory, or
  to `outputDirectory` when the configuration sets one.
