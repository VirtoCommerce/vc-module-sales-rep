# VC-Shell app E2E (draft)

Playwright tests that drive the **sales-rep VC-Shell app** (`src/VirtoCommerce.SalesRep.Web/App`) against the
**component harness hosted inside the test process**. The same `SalesRepTestContext` (in-memory SQLite, RAM Lucene,
hand-written doubles) sits behind Kestrel on a loopback port and serves two things from one origin: the module's
own REST controllers, hosted as they are, and the built app as static files under `/apps/vc-sales-rep/`. A test
seeds through the DbContexts, acts through the browser and asserts against the same rows. No dev server, no proxy,
no second dotnet process.

```
test process ─┬─ Kestrel (InProcessVCShellBackend) ── SalesRepTestContext (SQLite + Lucene + doubles)
              │     ├─ /api/sales-rep/**            SalesRepController, SalesRepDocumentsController (the module's .Web assembly)
              │     ├─ /api/platform/security/**    login / logout / currentuser replicas over Identity + the real claims factory
              │     ├─ /api/stores/search, /api/organizations/search   replicas over the harness services
              │     └─ /apps/vc-sales-rep/**         App/dist (static)
              └─ Playwright → Chromium ──► http://127.0.0.1:<port>/apps/vc-sales-rep/#/sales-reps
```

## Layout

| Piece | File | Role |
|---|---|---|
| Backend host | `Infrastructure/InProcessVCShellBackend.cs` | Copies the harness registrations into a `WebApplication`, starts Kestrel on port 0, self-checks the app page and the auth wall. |
| Services | `Infrastructure/VCShellServices.cs` | Real claim producers; MVC over the module's controllers with the platform's JSON contract; the Identity cookie as the auth scheme; the pipeline. |
| Replicas | `Infrastructure/VCShellPlatformEndpoints.cs` | The platform endpoints the framework and the blades call whose controllers are not packaged: login, logout, currentuser, pushnotifications, apps, ui/customization, externalsignin providers, stores and organizations search, and the test-only `/e2e/sign-in`. |
| JSON | `Infrastructure/PlatformJson.cs` | `Startup.AddNewtonsoftJson` as the platform configures it, plus the two converters it adds at runtime. |
| Policies | `Infrastructure/PermissionPolicyProvider.cs` | `[Authorize("permission")]` resolved to the platform's own permission requirement and handler. |
| Dynamic properties | `Infrastructure/EmptyDynamicPropertyMetaDataResolver.cs` | The process-wide metadata the JSON contract consults; the harness has none. |
| Group fixture | `Infrastructure/VCShellAppEnvironment.cs` | Backend + seed + browser per xunit collection; `OpenAsync(userId, route)` = signed-in context on the app route. |
| Session | `Infrastructure/VCShellSession.cs` | Records console errors, dialogs, API responses of 400 and above and failed requests; a failing test gets them plus a screenshot in its message. |
| Availability | `Infrastructure/VCShellAppAvailability.cs` | Skips the tests where the built app is absent (CI). |
| Group 1 | `SalesReps/` | An administrator creates a rep through the details blade (account, contact and membership rows asserted) and blocks one (lockout asserted). |

Groups are xunit collections with `DisableParallelization = true`; tests inside a group share the environment and its
seed, each test gets its own browser context. Each fact carries a 5-minute timeout.

Measured on the workstation that produced this draft: the group of two tests runs in 9 seconds; the environment is
up in about 3 seconds; each test takes about one second (the built app loads from static files, nothing compiles).

## Running

The tests are local-only by construction: each fact carries `SkipUnless = VCShellAppAvailability.IsAvailable`, so
without a built app (a CI runner, or `VC_VCSHELL_APP_E2E=0`) they report as skipped and the fixture starts nothing.
On the module CI both output folders are gitignored and `vc-build Test` runs before `vc-build Compress`, the step
that builds the app into `Content/vc-sales-rep`, so the runner never has a built app at test time.

Prerequisites: the built app, produced once with `yarn build:app` in `src/VirtoCommerce.SalesRep.Web/App` (the
output `App/dist` is found automatically, as is `Content/vc-sales-rep`, which vc-build fills; `VC_VCSHELL_APP_DIR`
overrides both), and Playwright's Chromium for Microsoft.Playwright 1.63.

```
dotnet test --filter "Category=VCShellAppE2E"
E2E_HEADED=1 dotnet test --filter "Category=VCShellAppE2E"   # watch the browser
```

Diagnostics land in `bin/Debug/net10.0/VCShellAppE2E-diagnostics/`: `environment-*.log` (timestamped phases),
`<TestName>.png` (full-page screenshot of a failed test).

## What the run taught

- **The framework authenticates with the platform's cookie and nothing else.** No request carries a bearer header;
  `vc_auth_data` in localStorage only feeds the refresh grant. Signing in is `POST /api/platform/security/login`
  setting the Identity application cookie, so the host keeps the harness's Identity cookie scheme (configured to
  answer 401/403 instead of redirecting) and a browser context signs in through `/e2e/sign-in` before its first
  navigation. The principal behind the cookie comes from the real `CustomUserClaimsPrincipalFactory`.
- **An administrator is a flag, not a role.** The claims factory synthesizes the `__administrator` system role claim
  from `ApplicationUser.IsAdministrator`; a user merely assigned to a role of that name gets 403 from every
  permission check.
- **The platform's JSON contract has process-wide state.** Serializing a member consults
  `DynamicPropertyMetadata`, which `Startup.Configure` initializes once; without it every organization search
  answers 500. The host initializes it with a resolver that owns no services, so later groups in the same process
  are unaffected.
- **Member searches with a type filter go to the index**, so the seed indexes the organizations it creates.
- **The routes are hash-based under the base path**: `/apps/vc-sales-rep/#/sales-reps`, not `/apps/vc-sales-rep/sales-reps`.
- **Locators**: the framework marks menu items (blade name), toolbar buttons (`add`, `save`, `block`, ...), select
  dropdowns and options with `data-test-id`; inputs are labelled through `for`; a select's trigger is a combobox
  named by its label and its choices are options. The password field's show/hide button is also labelled
  "Show password", so that label needs an exact match.
- **A missing SignalR hub is tolerated**: the framework retries the negotiation with backoff and logs each attempt;
  the recorder drops that noise. It must never answer 401, which the framework reads as an expired session.

## Known gaps (draft)

- No sign-in-page test yet; the login replica exists for it.
- `GET /api/platform/apps` answers an empty list; the app hub is not exercised.
- The replica endpoints cover the two blades under test; the documents blades need nothing more from the platform,
  the organization-list blades call `/api/organizations/search` only.
