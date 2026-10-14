import { CODEGEN_CONFIG, CODEGEN_PLUGINS } from "@vc-frontend/core/codegen";
import { loadEnv } from "vite";
import type { CodegenConfig } from "@graphql-codegen/cli";

// graphql-codegen does not load .env files; reuse Vite's loader so the same .env serves
// dev and codegen. process.env (shell) takes precedence over .env values.
const env = { ...loadEnv("", process.cwd(), ""), ...process.env };

if (!env.APP_BACKEND_URL) {
  throw new Error("APP_BACKEND_URL is not set — put it in .env (see .env.example) or export it in the shell.");
}

// Scalars and plugins come from the host, so the same backend value never gets two different
// TypeScript types.
const codegen: CodegenConfig = {
  generates: {
    // The sales-rep backend module's scoped schema (registered via ScopedSchemaFactory,
    // exposed at /graphql/sales-rep by its Web module).
    "src/api/graphql/types.ts": {
      schema: `${env.APP_BACKEND_URL}/graphql/sales-rep`,
      documents: ["src/api/graphql/**/*.graphql", "!src/api/graphql/core/**"],
      plugins: CODEGEN_PLUGINS,
      config: CODEGEN_CONFIG,
    },
    // The operations VirtoCommerce.XFrontend serves on the platform's core schema (the saved dashboard layout,
    // VCST-6078), generated from the committed excerpt of that schema rather than by introspecting `/graphql`:
    // the rest of the core schema is the host's to type, and the excerpt pins the contract this plugin relies on.
    "src/api/graphql/core-types.ts": {
      schema: "src/api/graphql/core/schema.graphql",
      documents: "src/api/graphql/core/**/*.graphql",
      plugins: CODEGEN_PLUGINS,
      config: CODEGEN_CONFIG,
    },
  },
};

export default codegen;
