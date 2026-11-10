# Storefront E2E (draft)

Playwright tests that drive the **real storefront** against the **component harness hosted inside the test process**.
No second dotnet process: the same `SalesRepTestContext` (the module and the platform around it, on in-memory SQLite
and a RAM Lucene index) sits behind Kestrel on a loopback port, so a test seeds through the modules' services, acts
through the browser and asserts against the same rows.

```
test process ─┬─ Kestrel (InProcessBackend) ── SalesRepTestContext (platform + modules, SQLite + Lucene)
              ├─ Playwright → Chromium ──► a clone:  https://localhost:<port> (Vite dev server, one node process per run)
              │                            a build:  http://127.0.0.1:<kestrel port> (Kestrel serves it)
              └─ Vite proxy (a clone only): /graphql, /connect/token, /revoke/token, /modules/…/plugins/vc-frontend/
                             ──► http://127.0.0.1:<kestrel port>
```

The storefront is either a vc-frontend clone on a Vite dev server, one for the whole run, whose proxy forwards the
platform's paths to the current group's Kestrel, or a built storefront (the theme zip's `default` folder), which
Kestrel serves from its own origin, with `index.html` for page routes, the way the cloud serves it.

The hub reaches the storefront the way it does in production: as the module's Module Federation plugin. The
storefront asks `store.plugins` for it, the platform's own `AppManifestService` answers from the module's
`plugins/vc-frontend/plugin.json`, and Kestrel serves the build on the platform's route for it.

## Platform and modules

The backend runs the platform's and the modules' own code, as a deployment runs it; the test's own code is only the
sign-in token endpoint and the list of installed modules (see Stand-ins below).
- **Data:** every module and the platform keep their data in an in-memory SQLite database of their own
  (`Infrastructure/ModuleDatabases.cs`), created from their EF model when first used. Each DbContext opens a connection
  of its own to it (shared-cache mode), as on a database server: a module's code may query on several DbContexts at
  once. The SQL they run goes to `SqlCommandLog`, which a test reads where it asks how often a module was called.
- **Search:** the search module's gateway serves the Lucene provider with its index in RAM.
- **Registrations** (`Infrastructure/PlatformModules*.cs`): the platform's own extensions where it has them (Platform.Data's
  `AddPlatformServices`: settings, localizable settings, dynamic properties, translations, the change log, the caches,
  the event bus; its `UseDbTriggers`, once per process: the audit stamps), and Platform.Web's `Startup` restated where
  it is the host's own (ASP.NET Identity with the platform's user and role managers and two-factor sign-in,
  authorization, the distributed lock service, the HTTP client factory, Kestrel's synchronous IO). Each module
  registers what its `Module.Initialize` registers at the package version referenced here, minus export and import,
  bulk actions, background jobs and the event handlers that only enqueue one, and admin authorization: store, core
  (currencies), customer, catalog, pricing, marketing, assets on the file system blob provider with the file experience
  API, inventory, payment, shipping, cart, orders, notifications (with its Liquid renderer, no email gateway, and its
  registrar restated so that a notification reaches the shared type factory complete: `HarnessNotificationRegistrar`), push
  messages (with its GraphQL), task management, and Google Analytics when a test stands Google in. X-API, X-Catalog, X-Cart and X-Order
  register through their own extensions. The module under test is not restated: its own `Module.Initialize` and
  `Module.PostInitialize` run, which register its settings, permissions, notification and event handlers, map
  `/graphql/sales-rep` and seed its roles and the Call task type.
- **The storefront shell** (`StorefrontShellModules.cs`): X-Profile 3.1020.0 (`me`, the organization, the address
  books), X-Frontend's VCST-6078 prerelease (`pageContext`, and `layout` / `saveLayout` for the hub's dashboards) with the SEO module behind its slug, X-CMS over the content module's menus (`menu`) and X-Recommend
  (`searchHistory`), each as its `Module.cs` registers it.
- **Configuration:** what the platform's shipped `appsettings.json` sets that this code reads (the default store, the
  GraphQL playground (on), the authentication types GraphQL ignores, the order full-text search), the platform's
  country lists (copies in `Platform/localization`), and the documents library's upload scope as the module's README
  tells a deployment to declare it.
