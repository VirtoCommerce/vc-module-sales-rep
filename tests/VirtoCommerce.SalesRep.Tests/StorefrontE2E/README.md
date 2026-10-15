# Storefront E2E (draft)

Playwright tests that drive the **real storefront** against the **component harness hosted inside the test process**.
No second dotnet process: the same `SalesRepTestContext` (in-memory SQLite, RAM Lucene, hand-written doubles) sits
behind Kestrel on a loopback port, so a test seeds through the DbContexts, acts through the browser and asserts
against the same rows.

```
test process ─┬─ Kestrel (InProcessBackend) ── SalesRepTestContext (SQLite + Lucene + doubles)
              ├─ Playwright → Chromium ──► https://localhost:<port> (Vite dev server, one node process)
              └─ Vite proxy: /graphql, /connect/token, /revoke/token ──► http://127.0.0.1:<kestrel port>
```

## Layout

| Piece | File | Role |
|---|---|---|
| Backend host | `Infrastructure/InProcessBackend.cs` | Copies the harness registrations into a `WebApplication`, starts Kestrel on port 0, builds the schema eagerly, self-checks the `store` query. |
| Shell services | `Infrastructure/StorefrontServices.cs` | Real X-API `store` handler, real profile handlers and graph types, real claim producers; JWT bearer auth; pipeline. |
| Shell schema | `Infrastructure/StorefrontShellSchema.cs` | `pageContext`, `childCategories`, `cart`, `organization`, `menu`, `searchHistory`: real response types, empty data. |
| Doubles | `Infrastructure/StorefrontDoubles.cs` | Store search/currency/auth schemes, module list, settings (`ReturnModuleVersion`), app manifest, cart methods. |
| Package skew | `Infrastructure/StorefrontOrganizationType.cs` | The storefront selects `isLockedForCurrentUser`; the pinned profile package predates it. |
| Tokens | `Infrastructure/TestTokenService.cs` | Production-shaped JWTs via `IUserClaimsPrincipalFactory` + `ITokenClaimProvider`s; `/connect/token`, `/revoke/token`. |
| Storefront | `Infrastructure/ViteDevServer.cs` | `node node_modules/vite/bin/vite.js --port N --strictPort` with `APP_BACKEND_URL`; waits for HTTP 200; bounded kill. |
| Group fixture | `Infrastructure/StorefrontEnvironment.cs` | Backend + seed + Vite + browser per xunit collection; `OpenAsync(userId, path)` = signed-in context; phase log. |
| Session | `Infrastructure/StorefrontSession.cs` | Records console errors, dialogs and GraphQL errors; a failing test gets them plus a screenshot in its message. |
| Documents | `Infrastructure/StorefrontDocuments.cs` | Loads the storefront's own `.graphql` operations (fragments inlined, `@gate` applied). |
| Contract | `StorefrontBootContractTests.cs` | Browser-less: the backend answers the storefront's boot documents for a signed-in rep (≈4 s). |
| Group 1 | `Customers/` | Rep with two organizations and one order: list + profile. |
| Group 2 | `Calendar/` | Rep with one task: complete it from the UI, create one from the form; both asserted on `WorkTask` rows. |

Groups are xunit collections with `DisableParallelization = true`: each runs alone, so one backend, one Vite and
one browser exist at a time. Tests inside a group share the environment and its seed; each test gets its own
browser context (fresh localStorage, fresh Apollo cache). Each test carries a 5-minute timeout.

Measured on the workstation that produced this draft: the whole group (contract + 4 browser tests) runs in about
55 seconds; a group's environment is up in 2–3 seconds; the first page load of a group costs ≈15 seconds (Vite
compiles on demand), later loads ≈2 seconds.

## Running

The storefront tests are local-only by construction: each fact carries `SkipUnless = StorefrontAvailability.IsAvailable`,
so without a storefront checkout (a CI runner, or `VC_STOREFRONT_E2E=0`) the five tests report as skipped and the group
fixtures start nothing. The shared CI workflow needs no filter. Verified: the full suite with the opt-out reports
583 passed, 5 skipped, 0 failed.

Prerequisites: the storefront checkout with `node_modules` installed (`<workspace>/front`, or `VC_STOREFRONT_DIR`),
Node 22 on PATH, Playwright browsers for Microsoft.Playwright 1.63 (`pwsh bin/Debug/net10.0/playwright.ps1 install chromium`
once, if `%LOCALAPPDATA%\ms-playwright\chromium-1243` is not already there).

```
dotnet test --filter "Category=StorefrontE2E"                # contract + the four storefront tests
dotnet test --filter "Category!=StorefrontE2E"               # everything else, as before
E2E_HEADED=1 dotnet test --filter "Category=StorefrontE2E"   # watch the browser
```

Diagnostics land in `bin/Debug/net10.0/StorefrontE2E-diagnostics/`: `environment-*.log` (timestamped phases per
group), `vite-<port>.log` (dev server output), `<TestName>.png` (full-page screenshot of a failed test).

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

## Known gaps (draft)

- Only the shell fields the captured pages use are served; the account dashboard and company info pages would also
  need `pendingOrganizationInvites`, `orders`, `currentOrganizationAddresses`, `fileUploadOptions`.
- No WebSocket transport: push messages are not in the module list, so the storefront never opens the subscription.
- The sign-in page is reachable through the password grant, but no test uses it yet.
- `isLockedForCurrentUser` always answers `false` (the pinned customer package has no membership lock).
