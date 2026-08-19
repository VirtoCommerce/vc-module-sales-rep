import { loadEnv } from "vite";
import type { CodegenConfig } from "@graphql-codegen/cli";

// graphql-codegen does not load .env files; reuse Vite's loader so the same .env serves
// dev and codegen. process.env (shell) takes precedence over .env values.
const env = { ...loadEnv("", process.cwd(), ""), ...process.env };

if (!env.APP_BACKEND_URL) {
  throw new Error("APP_BACKEND_URL is not set — put it in .env (see .env.example) or export it in the shell.");
}

const config: CodegenConfig = {
  // The sales-rep backend module's scoped schema (registered via ScopedSchemaFactory,
  // exposed at /graphql/sales-rep by its Web module).
  schema: `${env.APP_BACKEND_URL}/graphql/sales-rep`,
  documents: "src/api/graphql/**/*.graphql",
  generates: {
    "src/api/graphql/types.ts": {
      plugins: [
        { add: { content: "// This file is auto-generated. Do not edit manually.\n" } },
        "typescript",
        "typescript-operations",
        "typed-document-node",
        "named-operations-object",
      ],
      // Mirrors the host's scripts/graphql-codegen/generator.ts CONFIG so generated
      // code is style- and scalar-compatible with the host's own modules.
      config: {
        dedupeFragments: true,
        identifierName: "OperationNames",
        maybeValue: "T",
        scalars: {
          BigInt: "number",
          Byte: "number",
          Date: "string",
          DateOnly: "string",
          Decimal: "number",
          DynamicPropertyValue: "string | number | boolean | null",
          Guid: "string",
          Half: "number",
          Long: "number",
          Milliseconds: "number",
          ModuleSettingValue: "string | number | boolean | null",
          OptionalDecimal: "number | undefined",
          OptionalNullableDecimal: "number | null | undefined",
          OptionalString: "string | undefined",
          PropertyValue: "string | number | boolean | null",
          SByte: "number",
          Seconds: "number",
          Short: "number",
          TimeOnly: "string",
          UInt: "number",
          ULong: "number",
          Uri: "string",
          UShort: "number",
        },
        skipTypename: true,
        useTypeImports: true,
        skipGraphQLImport: true,
      },
    },
  },
};

export default config;