- **Startup:** the other modules' `PostInitialize` (settings, permissions, dynamic property types, search request
  builders, the GraphQL endpoints), then the module's, then what the notifications module does once the application has
  started (it saves every registered notification). The host runs as Development, so a failing resolver puts its
  exception in the response, and validates its container at start as a Development platform does, except for the
  lifetimes (see What the run taught).
- **Seed:** what an administrator would set up, through the modules' services: the currencies (USD primary, EUR), the
  stores, the Order.Status dictionary, and for the hub one language, the hub's public store setting on, guests allowed,
  push messages off and X-API's ReturnModuleVersion on (`StorefrontServices.ConfigureStoreAsync`). Reps, organizations and tasks come from
  each group's seed, through the module's own controller and mutations.

Where a module ships provider-specific SQL (SQL Server, PostgreSQL, MySQL), the harness runs the module's PostgreSQL
statements on SQLite: `SqliteCatalogRawDatabaseCommand` and `SqliteCartRawDatabaseCommand`. Statements nothing here
reaches throw with their name instead.

### Stand-ins

Two pieces stand in for the platform's, and one for an outside service:
- **Tokens:** the platform's OpenIddict server isn't hosted. `TestTokenService` mints a bearer token with the claims a
  token carries (the platform's claims principal factory and the modules' claim providers: the customer module adds the
  organization), for an account of the security database, and the test endpoints answer the storefront's refresh grant
  and revoke.
- **Installed modules:** the platform builds that list by discovering module folders at startup.
  `Infrastructure/InstalledModuleService.cs` reports the module (from its real `module.manifest`), X-Frontend, the app
  whose plugins `store.plugins` lists, and task management when the group hosts it.
- **Google's Analytics API** (component tests only): `ComponentTests/Infrastructure/TestAnalytics.cs` answers the
  analytics module's report client from a seeded event pool; the module's own service, data source and settings
  resolver run.

## Layout

