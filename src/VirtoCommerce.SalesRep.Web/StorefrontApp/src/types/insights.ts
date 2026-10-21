import type { RouteLocationRaw } from "vue-router";

// View models for the customer insights panels. Counts stay numbers: vue-i18n needs them to pick plural forms.

export type SalesRepSearchTermRowType = {
  term: string;
  count: number;
  // GA reports aggregate by hour bucket, not event timestamp — render as a date, never as a time.
  lastSearchedDate?: string;
};

export type SalesRepBrowsedProductRowType = {
  // Empty unless the backend resolved GA's product code to a real product; gates the deep link.
  productId: string;
  name: string;
  sku: string;
  imageUrl: string;
  viewCount: number;
  lastViewedDate?: string;
};

// One line of the Activities page's Top list, whichever collection it ranks. The count arrives worded.
export type SalesRepRankedRowType = {
  key: string;
  label: string;
  // Absent for an unresolved product code, which has nowhere to link.
  to?: RouteLocationRaw;
  count: string;
};
