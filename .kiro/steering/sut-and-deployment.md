# System Under Test & Deployment

## Services

| Service | Local URL | Docker internal | Description |
|---|---|---|---|
| WebApp | `http://localhost:5168` | `webapp:8080` | ASP.NET Core MVC — product list, create, edit, details, delete |
| ProductAPI | `http://localhost:5044` | `productapi:8080` | ASP.NET Core Web API — `GET/POST/PUT/DELETE api/products` |

WebApp proxies all data through ProductAPI via `ProductApiClient` (typed `HttpClient`). The MVC app has no direct database access.

## ProductAPI Endpoints

```
GET    api/products          → 200 []Product
GET    api/products/{id}     → 200 Product | 404
POST   api/products          → 201 Product | 409 (already exists)
PUT    api/products/{id}     → 204 | 404 | 400
DELETE api/products/{id}     → 204 | 404
GET    /                     → 200 "Hello World!" (health probe)
```

Persistence: SQLite at `<ContentRoot>/Database/ProductList.db`. Seed data created by `ProductContext.OnModelCreating`:

| Id | Name | ProductType | Price |
|---|---|---|---|
| 1 | Ryzen 7 7800X3D | CPU | 399 |
| 2 | Dell UltraSharp 27 | MONITOR | 499 |
| 3 | Logitech MX Master 3S | PERIPHARALS | 99 |

Seed data is applied via `Database.EnsureCreated()` at startup — it is only inserted once when the database file is first created. The `@api` scenario `Get an existing product` depends on seed product id `1` existing.

## ProductType Enum

Defined in three places — keep them in sync when adding new values. The two SUT files live in https://github.com/gromozeka1/sut-hardpoint (changing them requires a new image release and a `SUT_TAG` bump here):

| Location | Values |
|---|---|
| SUT `ProductAPI/Models/ProductType.cs` | `CPU`, `MONITOR`, `PERIPHARALS`, `EXTERNAL` |
| `Tests/Models/Product.cs` | `CPU`, `MONITOR`, `PERIPHARALS`, `EXTERNAL` |
| SUT `WebApp/Models/ProductViewModel.cs` | `CPU`, `MONITOR`, `PERIPHERALS`, `EXTERNAL` |

Note: `PERIPHARALS` (API + Tests) vs `PERIPHERALS` (WebApp) is an existing inconsistency. Do not silently correct it without updating all three files and any database seed data.

## Running Locally

```powershell
# 1. Build
dotnet build TestAutomation.slnx

# 2. Install Chromium (after build, once per machine)
pwsh Tests/bin/Debug/net10.0/playwright.ps1 install chromium

# 3. Start services (pulls the published SUT images; pin via SUT_TAG, default 1.0.0)
docker compose up -d

# 4. Run tests (services must be up first)
dotnet test Tests/Tests.csproj
```

Default `appsettings.json` targets `localhost:5168` (WebApp) and `localhost:5044/api/` (ProductAPI) with `headless: false` and `slowMo: 500` for local observation.

## Exploring the SUT with playwright-cli

Use the `playwright-cli` skill to explore and interact with the running WebApp and ProductAPI manually, without writing any test code. The `.playwright/cli.config.json` file is pre-configured for this project (Chromium, `localhost:5168`, `slowMo: 500`), so no flags are needed for most commands.

**Install once:**
```bash
npm install -g @playwright/cli@latest
playwright-cli install --skills
```

**Open the WebApp:**
```bash
playwright-cli open http://localhost:5168
playwright-cli snapshot          # see the page structure with element refs
playwright-cli find "Products"   # locate specific text in the snapshot
```

**Inspect the ProductAPI directly:**
```bash
# Use a named session to keep the WebApp session open in parallel
playwright-cli -s=api open http://localhost:5044/api/products
playwright-cli -s=api snapshot
```

**Named sessions** allow working against both services simultaneously:
```bash
playwright-cli -s=webapp open http://localhost:5168
playwright-cli -s=api    open http://localhost:5044/api/products
playwright-cli list      # shows both active sessions
playwright-cli close-all
```

**Take a screenshot for a bug report or PR:**
```bash
playwright-cli open http://localhost:5168/Products
playwright-cli screenshot --filename=products-list.png
```

The `playwright-cli` skill (`#playwright-cli`) provides full reference documentation for all available commands.

## Playwright MCP

The Playwright MCP server (`@playwright/mcp`) is configured at the workspace level and gives any MCP-capable client (Kiro, Claude Desktop, VS Code Copilot, Cursor, etc.) a persistent browser it can drive via structured tool calls.

