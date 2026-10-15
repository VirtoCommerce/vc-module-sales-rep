# Temporary: the unreleased facade

`vc-frontend-core-0.2.4.tgz` is `@vc-frontend/core` 0.2.4, packed with `npm pack` from
VirtoCommerce/vc-frontend#2552 at `021d34bb` (`feat/VCST-6078-user-dashboard`, the commit that exports the
dashboard layout engine through the facade) — the same command the Core Facade Release workflow runs. It lets this branch build and
ship its plugin before that facade is released.

Once the release exists (its number may differ, depending on which facade change merges first): pin
`https://github.com/VirtoCommerce/vc-frontend/releases/download/core-v<version>/vc-frontend-core-<version>.tgz` in
`package.json`, set `requiredHostVersion` in `vite.config.ts` to `^<version>`, run `yarn install`, and delete this
folder.
