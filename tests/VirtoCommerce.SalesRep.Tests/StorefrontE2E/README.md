# Storefront E2E (draft)

Playwright tests that drive the **real storefront** against the **component harness hosted inside the test process**.
No second dotnet process: the same `SalesRepTestContext` (in-memory SQLite, RAM Lucene, hand-written doubles) sits
behind Kestrel on a loopback port, so a test seeds through the DbContexts, acts through the browser and asserts
against the same rows.

```
test process ─┬─ Kestrel (InProcessBackend) ── SalesRepTestContext (SQLite + Lucene + doubles)
              ├─ Playwright → Chromium ──► a clone:  https://localhost:<port> (Vite dev server, one node process)
              │                            a build:  http://127.0.0.1:<kestrel port> (Kestrel serves it)
              └─ Vite proxy (a clone only): /graphql, /connect/token, /revoke/token, /modules/…/plugins/vc-frontend/
                             ──► http://127.0.0.1:<kestrel port>
```

The storefront is either a vc-frontend clone on its own Vite dev server, whose proxy forwards the platform's paths to
Kestrel, or a built storefront (the theme zip's `default` folder), which Kestrel serves from its own origin, with
`index.html` for page routes, the way the cloud serves it.

The hub reaches the storefront the way it does in production: as the module's Module Federation plugin. The
storefront asks `store.plugins` for it, the platform's own `AppManifestService` answers from the module's
`plugins/vc-frontend/plugin.json`, and Kestrel serves the build on the platform's route for it.

## Layout

| Piece | File | Role |
|---|---|---|
| Backend host | `Infrastructure/InProcessBackend.cs` | Copies the harness registrations into a `WebApplication`, starts Kestrel on port 0, builds the schema eagerly, self-checks the `store` query; `RunExclusiveAsync` gives test-side reads a turn between requests. |
| Shell services | `Infrastructure/StorefrontServices.cs` | Real X-API `store` handler, real profile handlers and graph types, real claim producers, the platform's `AppManifestService` for `store.plugins`; JWT bearer auth; pipeline (a built storefront's files, plugin files, one request at a time). |
| Shell schema | `Infrastructure/StorefrontShellSchema.cs` | `pageContext`, `childCategories`, `cart`, `organization`, `menu`, `searchHistory`: real response types, empty data. |
| Doubles | `Infrastructure/StorefrontDoubles.cs` | Store search/currency/auth schemes, module list (the module's own `module.manifest` plus two dependencies at a version the storefront's "outdated backend" check cannot flag), settings (`ReturnModuleVersion`, task types), cart methods. |
| Hub plugin | `Infrastructure/StorefrontPlugin.cs` | The module's plugin build (`plugins/vc-frontend`) and its route; a missing build, or one older than its sources under `StorefrontApp/src`, fails the group. |
| Package skew | `Infrastructure/StorefrontOrganizationType.cs` | The storefront selects `isLockedForCurrentUser`; the pinned profile package predates it. |
| Tokens | `Infrastructure/TestTokenService.cs` | Production-shaped JWTs via `IUserClaimsPrincipalFactory` + `ITokenClaimProvider`s; `/connect/token`, `/revoke/token`. |
| Storefront location | `Infrastructure/StorefrontLocation.cs` | Finds the storefront, a clone or a build (see Running); a missing or broken one fails the groups with what to do. |
| Vite | `Infrastructure/ViteDevServer.cs` | A clone's dev server: `node node_modules/vite/bin/vite.js --port N --strictPort` with `APP_BACKEND_URL`; waits for HTTP 200; bounded kill. |
| Group fixture | `Infrastructure/StorefrontEnvironment.cs` | Backend + seed + storefront (Vite for a clone) + browser per xunit collection; `OpenAsync(userId, path)` = signed-in context, in the group's `BrowserTimeZone` when it pins one; `WaitForTaskAsync` reads task rows between requests; phase log. |
| Session | `Infrastructure/StorefrontSession.cs` | Records console errors, dialogs and GraphQL errors; a failing test gets them plus a screenshot in its message. |
| Documents | `Infrastructure/StorefrontDocuments.cs` | Loads the storefront's own `.graphql` operations from the clone (fragments inlined, `@gate` applied). |
| Contract | `StorefrontBootContractTests.cs` | Browser-less: the backend answers the storefront's boot documents for a signed-in rep (≈4 s). |
| Group 1 | `Customers/` | Rep with two organizations and one order: list + profile. |
| Group 2 | `Calendar/` | Rep with one task: complete it on the tasks page, create one from the form; both asserted on `WorkTask` rows. |
| Group 3 | `Calls/` | Rep with calls and plain tasks due tomorrow, browser in New York time. Schedule a call from a customer's page, also adding it to Google and Apple Calendar, book one from the task form, edit calls and tasks, check what the list shows of a call, and check a customer the rep stopped serving. Each is asserted on the page and on `WorkTask` rows. |

Groups are xunit collections with `DisableParallelization = true`: each runs alone, so one backend, at most one Vite
and one browser exist at a time. Tests inside a group share the environment and its seed; each test gets its own
browser context (fresh localStorage, fresh Apollo cache). Each test carries a 5-minute timeout.

Measured on the workstation that produced this draft: the contract test and the three groups run in about two
minutes; a group's environment is up in about 7 seconds; the first page load of a group costs ≈20 seconds (Vite
compiles on demand, then the storefront loads the hub plugin), later loads ≈2 seconds.

