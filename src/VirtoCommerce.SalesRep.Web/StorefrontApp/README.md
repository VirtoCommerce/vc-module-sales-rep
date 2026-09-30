# sales-rep — storefront plugin

The Sales Rep Hub as a Module Federation remote for the VC storefront (`vc-frontend`):
the hub dashboard, My customers, the customer profile, list sharing to a customer, and
the buyer-facing "My Sales Reps" page (`/company/sales-reps`). Data comes from this
module's Experience API.

It ships inside this module: `yarn build` writes the remote into
`../plugins/vc-frontend/`, the folder the platform probes for `vc-frontend` plugins
(see "Packaging" below). Full walkthrough of the plugin model (host facade, contract
gate, dev loop): the host repo's `client-app/modules/federated/HOWTO.md`.

## Scripts

| Script | Purpose |
| --- | --- |
| `yarn build` | Production build of the remote into `../plugins/vc-frontend/` |
| `yarn watch` | Rebuild on save; pair with `yarn preview` |
| `yarn preview` | Serve the built remote for the host (port 3001) |
| `yarn dev` | HMR dev server (see HOWTO "Dev inner loop") |
| `yarn type-check` | `vue-tsc` against the frozen facade contract |
| `yarn generate:graphql-types` | Regenerate `src/api/graphql/types.ts` from the backend schema |
| `yarn test` / `yarn test:watch` | Vitest unit tests (jsdom) |
| `yarn lint` / `yarn format` | ESLint / Prettier over the sources |

## Local development against the host

1. Build the facade from the host checkout and link it here (types only):
   `(host) yarn build:core-types && cd client-app/core-api && yalc publish --private`
   then `(plugin) yalc add @vc-frontend/core && yarn install`.
2. Serve this plugin: `yarn build && yarn preview` (or `yarn dev` for HMR).
3. Point the host at it:
   `APP_MODULES_FEDERATION_ENABLED=true APP_MODULES_FEDERATION_REMOTES='{"sales-rep":"http://localhost:3001/mf-manifest.json"}' yarn build-only --mode=development && yarn preview`

## The facade dependency & contract versioning

`@vc-frontend/core` is pinned to a versioned tarball URL (a Release asset of the host
repo) — the lockfile records its checksum. **Keep it that way in commits.** For local
co-development against an unpushed facade, use yalc (`yalc add @vc-frontend/core`);
run `yalc remove @vc-frontend/core` and restore the pinned URL before pushing — never
commit a `file:.yalc/...` dependency.

`vite.config.ts` declares `requiredHostVersion` — the contract gate: hosts whose facade
version does not satisfy it refuse to load this plugin (clean skip instead of a runtime
error). When you adopt an export added in a newer facade version, bump
`requiredHostVersion` **and** the pinned tarball URL together. This plugin requires
**^1.1.0** (`useQuery` re-export).

## GraphQL codegen

GraphQL documents live in `src/api/graphql/**/*.graphql`; `yarn generate:graphql-types`
introspects the sales-rep backend module's scoped schema at `/graphql/sales-rep` (see
`codegen.ts`) and regenerates `src/api/graphql/types.ts` (committed). Set
`APP_BACKEND_URL` in `.env` first (copy `.env.example`; `.env` is gitignored and must
stay that way). The `customerSalesReps` query is authenticated and resolves the
organization from the caller's claims — anonymous calls are rejected by the backend.

## Styling

Your plugin renders inside the host page, and the host uses unprefixed Tailwind. To avoid
clobbering host styles (or being clobbered):

1. **Style components with `<style scoped>` + `@apply`.** Vue stamps a `data-v-*` attribute
   onto the component's elements (and onto the root of any host component you pass a `class`
   to) and rewrites your selectors to match, so the styles apply only to this component and
   never leak into host pages. Utilities resolve against the host design tokens. See
   `src/pages/sales-reps.vue`.
2. **Never add `@tailwind utilities` to `src/styles.css`.** That file is injected globally;
   a re-emitted flat utility (e.g. `.flex-col`) would win by source order and clobber host
   elements that rely on a later variant like `lg:flex-row`. Keep global CSS to
   plugin-owned selectors you fully control.
3. **Escape hatches (optional).** If a scoped rule and something else fight on the same
   element, add `!important` to that one declaration — a deliberate, local override. And if
   you ever need a whole subtree scoped without per-component styles, Tailwind's
   `important: ".<plugin-root>"` config (with a matching root class) confines every emitted
   utility to that subtree; it works but re-introduces a global utility layer, so prefer
   scoped styles.

## Packaging into the module

The platform discovers storefront plugins by walking installed modules and probing
`{moduleRoot}/{pluginsDiscoveryFolder}/{appId}/` — `plugins/vc-frontend/` here, from
`VirtoCommerce.XFrontend`'s app declaration. Two things wire this module into that walk:

1. `module.manifest` depends on `VirtoCommerce.XFrontend`.
2. `yarn build` emits `remoteEntry.js` and `plugin.json` into `../plugins/vc-frontend/`
   (`public/plugin.json` is copied verbatim by Vite).

`plugin.json` overrides the platform defaults, which would otherwise be the .NET module
id and `./Module`:

```json
{ "id": "sales-rep", "remote": { "name": "sales-rep", "exposed": "./plugin" } }
```

The built folder is gitignored — CI builds it before packing the module zip.
Deliberately no `permission` field: the plugin also contributes the buyer-facing
"My Sales Reps" widget, so it must ship to every storefront user, not only to reps.

The storefront then reads the plugin list from xAPI:

```graphql
query { store(domain: $domain) { plugins(appId: "vc-frontend") { id version entry { type path } remote { name exposed } } } }
```