**Configuration files:**
- `.kiro/settings/mcp.json` — registers the server with Kiro using the `servers` key (schema uses `servers`, not `mcpServers`); points to the project config file
- `.playwright/mcp.config.json` — MCP server options: Chromium, isolated profile, `csharp` codegen, outputs to `Tests/TestResults/mcp/`

**When to use MCP over playwright-cli:**

| Situation | Prefer |
|---|---|
| AI agent balancing browser work with a large codebase (test authoring, code edits) | `playwright-cli` + skills — token-efficient |
| Long-running autonomous exploration — self-healing, iterative page reasoning | MCP — maintains persistent browser state across tool calls |
| Quick locator discovery, debugging a failing step | `playwright-cli` |
| Exploratory test planning requiring deep, iterative page introspection | MCP |

**Key differences from playwright-cli:**
- MCP loads full tool schemas and accessibility trees into the model context on each call — richer but more expensive in tokens
- MCP maintains a single persistent browser context shared across all tool calls in a session; `playwright-cli` uses named daemon sessions
- MCP codegen is configured to output **C#** (`"codegen": "csharp"` in `.playwright/mcp.config.json`), making generated snippets directly usable in Playwright .NET step definitions and page objects
- MCP `isolated: true` keeps the browser profile in memory — no stale cookies or local storage from previous sessions bleed into a new run

**Capabilities enabled** (in `.playwright/mcp.config.json`):
- `network` — inspect and mock HTTP requests
- `storage` — read/write cookies, localStorage, sessionStorage
- `testing` — web-first assertions (`browser_expect_*` tools)
- `devtools` — evaluate JavaScript, access console

**Using MCP for C# code generation:**

Because `codegen` is set to `csharp`, actions the MCP agent performs on the WebApp produce C# Playwright snippets. These map directly to the locator helpers in `BasePage` — paste the output and replace raw `IPage` calls with the appropriate `BasePage` method:

```csharp
// MCP-generated (paste as starting point)
await page.GetByRole(AriaRole.Textbox, new() { Name = "Name" }).FillAsync("Headphones");

// Refactored to page object pattern
private ILocator TxtName => Page.GetByLabel("Name");
await TxtName.FillAsync(product.Name);
```

## Environment Variable Overrides

Any `TestSettings` property can be overridden at runtime with `TEST_<PropertyName>`:

| Variable | Example | Effect |
|---|---|---|
| `TEST_Headless` | `true` | Runs browser without UI (required in CI) |
| `TEST_ApplicationUrl` | `http://myhost:5168` | Override WebApp URL |
| `TEST_SlowMo` | `0` | Disable step delay |

Variable names are case-insensitive against the property name. Arrays are comma-separated.

## Docker Compose

`docker-compose.yml` defines two services running the pre-built images published to Docker Hub (source: https://github.com/gromozeka1/sut-hardpoint; version pinned by `SUT_TAG`, default `1.0.0`):

- **productapi** — image `gromozeka1/hardpoint-productapi`; mounts volume `productapi-data:/app/Database` to persist SQLite across restarts; runs with `ASPNETCORE_ENVIRONMENT=Development` (enables OpenAPI at `/openapi/v1.json`).
- **webapp** — image `gromozeka1/hardpoint-webapp`; sets `ProductApi__BaseUrl=http://productapi:8080/` to reach the API over the Docker internal network; depends on `productapi`.

```powershell
docker compose up -d           # start (pulls missing images)
docker compose down             # stop (volume is preserved)
docker compose down -v          # stop and delete database volume (resets seed data)
```

## CI Workflow (`.github/workflows/dotnet.yml`)

Trigger: push or PR to `main`. Runner: `ubuntu-latest`, 20-minute timeout.

Key steps:
1. `dotnet build Tests/Tests.csproj -c Release -o artifacts/tests`
2. `pwsh artifacts/tests/playwright.ps1 install --with-deps chromium`
3. `docker compose up -d --pull always --wait --wait-timeout 180`
4. Health-check both services with `curl --retry 30`
5. `dotnet test artifacts/tests/Tests.dll` with `TEST_Headless=true`
6. Upload `TestReports.html` + `*.png` screenshots as `test-results` artifact (always, even on failure)
7. `docker compose down`

The CI build only builds `Tests.csproj` (which pulls the `HardPoint.Testing` package); it never builds the SUT — CI pulls the published Docker Hub images via `docker compose up --pull always`.

## WebApp Configuration

The WebApp image defaults to `ProductApi:BaseUrl = http://localhost:5044/` in its own `appsettings.json` (SUT repository). Docker Compose overrides it with `ProductApi__BaseUrl: http://productapi:8080/` (double-underscore notation for nested keys in ASP.NET Core environment variables) so the UI reaches ProductAPI over the Compose network.
