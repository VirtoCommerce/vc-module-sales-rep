import { getApplicationConfiguration } from "@vc-shell/config-generator";

export default getApplicationConfiguration({
  // Output the embedded build straight into the module's Content folder, which the platform
  // serves at /apps/vc-sales-rep/ (Modules/VirtoCommerce.SalesRep/Content/vc-sales-rep).
  build: {
    outDir: "../Content/vc-sales-rep",
    emptyOutDir: true,
  },
});
