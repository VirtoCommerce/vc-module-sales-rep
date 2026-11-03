# Temporary: the unreleased facade

`vc-frontend-core-0.2.8.tgz` is `@vc-frontend/core` 0.2.8, packed with `npm pack` from
VirtoCommerce/vc-frontend#2596 (merged with dev, which took 0.2.5–0.2.7) — the same command the Core Facade Release workflow runs. It lets
this branch build and ship its plugin before `core-v0.2.8` is released.

Once the release exists: pin
`https://github.com/VirtoCommerce/vc-frontend/releases/download/core-v0.2.8/vc-frontend-core-0.2.8.tgz` in
`package.json`, run `yarn install`, and delete this folder.
