import { fileURLToPath } from "node:url";
import { federation } from "@module-federation/vite";
import { createRemoteFederationOptions } from "@vc-frontend/core/federation";
import { pluginContributions } from "@vc-frontend/core/manifest";
import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vite";
import contributions from "./plugin.config";

export default defineConfig({
  plugins: [
    vue(),
    // Writes plugin.config.ts into the built plugin.json.
    pluginContributions(contributions),
    // Wiring conventions (expose key, shared singletons, manifest metadata) come from
    // the host - client-app/core-api/federation.mjs in the host checkout owns them.
    federation(
      createRemoteFederationOptions({
        name: "sales-rep",
        // CONTRACT GATE: the facade version this plugin is built against.
        requiredHostVersion: "^0.2.3",
        // No sharedOverrides: the host's set is exactly what this plugin needs. Apollo and
        // @vue/apollo-composable are imported DIRECTLY here (useSalesRepHubQuery), so bundling
        // a second copy would give the plugin its own client and cache instead of the host's;
        // @vueuse/core carries the extension registry's createGlobalState, which must be the
        // host's instance or a registration lands in a store nothing reads. sortablejs is a
        // leaf DOM library with no cross-copy state, so it rides along in the bundle.
      }),
    ),
  ],
  resolve: {
    alias: {
      // @module-federation/vite 1.18 resolves every import it finds under the exposed module, `import type`
      // included, and resolving this types-only package (`"main": ""`) throws. Only the generated types import
      // it, so nothing loads it at runtime. Fixed upstream in 1.23.1; drop this when the host moves past it.
      "@graphql-typed-document-node/core": fileURLToPath(
        new URL("./node_modules/@graphql-typed-document-node/core/typings/index.d.ts", import.meta.url),
      ),
    },
  },
  build: {
    target: "esnext", // MF entry uses top-level await
    // The platform probes {moduleRoot}/plugins/{appId}/ for a remote, so the bundle is
    // written straight into the discovery folder the module ships. `public/plugin.json`
    // rides along and overrides the platform's defaults (remote name, exposed key).
    outDir: "../plugins/vc-frontend",
    emptyOutDir: true,
  },
  server: { port: 3001, cors: true, origin: "http://localhost:3001" },
  preview: { cors: true },
});
