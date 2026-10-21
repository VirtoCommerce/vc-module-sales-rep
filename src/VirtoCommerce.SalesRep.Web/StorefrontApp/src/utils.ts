import { useMemoize } from "@vueuse/core";
import { ContentType, formatStatCount, formatStatMoney, globals, QueryParamName, ROUTES } from "@vc-frontend/core";
import { BUYER_ORDER_ROUTE_NAME, CUSTOMER_ORDER_ROUTE_NAME } from "./constants";
import type { MoneyType, SalesRepCustomerOrdersQuery, SalesRepOrdersQuery } from "./api/graphql/types";
import type {
  SalesRepActivityItemType,
  SalesRepCustomerOrderRowType,
  SalesRepDocumentType,
  SalesRepOrderRowType,
  SalesRepFacetOptionType,
  SalesRepRuleType,
} from "./types";
import type { ComposerTranslation } from "vue-i18n";
import type { RouteLocationRaw } from "vue-router";

// Selectable filter options exclude the backend "all" rule (chips already prepend a synthetic "All" baseline).
export function selectableFilterRules(rules: SalesRepRuleType[]): SalesRepRuleType[] {
  return rules.filter((rule) => rule.name.toLowerCase() !== "all");
}

// Backend leaves address formatting to the storefront; single source of that format so both surfaces
// render locations consistently.
type LocationPartsType =
  | { postalCode?: string | null; zip?: string | null; city?: string | null; regionName?: string | null }
  | null
  | undefined;

export function formatCustomerLocation(address: LocationPartsType, options?: { withPostalCode?: boolean }): string {
  if (!options?.withPostalCode) {
    // Profile "ship to": "City, Region".
    return [address?.city, address?.regionName].filter(Boolean).join(", ");
  }

  // `postalCode` is the canonical member-address field; `zip` is a legacy alias kept as a fallback.
  const postalCode = address?.postalCode || address?.zip;
  // List rows: postal code (prefixed "#"), city, region — middot-separated (e.g. "#23220 · Richmond · Virginia").
  const code = postalCode ? `#${postalCode}` : "";
  return [code, address?.city, address?.regionName].filter(Boolean).join(" · ");
}

// Local-time counterpart of Date.UTC: the instant at a wall-clock moment in the user's zone, with the same
// over/underflow normalization (month − 1 === −1 → prev Dec; day − n <= 0 → prev month).
export const local = (year: number, month: number, day: number, hours = 0, minutes = 0, seconds = 0, ms = 0): number =>
  new Date(year, month, day, hours, minutes, seconds, ms).getTime();

// End of a day on the user's clock — the inclusive upper bound every "to" window uses.
export const eod = (year: number, month: number, day: number): number => local(year, month, day, 23, 59, 59, 999);

// Every bound leaves here as a UTC instant — only *where* the user's day starts differs from UTC's.
export const iso = (ms: number): string => new Date(ms).toISOString();

// Icon per activity category (canonical Lucide names); unknown categories get a neutral mark so a
// backend-added category renders instead of breaking the row.
const ACTIVITY_CATEGORY_ICONS: Readonly<Record<string, string>> = {
  orders: "file-text",
  customers: "user-plus",
  searches: "search",
  productViews: "eye",
  logins: "log-in",
};

export function activityCategoryIcon(category: string): string {
  return ACTIVITY_CATEGORY_ICONS[category] ?? "activity";
}

// A row's identity: an order by its id, an analytics row by its hour, its customer and what it names.
export function activityKey(item: SalesRepActivityItemType): string {
  return [
    item.category,
    item.occurredAt,
    item.organizationId,
    item.orderId,
    item.searchTerm,
    item.productCode,
    item.productName,
  ].join("|");
}

// Relative "time ago" for the compact activity rows, in the active culture. Coarse on purpose:
// analytics rows are hour-buckets, so anything finer than minutes would imply precision they lack.
const TIME_AGO_UNITS: readonly { unit: Intl.RelativeTimeFormatUnit; seconds: number }[] = [
  { unit: "year", seconds: 31536000 },
  { unit: "month", seconds: 2592000 },
  { unit: "week", seconds: 604800 },
  { unit: "day", seconds: 86400 },
  { unit: "hour", seconds: 3600 },
  { unit: "minute", seconds: 60 },
];