| Piece | File | Role |
|---|---|---|
| Backend host | `Infrastructure/InProcessBackend.cs` | Copies the harness registrations into a `WebApplication` (Development, its container validated), lets the platform's and the modules' startup add their endpoints, starts the shell modules, sets the store up, builds the schema eagerly, starts Kestrel on port 0 (with a clone, on the port the run's Vite proxies to) and self-checks the `store` query; `RunExclusiveAsync` gives test-side reads a turn between requests. |
| Platform and modules | `../Infrastructure/PlatformModules*.cs`, `PlatformStatics.cs`, `ModuleDatabases.cs`, `SqlCommandLog.cs`, `PlatformDbTriggers.cs`, `SeededDates.cs`, `TestedModule.cs` | Shared with the component tests: see Platform and modules. |
| Shell modules | `Infrastructure/StorefrontShellModules.cs` | X-Profile, X-Frontend with the SEO module, X-CMS with the content module, X-Recommend: everything the storefront shell asks for around the hub's pages, answered by those modules. |
| Shell services | `Infrastructure/StorefrontServices.cs` | The shell modules, the token service and the platform's `AppManifestService` for `store.plugins`; Kestrel's synchronous IO and JWT bearer auth; the pipeline before the modules' endpoints (a built storefront's files, plugin files, one request at a time); the store as an administrator sets it up for the hub. |
| GraphQL over HTTP | `Infrastructure/GraphQlHttp.cs` | A GraphQL POST as Apollo sends one: `application/json` exactly, the only JSON type X-API accepts. |
| Hub plugin | `Infrastructure/StorefrontPlugin.cs` | The module's plugin build (`plugins/vc-frontend`) and its route; a missing build, or one older than its sources under `StorefrontApp/src`, fails the group. |
| Module list | `../Infrastructure/InstalledModuleService.cs` | A stand-in (see Stand-ins): the module's own `module.manifest` plus two dependencies at a version the storefront's "outdated backend" check cannot flag. |
| Tokens | `Infrastructure/TestTokenService.cs`, `TokenResponse.cs`, `TestTokenEndpoints.cs` | A stand-in (see Stand-ins): production-shaped JWTs via `IUserClaimsPrincipalFactory` + `ITokenClaimProvider`s; `/connect/token`, `/revoke/token` (`TestTokenEndpoints`). |
| Storefront location | `Infrastructure/StorefrontLocation.cs` | Finds the storefront, a clone or a build (see Running); a missing or broken one fails the groups with what to do. |
| Vite | `Infrastructure/ViteDevServer.cs`, `Infrastructure/SharedViteDevServer.cs` | A clone's dev server, one per run: `node node_modules/vite/bin/vite.js --port N --strictPort` with `APP_BACKEND_URL`, which is fixed at start, so each group's backend takes that port in turn; started by the first group that needs it, stopped by an xunit assembly fixture once every test has run; waits for HTTP 200; bounded kill. |
| Group fixture | `Infrastructure/StorefrontEnvironment.cs` | Backend + seed + browser per xunit collection, on the run's Vite for a clone; `OpenAsync(userId, path)` = signed-in context, in the group's `BrowserTimeZone` when it pins one or in the zone a test passes, with `beforeLoad` to prepare the context (routes) before the first navigation; `CreateTaskAsync` seeds a task through the real mutation; `NewRepAsync` gives a test a rep of its own and `AddTaskAsync` a task for it; `WaitForTaskAsync` and `CountTasksAsync` read task rows between requests, `RunExclusiveAsync` anything else; `WithTaskManagement` for a group on a platform without the task management module; phase log. |
| Request gate | `Infrastructure/RequestGate.cs` | One turn at a time, for both hosts: their requests wait at it, and test-side reads take a turn through `RunExclusiveAsync`. |
| Session | `Infrastructure/StorefrontSession.cs` | Records console errors, dialogs and GraphQL errors; a failing test gets them plus a screenshot in its message, and a test can assert there were no GraphQL errors. |
| Hub controls | `Infrastructure/HubControls.cs` | The hub's task controls by role and label, the steps the task groups repeat, the Google Calendar stand-in route, and `Exactly`, which tolerates Intl's spaces in an input's value. |
| Layout audit | `Infrastructure/LayoutAudit.cs` | QA's audit widths and overflow audit (rule BL-UI-004) for the layout tests: prepares a page for measuring, walks the widths, and lists every width and element that broke (see Layout tests). |
| Layout controls | `Infrastructure/LayoutControls.cs` | Layout editing on a hub page: the Edit layout toggle, a stat hidden by keyboard, and a save proved by the edit bar going away. |
| Clock | `Infrastructure/NewYork.cs` | Today and instants in New York, the zone the time-sensitive groups pin; the next nights the clocks skip or repeat an hour. |
| Documents | `Infrastructure/StorefrontDocuments.cs` | Loads the storefront's own `.graphql` operations from the clone (fragments inlined, `@gate` applied). |
| Contract | `StorefrontBootContractTests.cs` | Browser-less: the backend answers the storefront's boot documents for a signed-in rep (≈4 s). |
| Group 1 | `Customers/` | Rep with two organizations and one order, and an organization the rep does not serve: list, profile, the profile's quick actions, the not-found page. `CustomerProfileGateTests`: an unserved, unknown or unreadable customer gets no statistics query, and a served one gets its statistics only once the customer has been read. `CustomerProfileLayoutTests`: a stat hidden on a customer's page is saved under that page's own scope and no longer fetched (a rep of its own). |
| Group 2 | `Calendar/` | Rep with one task: complete it on the tasks page, create one from the form; both asserted on `WorkTask` rows. |
| Group 3 | `Calls/` | Browser in New York time. Carl has calls and plain tasks, which the editing tests change; a test that books a call or takes a customer away signs in as a rep of its own. `CallsTests`: schedule a call from a customer's page, also adding it to Google and Apple Calendar, book one from the task form, edit calls and tasks, check what the list shows of a call, and check a customer the rep stopped serving. `CallTimesTests`: the edges of a day, the nights the clocks skip and repeat an hour, Los Angeles, a time off the select's steps, the select itself. `CallSavesTests`: a call without a customer, a save refused because the customer is no longer the rep's (with the Google tab; without it, skipped until VCST-6455), the Google tab blocked, a double click, a cancelled draft. Each test is asserted on the page and on `WorkTask` rows. |
| Group 4 | `Tasks/` | Rep with plain tasks, browser in New York time: overdue, today, later, completed, canceled and dateless ones. An edit that changes only the date, a canceled and a completed task edited, a dateless one opened and dated, a delete, completing and reopening across the statuses, and the filters: customers, statuses, priorities, the search and the day, their chips, Reset filters, and the calendar's dots following them. The dashboard's overdue link: the list on Overdue over every date, the link's parameter dropped from the address, Back to the dashboard. |
| Group 5 | `WithoutTaskManagement/` | A module list without TaskManagement: the customer page offers "Schedule call" disabled, the hub menu has no Tasks, and the tasks page sends the rep to a dashboard without the tasks widget. |
| Group 6 | `Tasks/`, the `TasksLayout…` files | The tasks page's layout, in New York time, with QA's stress content: a 100-character customer name, two names that differ only at their ends, one with no break in it, an 80-character title, an unbroken SKU and long notes. The page as it opens, under every filter, with the Customers filter open, and with a task's edit popup, each at QA's 19 widths (see Layout tests). |
| Group 7 | `Dashboard/` | The hub dashboard. Dora placed two orders for Tailspin Toys, one in this store and one in another; a test that saves a layout signs in as a rep of its own. `DashboardLayoutTests`: a stat hidden by keyboard is saved under the hub's scope (read back through X-Frontend's `ILayoutService`) and its period is no longer fetched; the recent orders' row limit is saved and fetched. `DashboardWidgetsTests`: the dashboard covers the current store only, in the widget, on the cards and in the `storeId` its requests carry; with every hub query failing, each widget says so and no error toast appears. |

