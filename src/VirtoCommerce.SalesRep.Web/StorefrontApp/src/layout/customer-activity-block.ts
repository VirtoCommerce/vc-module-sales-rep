import { defineAsyncComponent, markRaw } from "vue";
import type { SalesRepBlockType } from "../types/layout";

const CustomerActivity = markRaw(defineAsyncComponent(() => import("../components/customer-activity.vue")));

// Registered (via registerBlock in index.ts) only while vc-module-google-ecommerce-analytics is installed and
// enabled: the widget shows tracked behaviour, and without that module it would have only a date to show.
export const customerActivityBlock: SalesRepBlockType = {
  id: "customer_activity",
  region: "mainRight",
  titleKey: "sales_rep.activity.customer.title",
  order: 30,
  component: CustomerActivity,
};
