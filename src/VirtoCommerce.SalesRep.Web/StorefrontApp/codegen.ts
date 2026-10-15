import { CODEGEN_CONFIG, CODEGEN_PLUGINS } from "@vc-frontend/core/codegen";
import { loadEnv } from "vite";
import type { CodegenConfig } from "@graphql-codegen/cli";

// graphql-codegen does not load .env files; reuse Vite's loader so the same .env serves
// dev and codegen. process.env (shell) takes precedence over .env values.
const env = { ...loadEnv("", process.cwd(), ""), ...process.env };

if (!env.APP_BACKEND_URL) {
  throw new Error("APP_BACKEND_URL is not set — put it in .env (see .env.example) or export it in the shell.");
}

const codegen: CodegenConfig = {
  // The sales-rep backend module's scoped schema (registered via ScopedSchemaFactory,
  // exposed at /graphql/sales-rep by its Web module).
  schema: `${env.APP_BACKEND_URL}/graphql/sales-rep`,
  documents: "src/api/graphql/**/*.graphql",
  // Scalars and plugins come from the host, so the same backend value never gets two different
  // TypeScript types.
  generates: { "src/api/graphql/types.ts": { plugins: CODEGEN_PLUGINS, config: CODEGEN_CONFIG } },
};

export default codegen;