// An hour bucket says which hour, not when within it, so it is never put closer than "this hour".
const HOUR_BUCKET_UNITS = TIME_AGO_UNITS.filter(({ unit }) => unit !== "minute");

// Intl formatters cost more to construct than to use, and this runs once per rendered row — up to a full page
// of them on every tab switch. One per culture and mode, kept for the life of the tab.
const timeAgoFormatterFor = useMemoize(
  (cultureName: string, numeric: Intl.RelativeTimeFormatNumeric) =>
    new Intl.RelativeTimeFormat(cultureName, { numeric }),
);

const MS_PER_DAY = 86400000;

// Calendar days between two instants on the user's clock. Rounded, so the 23- and 25-hour days of a DST switch
// still count as one.
function calendarDaysBetween(earlier: Date, later: Date): number {
  const dayStart = (date: Date) => local(date.getFullYear(), date.getMonth(), date.getDate());
  return Math.round((dayStart(later) - dayStart(earlier)) / MS_PER_DAY);
}

type TimeAgoOptionsType = {
  // The clock to measure against, for a caller that re-renders on its own tick; the current time by default.
  now?: Date;
  precision?: SalesRepActivityItemType["precision"];
};

export function formatTimeAgo(isoDate: string, options: TimeAgoOptionsType = {}): string {
  const occurredAt = new Date(isoDate);
  const now = options.now ?? new Date();
  const elapsedSeconds = Math.max(0, Math.round((now.getTime() - occurredAt.getTime()) / 1000));
  const isHourBucket = options.precision === "hour";

  const match = (isHourBucket ? HOUR_BUCKET_UNITS : TIME_AGO_UNITS).find(({ seconds }) => elapsedSeconds >= seconds);
  if (!match) {
    // Under the smallest unit: "this minute", or "this hour" for a bucket — wording from numeric: "auto".
    return timeAgoFormatterFor(globals.cultureName, "auto").format(0, isHourBucket ? "hour" : "minute");
  }

  // Days by the calendar, as "yesterday" reads: 26 hours back from 1 AM is the day before yesterday.
  if (match.unit === "day") {
    return timeAgoFormatterFor(globals.cultureName, "auto").format(-calendarDaysBetween(occurredAt, now), "day");
  }

  // The other units count back from now, not by the calendar, so they read as numbers: numeric "auto" calls 13 days
  // ago "last week", which is the week before last.
  const amount = -Math.floor(elapsedSeconds / match.seconds);
  return timeAgoFormatterFor(globals.cultureName, "always").format(amount, match.unit);
}

// The catalog search results page for a tracked term, exactly as the header search navigates (VCST-5731).
export function searchResultsRoute(term: string): RouteLocationRaw {
  return { name: ROUTES.SEARCH.NAME, query: { [QueryParamName.SearchPhrase]: term } };
}

// Document library display helpers.

// File-type badge: the extension is the most precise source (tells DOCX from DOC), so it wins over content-type.
const CONTENT_TYPE_BADGES: Record<string, string> = {
  "application/pdf": "PDF",
  "application/msword": "DOC",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document": "DOCX",
  "application/vnd.ms-excel": "XLS",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": "XLSX",
  "application/vnd.ms-powerpoint": "PPT",
  "application/vnd.openxmlformats-officedocument.presentationml.presentation": "PPTX",
  "application/zip": "ZIP",
  "image/jpeg": "JPG",
  "image/png": "PNG",
  "text/plain": "TXT",
  "text/csv": "CSV",
};

const FILE_EXTENSION_RE = /\.([a-z\d]+)$/i;

export function documentTypeLabel(name: string, contentType?: string | null): string {
  const extension = FILE_EXTENSION_RE.exec(name)?.[1];
  if (extension) {
    return extension.toUpperCase();
  }

  const type = contentType?.toLowerCase() ?? "";
  return CONTENT_TYPE_BADGES[type] ?? type.split("/")[1]?.toUpperCase() ?? "";
}

