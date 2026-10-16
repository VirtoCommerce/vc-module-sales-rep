# Temporary: the unreleased facade

`vc-frontend-core-0.2.4.tgz` is `@vc-frontend/core` 0.2.4, packed with `npm pack` from
VirtoCommerce/vc-frontend#2458 at `7c73c367` — the same command the Core Facade Release workflow runs. It lets
this branch build and ship its plugin before `core-v0.2.4` is released.

Once the release exists: pin
`https://github.com/VirtoCommerce/vc-frontend/releases/download/core-v0.2.4/vc-frontend-core-0.2.4.tgz` in
`package.json`, run `yarn install`, and delete this folder.
