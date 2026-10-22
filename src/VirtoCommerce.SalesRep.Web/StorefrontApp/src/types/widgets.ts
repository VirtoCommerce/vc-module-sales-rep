// The hub's vocabulary of statistics needs: the tokens its KPI cards declare (`layout/stat-cards.ts`) and its
// statistics queries are shaped from (`layout/stat-data-needs.ts`). The card model itself is the engine's
// `StatCardType` (`@vc-frontend/core`).
import type { StatDataNeedsType } from "@vc-frontend/core";
import type { Ref } from "vue";

/**
 * What a card needs fetched. The statistics queries are shaped from the union over the cards the rep
 * can actually see, so a hidden card costs nothing: an unneeded slice is left out of the document
 * (`@include`), and a query no visible card needs never fires at all.
 *
 * Tokens name a card's metric, not a field, and each stands for whole aggregation buckets — the unit
 * of backend cost, one bucket being one grouped query over the orders (see the batch loader in
 * CustomerOrderStatisticsType). `week` covers the week bucket and its previous-week baseline;
 * `monthOverMonth`/`yearOverYear` cover only the baseline, the current side being `mtd`/`ytd`.
 * `averageOrderValue` is the one field-level token: it rides along in the `ytd` bucket and so costs
 * bytes rather than a query.
 */
export type StatDataNeedType =
  | "newOrders"
  | "week"
  | "mtd"
  | "monthOverMonth"
  | "ytd"
  | "yearOverYear"
  | "averageOrderValue"
  | "cartStatistics"
  | "customerCounts";

/** What each hub statistics query (orders, carts, customer counts) is called with. */
export type HubStatisticsOptionsType = {
  /** What the surface's visible cards need fetched, read off its layout (`useStatDataNeeds`). */
  needs: StatDataNeedsType<StatDataNeedType>;
  // Scope to one customer (the customer profile); omit for the cross-customer dashboard.
  // Expanded union (not MaybeRefOrGetter<… | undefined>) to avoid the redundant "undefined" — Sonar S4782.
  organizationId?: string | Ref<string | undefined> | (() => string | undefined);
};
