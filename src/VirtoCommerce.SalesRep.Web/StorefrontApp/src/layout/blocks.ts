// The hub's own blocks: which widgets exist on each surface and which region each lives in; the saved
// document only adds order and hidden flags. The layout engine is the host's (`@vc-frontend/core`), so
// registering a block there is all a widget needs to join drag-and-drop, hiding and persistence.
import { defineAsyncComponent, markRaw } from "vue";
import { MIN_ROWS, registerBlock, statBlocks } from "@vc-frontend/core";
import {
  CUSTOMER_PROFILE_LAYOUT_SCOPE,
  DASHBOARD_LAYOUT_SCOPE,
  ORDERS_DEFAULT_ROWS,
  ORDERS_MAX_ROWS,
  TOP_SELLERS_DEFAULT_ROWS,
  TOP_SELLERS_MAX_ROWS,
} from "../constants";
import { CUSTOMER_PROFILE_STAT_CARDS, DASHBOARD_STAT_CARDS } from "./stat-cards";
import type { BlockSettingType, BlockType } from "@vc-frontend/core";

// `markRaw` keeps Vue from making the component definition reactive when it lands in layout state.
const SalesRepOrders = markRaw(defineAsyncComponent(() => import("../components/sales-rep-orders.vue")));
const TopSellers = markRaw(defineAsyncComponent(() => import("../components/top-sellers.vue")));
const CustomerProfileActions = markRaw(
  defineAsyncComponent(() => import("../components/customer-profile-actions.vue")),
);
const CustomerProfileInfo = markRaw(defineAsyncComponent(() => import("../components/customer-profile-info.vue")));
// Shown with or without the analytics module: orders and customer assignments are platform data, and the card
// leaves the tracked half out when the module is absent. The customer activity widget is registered on its own.
const MyActivity = markRaw(defineAsyncComponent(() => import("../components/my-activity.vue")));

// Both surfaces configure the same two list widgets; the row cap differs per widget, not per scope.
// Shared arrays rather than repeated literals — nothing mutates a registered block, only the arrays
// they sit in.
const ordersSettings: readonly BlockSettingType[] = [
  { kind: "maxRows", default: ORDERS_DEFAULT_ROWS, min: MIN_ROWS, max: ORDERS_MAX_ROWS },
  { kind: "ruleTabs" },
];
const topSellersSettings: readonly BlockSettingType[] = [
  { kind: "maxRows", default: TOP_SELLERS_DEFAULT_ROWS, min: MIN_ROWS, max: TOP_SELLERS_MAX_ROWS },
];

// The ids are persisted as `block.type` in every saved document, so they are load-bearing.
const ordersBlock: BlockType = {
  id: "orders",
  region: "mainLeft",
  titleKey: "sales_rep.orders.title",
  order: 10,
  component: SalesRepOrders,
  props: { filterable: true },
  settings: ordersSettings,
};
const topSellersBlock: BlockType = {
  id: "top_sellers",
  region: "mainLeft",
  titleKey: "sales_rep.top_sellers.title",
  order: 20,
  component: TopSellers,
  settings: topSellersSettings,
};

// The stat row renders its cards by key, not by component, so the stat blocks come straight from the
// card tables: order follows the table, a saved document overrides it.
export const dashboardBlocks: readonly BlockType[] = [
  ...statBlocks(DASHBOARD_STAT_CARDS),
  ordersBlock,
  topSellersBlock,
  {
    id: "my_activity",
    region: "mainRight",
    titleKey: "sales_rep.activity.my_activity.title",
    order: 10,
    component: MyActivity,
  },
];

export const customerProfileBlocks: readonly BlockType[] = [
  ...statBlocks(CUSTOMER_PROFILE_STAT_CARDS),
  ordersBlock,
  topSellersBlock,
  {
    id: "actions",
    region: "mainRight",
    titleKey: "sales_rep.communication.quick_actions.title",
    order: 10,
    component: CustomerProfileActions,
  },
  {
    id: "info",
    region: "mainRight",
    titleKey: "sales_rep.customer_profile.info.title",
    order: 20,
    component: CustomerProfileInfo,
  },
];

/** Synchronously in `init()`, so a surface's first render (its skeleton included) knows every block. */
export function registerHubBlocks(): void {
  for (const block of dashboardBlocks) {
    registerBlock(DASHBOARD_LAYOUT_SCOPE, block);
  }

  for (const block of customerProfileBlocks) {
    registerBlock(CUSTOMER_PROFILE_LAYOUT_SCOPE, block);
  }
}
