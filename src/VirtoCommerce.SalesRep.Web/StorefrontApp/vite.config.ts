import { federation } from "@module-federation/vite";
import { createRemoteFederationOptions } from "@vc-frontend/core/federation";
import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [
    vue(),
    // Wiring conventions (expose key, shared singletons, manifest metadata) come from
    // the host - client-app/core-api/federation.mjs in the host checkout owns them.
    federation(
      createRemoteFederationOptions({
        name: "sales-rep",
        // CONTRACT GATE: the facade version this plugin is built against.
        requiredHostVersion: "^0.1.0",
        // No sharedOverrides: the host's set is exactly what this plugin needs. Apollo and
        // @vue/apollo-composable are imported DIRECTLY here (useSalesRepHubQuery), so bundling
        // a second copy would give the plugin its own client and cache instead of the host's;
        // @vueuse/core carries the extension registry's createGlobalState, which must be the
        // host's instance or a registration lands in a store nothing reads. sortablejs is a
        // leaf DOM library with no cross-copy state, so it rides along in the bundle.
      }),
    ),
  ],
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
