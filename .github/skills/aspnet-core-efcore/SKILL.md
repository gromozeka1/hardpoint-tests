---
name: aspnet-core-efcore
description: Use when the SUT's HTTP contract, API behavior, EF Core/SQLite persistence, or ASP.NET Core configuration changes and the test suite must follow.
---

# ASP.NET Core and EF Core — SUT contract guidance

- The ProductAPI and WebApp source lives in https://github.com/gromozeka1/sut-hardpoint, not in this repository. Make service changes there, release new images, and bump `SUT_TAG` in `docker-compose.yml` to test them.
- Inspect both sides of the contract before adapting to a change: the SUT controller and this repository's consumers (`Tests/Support/ProductsApi.cs`, page objects, feature files). Keep them compatible or update both in the same change.
- Keep the SQLite database path and API base URL configurable through the established configuration (`ProductApi:BaseUrl` / `ProductApi__BaseUrl`); do not hard-code developer-specific paths or addresses.
- Match existing async patterns and pass cancellation tokens to HTTP operations when the call chain supports them.
- Add or update corresponding BDD tests in `Tests/Features/` and step definitions in `Tests/Steps/` to cover new API behaviors or UI views.
- Build with `dotnet build TestAutomation.slnx`, start the SUT (`docker compose up -d`), and run the affected tests when Docker is available.
