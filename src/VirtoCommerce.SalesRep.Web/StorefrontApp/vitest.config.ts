import { fileURLToPath } from "node:url";
import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: [
      // "@vc-frontend/core" is types-only at runtime (host injects it via MF shared
      // singletons); Vite can't resolve its package.json entry, so tests would fail
      // before vi.mock() gets a chance to substitute the module. Point it at a real,
      // empty file - every test overrides it explicitly with vi.mock(...).
      // Exact match only — a string key matches by PREFIX, which would send
      // "@vc-frontend/core/testing" here too instead of the package's real testing.mjs.
      {
        find: /^@vc-frontend\/core$/,
        replacement: fileURLToPath(new URL("./src/mocks/vc-frontend-core.ts", import.meta.url)),
      },
    ],
  },
  test: { environment: "jsdom" },
});
