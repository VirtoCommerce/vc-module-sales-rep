import { defineAsyncComponent, markRaw } from "vue";
import { MIN_ROWS } from "@vc-frontend/core";
import { COUPONS_BLOCK_ID, COUPONS_DEFAULT_ROWS, COUPONS_MAX_ROWS } from "../constants";
import type { BlockType } from "@vc-frontend/core";

const SalesRepCoupons = markRaw(defineAsyncComponent(() => import("../components/sales-rep-coupons.vue")));

// Registered (via registerBlock in index.ts) only when vc-module-marketing is installed.
export const couponsBlock: BlockType = {
  id: COUPONS_BLOCK_ID,
  region: "mainLeft",
  titleKey: "sales_rep.coupons.title",
  order: 30,
  component: SalesRepCoupons,
  settings: [{ kind: "maxRows", default: COUPONS_DEFAULT_ROWS, min: MIN_ROWS, max: COUPONS_MAX_ROWS }],
};
