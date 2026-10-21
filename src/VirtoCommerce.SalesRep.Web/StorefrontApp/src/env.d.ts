// Env vars the plugin reads at build time; vite.config.ts `envPrefix` must expose each one.
interface ImportMetaEnv {
  /** `"true"` shows example figures on the Sales Rep Reporting page instead of the backend's (local demos only). */
  readonly APP_SALES_REP_REPORTING_MOCK?: string;
}
