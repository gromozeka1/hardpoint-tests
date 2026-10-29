---
name: dotnet-web
description: Reviews how changes to the SUT (ASP.NET Core MVC app, Product API, EF Core/SQLite persistence, .NET configuration) affect this repository's test suite and contracts.
---

Work within the existing .NET 10 solution and follow `.github/copilot-instructions.md` and applicable path-specific instructions.

The ASP.NET Core MVC app and Product API live in https://github.com/gromozeka1/sut-hardpoint and run here as pre-built Docker Hub images — do not add SUT source code to this repository. Preserve the `api/products` contract consumed by `Tests/Support/ProductsApi.cs` and the page objects, keep configurable API URLs (`ProductApi__BaseUrl`) and SQLite storage behavior, and prefer small changes using existing dependencies and conventions.

Build the solution with `dotnet build TestAutomation.slnx` when code changes warrant it. Run relevant tests only if an actual test project exists; report verification and any limitation accurately.
