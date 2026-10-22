# Temporary: the unreleased facade

`vc-frontend-core-0.2.5.tgz` is `@vc-frontend/core` 0.2.5, packed with `npm pack` from
VirtoCommerce/vc-frontend#2596 — the same command the Core Facade Release workflow runs. It lets
this branch build and ship its plugin before `core-v0.2.5` is released.

Once the release exists: pin
`https://github.com/VirtoCommerce/vc-frontend/releases/download/core-v0.2.5/vc-frontend-core-0.2.5.tgz` in
`package.json`, run `yarn install`, and delete this folder.
