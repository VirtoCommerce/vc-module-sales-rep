// Saved dashboard / customer-profile layout (VCST-5367). The layout engine is the host's (`@vc-frontend/core`);
// this plugin only names its two surfaces. Free-form `String` server-side — the literals live in constants.ts, and
// a renamed scope reads a new, empty document, so they are load-bearing.
import type { CUSTOMER_PROFILE_LAYOUT_SCOPE, DASHBOARD_LAYOUT_SCOPE } from "../constants";

export type SalesRepLayoutScopeType = typeof DASHBOARD_LAYOUT_SCOPE | typeof CUSTOMER_PROFILE_LAYOUT_SCOPE;