Groups are xunit collections with `DisableParallelization = true`: each runs alone, so one backend and one browser
exist at a time, and a clone's one Vite serves them in turn. Tests inside a group share the environment and its seed;
each test gets its own browser context (fresh localStorage, fresh Apollo cache), and a test that adds tasks or takes a
customer away signs in as a rep of its own (`NewRepAsync`). Nothing is cleaned up: the databases and the index live in
memory and go with the group, and what a test changes is its own rep's. Each test carries a 5-minute timeout.

Measured on the workstation that produced this draft, against a clone: the contract test and the first five groups run in
about three minutes; a group's environment is up in under a second (the run's first, which starts Vite, in about
four); the run's first page load costs ≈20 seconds (Vite compiles on demand, then the storefront loads the hub
plugin), every later one 1–2 seconds, the first page of each later group included.

## Running

The tests run wherever they find a storefront, and anywhere else they fail with what is missing. The module CI never
runs them: every class also carries the `IntegrationTest` category, which `vc-build Test` leaves out by default. In
GitHub, the `E2E tests` workflow runs them on demand against any vc-frontend branch (see the module README).

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
dotnet test --filter "Category=StorefrontE2E"                # contract + every storefront group, layout included
dotnet test --filter "Category!=IntegrationTest"             # everything else, as vc-build Test runs it
E2E_HEADED=1 dotnet test --filter "Category=StorefrontE2E"   # watch the browser
```

The layout tests can also run apart from the functional ones (see Layout tests).

Diagnostics land in `bin/Debug/net10.0/StorefrontE2E-diagnostics/`: `environment-*.log` (timestamped phases per
group), `vite-<port>.log` (the run's dev server output, with a clone), `<TestName>.png` (full-page screenshot of a
failed test).

## Layout tests

Group 6 checks what fits, not what works. Its class carries `Kind=Layout` besides the two categories every group
carries, so a run can pick the tests by kind. The `E2E tests` workflow runs them all.

```
dotnet test --filter "Category=StorefrontE2E"                            # functional and layout, as the workflow runs them
dotnet test --filter "Kind=Layout"                                       # layout only, e.g. after a style change
dotnet test --filter "Category=StorefrontE2E&Kind!=Layout"               # functional only
dotnet test --filter "Category=StorefrontE2E&FullyQualifiedName~Tasks"   # one page, both kinds
```

Each test opens one state of the page and walks QA's audit widths: every ui-kit breakpoint (480, 640, 768, 1024,
1280, 1500), the pixel below it, the middles between them, and 375 and 1920 (ai-tools
`scripts/lib/design-tokens.generated.ts`, `AUDIT_VIEWPORTS_PX`). At each width it applies QA's overflow audit, rule
BL-UI-004 (ai-tools `scripts/lib/measure-layout.ts`):
- the page never scrolls sideways: the document's scroll width stays within its client width;
- no text inside the page or its open popup is cut off by `overflow: hidden` without an ellipsis (`text-overflow` or
  `line-clamp`). Text for screen readers only, and boxes with no text, don't count.

A failure lists every width and element at once, for example `768 px: the page scrolls sideways, 805 px of content in
768: span.vc-chip__content in div.sales-rep-filter-chips “Northwind Wholesale…” ends at 804`, and leaves screenshots
of the first three widths that failed as `<test>-<width>px.png` in the diagnostics folder.

What keeps the results the same from run to run:
- the group seeds its own data, and the browser is the Chromium build Playwright pins;
- the checks compare each element with its own box, never with a pixel value;
- before measuring, the page loses its transitions and animations;
- the storefront's own font (Lato, from Google Fonts) must have loaded. A fallback font changes the width of every text,
  and the fallbacks differ by OS, so a font that did not load fails the test rather than measuring something else;
- after each resize the audit waits until the page's size holds still.

The results still depend on the storefront build: its account layout decides how wide the content column is at each
width, so a defect can show at fewer widths here than on a deployed store.

Not checked: touch targets and their spacing at phone widths (BL-UI-006), German labels, a mobile browser context (the
tests resize a desktop one), and whether text that fits is also readable: two look-alike names cut with an ellipsis
pass.

## Harness changes

- `SalesRepTestContext.Create(configureOverrides, withTaskManagement, beforeModuleStart, buildHost)`: `buildHost` lets a
  host build (and own) the web host, on which the platform's and the modules' startup then runs; `withTaskManagement`
  installs the task management module or leaves it out; `beforeModuleStart` seeds what the database held before the
  module started.
- The store is configured through the store service (`SaveStoreAsync`), settings through the settings manager
  (`SetSettingAsync`); `UserIdClaimTypes` lists `sub` first (the platform's token claim) and `NameIdentifier` (the
  component tests'), as Platform.Web sets them.

## What the run taught

- The storefront sends every operation, sales-rep ones included, to `/graphql`: X-API's own schema there holds every
  hosted module's fields, the sales-rep ones too; the scoped `/graphql/sales-rep` serves the component tests and codegen.
- A root field that is missing fails the whole operation with a validation error. With the shell's modules hosted for
  real, every field the storefront shell asks for is the module's own: `pageContext`, `me`, the organization and the
  address books (`isLockedForCurrentUser` included), `menu`, `searchHistory`, the cart.
- Package versions: the storefront is built against newer X-API modules than the module pins. The X-Frontend prerelease
  with the layout API (VCST-6078) builds on X-Profile 3.1020.0, which needs X-Order 3.1014, so the tests reference those. The eager schema build names a missing service; the contract test turns a missing field
  into a four-second failure.
- On the real platform the host needed what a platform has and the old stand-ins didn't ask for: Kestrel's synchronous
  IO (X-API's serializer reads the body synchronously), `application/json` exactly on a GraphQL POST, and the customer
  module's token claim provider, which puts the organization in the token (without it the contact's `organizationId`
  is empty).
- As Development, the host validates its container at start, and that named what the restated registrations had left
  out (two-factor sign-in, the distributed lock service, purchased products, product suggestions, the account sessions
  factory), and one thing that is the module's: its `SalesRepCustomerCartSharingScopePolicy` (transient) takes
  `ISalesRepOrganizationAccessService`, which takes the SCOPED `ISalesRepRoleResolver`, and X-Cart's singleton
  `CanAccessCartAuthorizationHandler` takes the policy through `ICartSharingService`. A Development platform refuses to
  start with that; a production one starts, and the role resolver behind the wish list check then lives for the
  application's lifetime, with the granting roles it memoized. The host checks everything but the lifetimes. A known
  issue: VCST-6476 (vc-module-sales-rep#29, not merged yet) makes the resolver a singleton; the hosts check the
  lifetimes again once this branch has it.
- The real push messages module brought what a store has by default: push messages on (a public store setting). The
  storefront then queried `pushMessages`, which the module's GraphQL package answers, and subscribed to new ones over a
  WebSocket. X-API authenticates that socket through OpenIddict's validation scheme, and the socket, alive as long as
  the page, sits outside the request gate: its operations ran beside the page's requests on the databases' single
  connections of the time, and the tasks page stalled (GraphQL requests timed out). The hub has no part in push messages, so the E2E
  store has them off.
- The storefront's primary contact for a customer is the organization owner, falling back to its oldest contact,
  and a rep is a contact of the organizations they serve: a test that wants a buyer's details must seed the owner.
- Since #13 the hub is the module's Module Federation plugin, and vc-frontend `dev` ships no built-in hub: without
  `store.plugins` every hub page is the storefront's 404.
- Since VCST-6078 the hub's dashboard and customer profile keep their layouts through X-Frontend's `layout` and
  `saveLayout`. A host without them still passed every test: each hub dashboard showed its default arrangement under
  "The saved layout could not be loaded…", with no Edit layout button. The host serves X-Frontend's real layout code
  now, from a test-only package reference that follows the manifest's version.
- The hub's pages send their queries in parallel, and each in-memory SQLite database lived on one connection, which is
  not thread-safe. The symptoms were "database is locked" and index errors inside the connection's command list. So the
  host answers one request at a time, and a test's own reads take the same turn (`RunExclusiveAsync`). Each DbContext
  has a connection of its own now (the component tests needed it: the cart statistics load their periods in parallel);
  the turn stays, so that a test reads after the page's requests, not between them.
- The storefront compares the module versions it is told with its own `backend-packages.json`. When one is lower, it
  shows an "outdated backend" error on every page, which covers the page's top-right buttons ("New task" among them)
  for fifteen seconds. The module under test reports its real manifest version; the stand-ins report a version no
  storefront expects to exceed, since a build keeps its list inside its bundle. So the error shows only when the module
  under test really is older than the storefront expects.
- Adding a call to the calendars opens a tab and downloads a file. Playwright starts Chromium with pop-up blocking off,
  so the scenario proves the Google link and the `.ics`, not a browser's pop-up policy. The tab opens blank and gets
  Google's page after the save, so the test waits on its URL (`ToHaveURLAsync`), not on a load, and routes Google to a
  stand-in page. `VcCheckbox` draws its box over the real input, so a test ticks it by its label, as a rep does.
- That stand-in route, registered on the context once the page was up while the hub plugin still loaded, left the
  page blank in two runs, with no failed request and no error. It is registered before the first navigation now
  (`OpenAsync`'s `beforeLoad`).
- The task list pages at 15 rows. Tests that add tasks to a shared rep push the seeded ones to page 2, so a test that
  adds tasks signs in as a rep of its own.
- A second full load in the same page (`GotoAsync` after `OpenAsync`) can leave the page blank on a clone: its module
  requests failed with `net::ERR_INSUFFICIENT_RESOURCES` or were aborted. A test that needs another page by URL opens a
  session of its own.
- A save's toast replaces the one before it, which stays in the page for its leave animation, so two can show for a
  moment. A save is proved by its dialog closing; the last toast is the new one.
- A layout save is proved by the edit bar going away. The Edit layout toggle stays on screen while editing, so waiting
  for it proved nothing: a session closed right after the click cancelled the save still queued at the request gate,
  and the test then read no layout.
- Intl puts a narrow no-break space before AM/PM. A text assertion given a string collapses whitespace on both sides,
  that space included, so it matches plain text; an input's value is compared as it is, so the time field takes
  `Exactly`.
- Vite compiles on demand and keeps what it compiled. Inside a group, every page after the first loaded in about a
  second, each in a fresh browser context, so a dev server per group cost each group a 20-second first load. One
  dev server serves the whole run now.

## Known gaps (draft)

- Push messages are off in the E2E store (every store has them on by default): the storefront would subscribe to them
  over a WebSocket, which stays outside the one-caller-at-a-time gate (see What the run taught). The module and its
  GraphQL are hosted; no test opens the bell.
- The sign-in page is reachable through the password grant, but no test uses it yet.
- Other modules' event handlers that enqueue background jobs are not registered (change logs, event-based indexing,
  order and task notifications): the tests index what they read and read no change log.
- Nothing is sent: an email is rendered and saved as a message, a push message is saved; the account sessions service
  is OpenIddict's and goes with the token server.