// Mirrors VcFile's icon mapping (ui-kit vc-file.vue); unknown types get the generic file icon.
const CONTENT_TYPE_KEYS = new Set<string>(Object.keys(ContentType));

export function documentIcon(contentType?: string | null): string {
  const known = CONTENT_TYPE_KEYS.has(contentType as ContentType) ? ContentType[contentType as ContentType] : undefined;
  return `file-${known ? known.replace("/", "-") : "file"}.svg`;
}

// "Published May 22 · 96 pages" (date only when no page count).
export function documentMeta(
  document: SalesRepDocumentType,
  t: ComposerTranslation,
  d: (value: number | Date | string, format: string) => string,
): string {
  return [
    t("sales_rep.documents.published", { date: d(document.createdDate, "short") }),
    document.pageCount
      ? t("sales_rep.documents.details.pages_count", { count: formatStatCount(document.pageCount) }, document.pageCount)
      : "",
  ]
    .filter(Boolean)
    .join(" · ");
}

// Connection items → table rows, shared by the orders widget and the customer orders page.
type OrderNodeType = NonNullable<NonNullable<SalesRepOrdersQuery["salesRepOrders"]>["items"]>[number];

type OrderRowSourceType = {
  id: string;
  number?: string;
  organizationId?: string;
  organizationName?: string;
  createdDate: string;
  status?: string;
  statusDisplayValue?: string;
  total?: Pick<MoneyType, "formattedAmount"> | null;
};

function toOrderRowBase(order: OrderRowSourceType) {
  return {
    id: order.id,
    number: order.number ?? "",
    organizationId: order.organizationId ?? "",
    organizationName: order.organizationName ?? "",
    createdDate: order.createdDate,
    status: order.status ?? "",
    statusDisplayValue: order.statusDisplayValue ?? "",
    total: formatStatMoney(order.total),
  };
}

function presentOrders<T>(items?: (T | null)[]): NonNullable<T>[] {
  return (items ?? []).filter((order): order is NonNullable<T> => order != null);
}

export function toSalesRepOrderRows(items?: OrderNodeType[]): SalesRepOrderRowType[] {
  return presentOrders(items).map((order) => ({
    ...toOrderRowBase(order),
    itemsCount: formatStatCount(order.itemsCount),
  }));
}

type CustomerOrderNodeType = NonNullable<
  NonNullable<SalesRepCustomerOrdersQuery["salesRepCustomerOrders"]>["items"]
>[number];

export function toSalesRepCustomerOrderRows(items?: CustomerOrderNodeType[]): SalesRepCustomerOrderRowType[] {
  return presentOrders(items).map((order) => ({
    ...toOrderRowBase(order),
    // A rep-placed order records the rep as its customer — the field the backend scopes own-orders by.
    isOwn: Boolean(globals.userId) && order.customerId === globals.userId,
  }));
}

export function toFacetOptions(
  facets: NonNullable<SalesRepCustomerOrdersQuery["salesRepCustomerOrders"]>["term_facets"] | undefined,
  facetName: string,
): SalesRepFacetOptionType[] {
  return (facets ?? [])
    .filter((facet) => facet?.name === facetName)
    .flatMap((facet) => facet.terms ?? [])
    .filter((term) => term != null)
    .map((term) => ({ name: term.term, label: term.label || term.term, count: term.count }));
}

// An order the rep placed opens on the buyer-facing page, where they can still act on it; everyone else's
// opens read-only in the hub.
export function salesRepOrderRoute(order: SalesRepCustomerOrderRowType, organizationId?: string): RouteLocationRaw {
  if (order.isOwn) {
    return { name: BUYER_ORDER_ROUTE_NAME, params: { orderId: order.id } };
  }

  return {
    name: CUSTOMER_ORDER_ROUTE_NAME,
    params: { organizationId: organizationId ?? order.organizationId, orderId: order.id },
  };
}