## Running

The tests run wherever they find a storefront, and anywhere else they fail with what is missing. The module CI never
runs them: every class also carries the `IntegrationTest` category, which `vc-build Test` leaves out by default.

Prerequisites:
- **A storefront**, either of:
  - a **vc-frontend clone** with `node_modules` installed, run on its own Vite dev server; it needs **Node 22** on PATH;
  - a **build**: the theme zip's `default` folder, a release or a PR build, served by Kestrel. No node process.

  The tests take `VC_STOREFRONT_BUILD` (a build) or `VC_STOREFRONT_DIR` (a clone) when set; otherwise a `vc-frontend`
  clone next to this repo, then a build two levels up. In a ticket folder those are `sources\vc-frontend` and `front`, so
  nothing needs setting there, Visual Studio included. A clone that is there wins even when it cannot run, and fails
  with `yarn install`: a ticket that moved to a clone keeps its old `front`. The storefront must load plugins, which
  vc-frontend does since 2.60.0 (vc-frontend#2569).
- **Hub plugin build:** `yarn build` in `src/VirtoCommerce.SalesRep.Web/StorefrontApp`. A missing build, or one older
  than its sources under `src`, fails the groups with that command; they never rebuild it themselves. Other build
  inputs (`vite.config.ts`, `package.json`, `public/plugin.json`) are not checked: rebuild after changing them.
- **Playwright browsers** for Microsoft.Playwright 1.63: run `pwsh bin/Debug/net10.0/playwright.ps1 install chromium`
  once, if `%LOCALAPPDATA%\ms-playwright\chromium-1243` is not already there.

The contract test reads the storefront's `.graphql` files, which only a clone has. With a build alone it reports
skipped and says why; `VC_STOREFRONT_DIR` naming the clone the build came from runs it too.

```
dotnet test --filter "Category=StorefrontE2E"                # contract + every storefront group
dotnet test --filter "Category!=IntegrationTest"             # everything else, as vc-build Test runs it
E2E_HEADED=1 dotnet test --filter "Category=StorefrontE2E"   # watch the browser
```

Diagnostics land in `bin/Debug/net10.0/StorefrontE2E-diagnostics/`: `environment-*.log` (timestamped phases per
group), `vite-<port>.log` (a clone's dev server output), `<TestName>.png` (full-page screenshot of a failed test).

## Harness changes

- `SalesRepTestContext.Create(..., providerFactory)` lets a host build (and own) the provider; `NewTaskManagementDbContext()`;
  `UserIdClaimTypes` now lists `sub` first (the platform's token claim) and `NameIdentifier` (the component tests').
- `TestStoreService.Customize` lets the host add languages, currencies and public module settings to the store double.

## What the run taught

- The storefront sends every operation, sales-rep ones included, to `/graphql`: one combined `SchemaFactory` is needed,
  not the scoped one the component tests use.
- "Schema-complete, data-empty" holds: a root field that is missing fails the whole operation with a validation error,
  so the shell fields exist with their real response types even where they return nothing.
- Package skew is the recurring cost: the storefront is built against newer X-API packages than the module pins
  (profile 3.1011 lacks `isLockedForCurrentUser`), and a package bump changes the graph types' constructor
  dependencies (X-Cart 3.1039 added `IXapiMapper` to the cart types; the pre-bump package had none). The eager
  schema build names the missing service; the contract test turns a missing field into a four-second failure.
- The storefront's primary contact for a customer is the organization owner, falling back to its oldest contact,
  and a rep is a contact of the organizations they serve: a test that wants a buyer's details must seed the owner.
- Since #13 the hub is the module's Module Federation plugin, and vc-frontend `dev` ships no built-in hub: without
  `store.plugins` every hub page is the storefront's 404.
- The hub's pages send their queries in parallel, and each in-memory SQLite database lives on one connection, which is
  not thread-safe. The symptoms were "database is locked" and index errors inside the connection's command list. So the
  host answers one request at a time, and a test's own reads take the same turn (`RunExclusiveAsync`).
- The storefront compares the module versions it is told with its own `backend-packages.json`. When one is lower, it
  shows an "outdated backend" error on every page, which covers the page's top-right buttons ("New task" among them)
  for fifteen seconds. The module under test reports its real manifest version; the stand-ins report a version no
  storefront expects to exceed, since a build keeps its list inside its bundle. So the error shows only when the module
  under test really is older than the storefront expects.
- Adding a call to the calendars opens a tab and downloads a file. Playwright starts Chromium with pop-up blocking off,
  so the scenario proves the Google link and the `.ics`, not a browser's pop-up policy. The tab opens blank and gets
  Google's page after the save, so the test waits on its URL (`ToHaveURLAsync`), not on a load, and routes Google to a
  stand-in page. `VcCheckbox` draws its box over the real input, so a test ticks it by its label, as a rep does.

## Known gaps (draft)

- Only the shell fields the captured pages use are served; the account dashboard and company info pages would also
  need `pendingOrganizationInvites`, `orders`, `currentOrganizationAddresses`, `fileUploadOptions`.
- No WebSocket transport: push messages are not in the module list, so the storefront never opens the subscription.
- The sign-in page is reachable through the password grant, but no test uses it yet.
- `isLockedForCurrentUser` always answers `false` (the pinned customer package has no membership lock).
