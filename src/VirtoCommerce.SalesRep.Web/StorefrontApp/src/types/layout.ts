// Saved dashboard / customer-profile layout (VCST-5367). The layout engine is the host's (`@vc-frontend/core`);
// this plugin only names its two surfaces. Free-form `String` server-side — the literals live in constants.ts, and
// a renamed scope reads a new, empty document, so they are load-bearing.
export type SalesRepLayoutScopeType = "dashboard" | "customerProfile";
