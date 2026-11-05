import { defineAsyncComponent, markRaw } from "vue";
import { COUPONS_BLOCK_ID, COUPONS_DEFAULT_ROWS, COUPONS_MAX_ROWS, MIN_ROWS } from "../constants";
import type { SalesRepBlockType } from "../types/layout";

const SalesRepCoupons = markRaw(defineAsyncComponent(() => import("../components/sales-rep-coupons.vue")));

// Registered (via registerBlock in index.ts) only when vc-module-marketing is installed.
export const couponsBlock: SalesRepBlockType = {
  id: COUPONS_BLOCK_ID,
  region: "mainLeft",
  titleKey: "sales_rep.coupons.title",
  order: 30,
  component: SalesRepCoupons,
  settings: [{ kind: "maxRows", default: COUPONS_DEFAULT_ROWS, min: MIN_ROWS, max: COUPONS_MAX_ROWS }],
};
