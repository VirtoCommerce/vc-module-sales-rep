# VCST-5409 follow-up — wire the sales-rep plugin to the real backend, remove data mocks

- **Tickets:** [VCST-5409](https://virtocommerce.atlassian.net/browse/VCST-5409) (FE feature),
  [VCST-5293](https://virtocommerce.atlassian.net/browse/VCST-5293) (BE role/module, delivered),
  VCST-4907/VCST-5304 (BE xAPI, [vc-module-sales-rep PR #2](https://github.com/VirtoCommerce/vc-module-sales-rep/pull/2))
- **Host PR:** [vc-frontend #2372](https://github.com/VirtoCommerce/vc-frontend/pull/2372)
  (branch `feat/VCST-5409-sales-reps`) — facade & scaffold extensions land there.
- **Backend env:** `https://vcptcore-dev.govirto.com` — module + xAPI build deployed and verified.
- **Date:** 2026-07-08

## Goal

Replace the plugin's mock data source with the real `customerSalesReps` GraphQL query,
using reactive Apollo (`useQuery`) exposed through the `@vc-frontend/core` facade, with
typed documents generated against the module's scoped schema endpoint.

## Verified backend contract (introspected on vcptcore-dev, 2026-07-08)

- Scoped schema endpoint: **`/graphql/sales-rep`** (registered via
  `ScopedSchemaFactory<XapiAssemblyMarker>` in the module's `ServiceCollectionExtensions`;
  probed live — 200).
- Query (also present on the main `/graphql` endpoint):

  ```graphql
  customerSalesReps(after: String, first: Int, keyword: String, sort: String): SalesRepContactConnection
  ```

  - **Auth required** (`AnonymousAccessDenied` otherwise); the organization is resolved
    **server-side from the caller's claims** — no org argument exists.
  - `SalesRepContact`: `id!`, `firstName`, `lastName`, `middleName`, `fullName`, `name`,
    `about`, `photoUrl`, `emails: [String]`, `phones: [String]`.
  - Active-only filtering (AC#5) is a server responsibility.

## Decisions

| Decision | Choice |
|---|---|
| Data access style | Reactive `useQuery` (Apollo composable) re-exported by the facade — not imperative `graphqlClient.query()` (user-selected Approach 3). |
| Typed documents | GraphQL codegen against `/graphql/sales-rep`, same plugin set + config as the host (`typescript`, `typescript-operations`, `typed-document-node`, `named-operations-object`; `skipTypename`, `useTypeImports`, host scalar map). |
| Codegen delivery | **Part of the `create:plugin` generator** (host repo): the `--with-apollo` group emits the codegen tooling. The sales-rep plugin is retrofitted to exactly the scaffolded shape. |
| Search/sort/paging | Server-side via query args; the client-side `selectSalesReps` selection logic is deleted with the mocks. |
| Gate (AC#2) | **Kept as-is** (hard-enabled `useSalesRepsConfig`): the BE module manifest exposes no storefront setting yet. Swap comment updated to reference the missing manifest `<settings>` entry as the future signal. |
| Generator README | `create:plugin` also emits an evergreen `README.md` (dev workflow, scripts, env — no code details). |

## Work streams

| Stream | Repo / branch | Contents |
|---|---|---|
| Host: facade + generator | `vc-frontend`, `feat/VCST-5409-sales-reps` (PR #2372) | §1, §2 |
| Plugin: real wiring | `~/vc/vc-plugins/sales-rep-plugin` | §3–§5 |

## 1. Facade extension (host)

`client-app/core-api/index.ts` additionally re-exports the host's reactive Apollo composables:

```ts
export { useQuery, useLazyQuery, useMutation } from "@vue/apollo-composable";
```

- Works because `app-runner.ts:65` `provide(DefaultApolloClient, apolloClient)` is app-wide and
  plugin components mount inside the host app; the re-export guarantees the plugin executes the
  **host's** module instance, so the injection key matches. The plugin needs no runtime copy of
  `@vue/apollo-composable`.
- `build:core-types` regenerates `contract/index.d.ts`; `CORE_VERSION` gets a **minor** bump
  (additive) → **1.1.0**.
- `@vue/apollo-composable` is already a type-peer (derived from `MF_SHARED_RANGES`) and already in
  the plugin's devDependencies — **no generator change needed** for peers.
- **Contract delivery (sequencing):** the plugin's committed dependency is a pinned release
  tarball (`core-v1.0.0`). Local dev uses yalc, but the final plugin commit requires a released
  `core-v1.1.0` asset: merge host PR #2372 → cut `core-v1.1.0` → repoint the plugin's
  `@vc-frontend/core` URL. The plugin PR is merge-gated on that release.

## 2. Generator: codegen tooling + README (host)

### 2a. Codegen in the `--with-apollo` group

When the Apollo group is selected, `create:plugin` additionally emits:

- `codegen.ts` — schema `${APP_BACKEND_URL}/graphql/<plugin-name>` (a commented TODO tells the
  author to correct the endpoint path if the module registers a different scope name), documents
  `src/api/graphql/**/*.graphql`, output `src/api/graphql/types.ts`, plugin set + config copied
  from the host `scripts/graphql-codegen/generator.ts` values. It starts with
  `import "dotenv/config"` (codegen-cli does NOT load `.env` itself) and **fails loudly** when
  `APP_BACKEND_URL` is unset.
- `.env.example` with `APP_BACKEND_URL=`; the emitted `.gitignore` **gains `.env`** (it does not
  have it today — generator change, unconditional).
- `package.json`: `generate:graphql-types` script + codegen devDependencies (versions aligned
  with the host's) **plus `dotenv` and `@graphql-typed-document-node/core`** (generated types
  import it directly; relying on hoisting is fragile).
- A sample `src/api/graphql/queries/.gitkeep` (no fake query — the author writes real documents).
- Apollo is opt-in (`defaultOn: false`): non-interactive scaffolding that needs this must pass
  `--yes --with-apollo` (docs/tests must not assume `--yes` alone).

No new flag: codegen is meaningless without GraphQL, and a GraphQL plugin without typed
documents is the anti-pattern we're removing. `--with-apollo` remains opt-in, so UI-only
plugins carry none of this.

### 2b. README.md (extend the existing template)

The generator **already emits a README** (build/preview, HOWTO link, styling rules, facade/yalc
rule — `create-plugin.mjs` ~line 468). This section **extends that template**, not a new file.
Evergreen content only — things that rarely change:

- One-line purpose + link to the host repo / MF harness HOWTO.
- Prerequisites (Node/Yarn versions matching host, a running host dev server).
- Scripts table, generated to match the selected groups (dev/build, test when test tooling is on,
  lint/format when lint is on, `generate:graphql-types` when Apollo is on).
- Local dev workflow: publish-from-source consumption of `@vc-frontend/core` (yalc), pointing the
  host at the plugin via `APP_MF_REMOTES`, contract-version peer rule (plugin declares the
  `CORE_VERSION` it was built against; host gates load on major mismatch).
- Env vars (`APP_BACKEND_URL` when Apollo is on).
- Style isolation note (Vue scoped + `@apply`; no global Tailwind emission).

The sales-rep plugin gets this README retrofitted with its concrete values.

## 3. Plugin data layer swap

- **Delete** `src/api/mock.ts` and `selectSalesReps` (client-side filter/sort/paging).
- **Add** `src/api/graphql/queries/customerSalesReps/customerSalesRepsQuery.graphql`:

  ```graphql
  query CustomerSalesReps($first: Int, $after: String, $keyword: String, $sort: String) {
    customerSalesReps(first: $first, after: $after, keyword: $keyword, sort: $sort) {
      totalCount
      items {
        id
        name
        fullName
        emails
        phones
      }
    }
  }
  ```

- `useSalesReps` keeps its **exact public shape** (`loading, keyword, sort, page, pages, items`;
  `sales-reps.vue` does not change) and becomes a wrapper over the facade `useQuery`:
  - `variables` computed: `first: PAGE_SIZE`, `after: String((page - 1) * PAGE_SIZE)` (xAPI
    connections accept offset-as-cursor — established host convention), **debounced keyword**
    (trimmed, via `refDebounced(keyword, 300)` so page/sort clicks stay immediate — a
    query-level `debounce` option would delay pagination too), `sort: "${column}:${direction}"`.
  - `useQuery(CustomerSalesRepsDocument, variables, { keepPreviousResult: true })`.
  - Mapping: `SalesRepContact → { id, name: fullName || name || "", email: emails[0] ?? "", phone: phones[0] ?? "" }`.
  - `pages = max(1, ceil(totalCount / PAGE_SIZE))`.
  - **Page-reset ownership: the page keeps it** — `sales-reps.vue` already resets `page` on
    keyword (line ~53) and sort (line ~63) change; the composable does NOT duplicate it.
  - The plugin-local `SalesRep` type is **trimmed to `{ id, name, email, phone }`** — `isActive`
    exists only for the mock's client-side filtering (server sends only active reps, AC#5).
  - Generated types stay internal to the api layer.
- **`requiredHostVersion` bumps to `"^1.1.0"`** in the plugin's `vite.config.ts` — the plugin now
  imports a 1.1.0-only export, and the host's version gate is the mechanism that turns a
  too-old-host mismatch into a clean load refusal instead of a runtime `TypeError`.
- `sharedOverrides` in `vite.config.ts` stay (Apollo packages remain unbundled) with comments
  updated: "consumed via the facade — never imported directly".
- `useSalesRepsConfig` untouched except the swap comment now references the missing BE manifest
  setting explicitly.

## 4. Errors & auth

- Route inherits `requiresAuth` + `requiresOrganization` from the `Company` parent — anonymous
  users never reach the query.
- On query error: page stays functional — empty items + existing `VcEmptyView`; error surfaced
  via `useQuery`'s `onError` → `console.error` (the facade exposes no Logger; adding one is out
  of scope). No toasts, per the harness dev-signal decision.

## 5. Testing & verification

- **Unit (plugin):** stub via explicit `vi.mock("@vc-frontend/core", ...)` with a controllable
  `useQuery` fake — the established pattern in `index.test.ts` (`src/mocks/vc-frontend-core.ts`
  is a resolver-only alias target, not a shared mock). Re-target `useSalesReps.test.ts`:
  variable computation (offset math, sort string, keyword trim + debounce), item mapping
  (missing emails/phones), `pages` math, loading/error passthrough. `selectSalesReps` tests are
  deleted with it; page-reset behavior stays covered where it lives (the page).
- **Host:** `create-plugin.test.ts` extended for the codegen emission (with/without
  `--with-apollo`) and README emission; contract build asserted via existing versioning tests.
- **Live (vcptcore-dev, credentials provided by Ivan):** list renders real reps; search hits the
  server; **sort by email/phone verified** — the BE sort is member-search-backed and may only
  support `name`; if a column's sort token is unsupported, disable sorting on that column
  (no client-side fake); paging; disabled rep excluded (AC#5).
- **Already verified (2026-07-08):** authenticated `customerSalesReps` executes against
  vcptcore-dev and accepts `keyword` + `sort: "email:asc"`; with the test account in an
  organization it returns a real rep (including the `phones: []` edge the mapping handles).
  Sort **ordering** across email/phone still needs ≥2 assigned reps to confirm.

## Open risks

| Risk | Handling |
|---|---|
| ~~BE sort tokens for `email`/`phone` unsupported~~ | **Resolved (verified on vcptcore-dev with 2 reps):** the member-backed sort reorders on `name` only; `email:asc`/`email:desc` return identical order and `emails` errors. Only the **Name** column is sortable (same as Company members). |
| `after` offset-cursor semantics | Verify live (xAPI convention); fall back to accumulating `first` pages only if broken (unlikely). |
| BE xAPI PR #2 still open — schema could shift before merge | Contract is introspected from the deployed build; regenerate types on merge; codegen makes drift a compile error. |
| No storefront gate setting (AC#2) | Explicitly deferred (user decision); gate stays hard-enabled with documented swap point. |

## Out of scope

- `salesRepCustomer(s)` queries (rep-side "My customers" — separate ticket).
- BE manifest `<settings>` for the role-name gate (BE change).
- Folding host codegen and plugin codegen into one shared package (the generator template copies
  values; a shared package is premature for one consumer).
