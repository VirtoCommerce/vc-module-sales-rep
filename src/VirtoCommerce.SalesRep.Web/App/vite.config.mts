import { getApplicationConfiguration } from "@vc-shell/config-generator";

export default getApplicationConfiguration({
  // Do NOT override outDir. vc-build's `BuildCustomApp` runs `yarn build`, then copies the app's
  // default `App/dist` output into `Content/vc-sales-rep` (deleting that folder first). Redirecting
  // the build straight to `../Content/vc-sales-rep` means the bundle gets wiped and only the
  // `build:types` output (App/dist/types) is packaged → /apps/vc-sales-rep/ 404s. Leave the default
  // `dist` output (matches vc-module-news). For local preview use `yarn serve`.
});
