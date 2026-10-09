import { defineConfigWithVueTs, vueTsConfigs } from "@vue/eslint-config-typescript";
import prettier from "eslint-plugin-prettier/recommended";
import pluginVue from "eslint-plugin-vue";
import globals from "globals";

// Trimmed, host-aligned flat config for a standalone MF plugin.
export default defineConfigWithVueTs(
  { ignores: ["dist/", "node_modules/", ".yalc/", "src/api/graphql/types.ts"] },
  pluginVue.configs["flat/recommended"],
  vueTsConfigs.recommended,
  { languageOptions: { globals: { ...globals.browser } } },
  {
    rules: {
      // `_`-prefixed args/vars are the deliberate "unused" convention (mock signatures, hooks).
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_", varsIgnorePattern: "^_" }],
      // Both are off in the host too, and the ported code is written to its conventions:
      // page components are named after their route segment, and props are typed with
      // `defineProps<IProps>()`, where an optional prop needs no runtime default.
      "vue/multi-word-component-names": "off",
      "vue/require-default-prop": "off",
    },
  },
  {
    // Tailwind/PostCSS configs are CommonJS by design (loaded by tools, not bundled).
    files: ["**/*.cjs"],
    rules: { "@typescript-eslint/no-require-imports": "off" },
  },
  prettier,
);
