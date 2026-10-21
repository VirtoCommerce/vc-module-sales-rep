import type { ReportingMonthSlotType, ReportingPeriodTotalsType, ReportingRawMonthType } from "./reporting";

/**
 * Example figures for the Reporting page, for demos and design reviews on stores without order history.
 * Off unless `APP_SALES_REP_REPORTING_MOCK=true` is set at build time (`.env.local`); the page then says
 * "Example data". Deterministic per customer and month, so a reload or a period switch shows the same numbers.
 */
export function isReportingMockEnabled(): boolean {
  return import.meta.env.APP_SALES_REP_REPORTING_MOCK === "true";
}

const BASE_MONTHLY_REVENUE = 68_000;
const BASE_ORDER_VALUE = 425;
// Share of a month's orders (and revenue) that ends up cancelled; the page subtracts it.
const CANCELLED_SHARE = 0.03;
const LAST_YEAR_FACTOR = 0.94;
// Jan…Dec: B2B buying dips in winter and peaks before the holidays.
const SEASONALITY = [0.88, 0.95, 0.97, 1.0, 1.0, 1.04, 1.03, 1.06, 1.1, 1.08, 1.12, 0.96];
// One month of last year (March) has no sales at any customer, so the page shows how a missing baseline reads.
const EMPTY_LAST_YEAR_MONTH = 2;

function hash(text: string): number {
  let value = 2166136261;
  for (let i = 0; i < text.length; i++) {
    value ^= text.charCodeAt(i);
    value = Math.imul(value, 16777619);
  }
  return value >>> 0;
}

/** 0–1, stable for the same key. */
function noise(key: string): number {
  return (hash(key) % 10_000) / 10_000;
}

function monthTotals(
  organizationId: string,
  year: number,
  month: number,
  scale: number,
  emptyYear: number,
): ReportingPeriodTotalsType {
  if (year === emptyYear && month === EMPTY_LAST_YEAR_MONTH) {
    return { amount: 0, count: 0 };
  }
  const key = `${organizationId || "all"}:${year}:${month}`;
  const customerScale = organizationId ? 0.25 + noise(organizationId) * 0.35 : 1;
  // ±12% month-to-month swing, so the YoY badges come out both green and red.
  const swing = 0.88 + noise(key) * 0.24;
  const amount = Math.round(BASE_MONTHLY_REVENUE * customerScale * SEASONALITY[month] * swing * scale);
  const orderValue = BASE_ORDER_VALUE * (0.92 + noise(`${key}:aov`) * 0.16);
  return { amount, count: Math.max(1, Math.round(amount / orderValue)) };
}

function withCancelled(totals: ReportingPeriodTotalsType): {
  all: ReportingPeriodTotalsType;
  cancelled: ReportingPeriodTotalsType;
} {
  const cancelled = {
    amount: Math.round(totals.amount * CANCELLED_SHARE),
    count: Math.round(totals.count * CANCELLED_SHARE),
  };
  return {
    all: { amount: totals.amount + cancelled.amount, count: totals.count + cancelled.count },
    cancelled,
  };
}

/** Raw figures in the shape the statistics query returns, for each month slot and the month before the first. */
export function buildReportingMock(
  months: ReportingMonthSlotType[],
  organizationId: string,
): { raw: ReportingRawMonthType[]; before: ReportingRawMonthType } {
  // "Last year" relative to the period shown, not to today, so the figures don't depend on when the page is opened.
  const thisYear = (months.find((slot) => slot.state === "current") ?? months.at(-1))?.year ?? new Date().getFullYear();
  const emptyYear = thisYear - 1;

  const raw = months.map((slot) => {
    if (slot.state === "upcoming") {
      return {};
    }
    const toDate = slot.daysPassed / slot.daysInMonth;
    const current = withCancelled(monthTotals(organizationId, slot.year, slot.month, toDate, emptyYear));
    const lastYear = withCancelled(monthTotals(organizationId, slot.year - 1, slot.month, LAST_YEAR_FACTOR, emptyYear));
    return {
      all: current.all,
      cancelled: current.cancelled,
      lastYear: lastYear.all,
      lastYearCancelled: lastYear.cancelled,
    };
  });

  const first = months[0];
  const previous = first ? new Date(first.year, first.month - 1, 1) : undefined;
  const before = previous
    ? withCancelled(monthTotals(organizationId, previous.getFullYear(), previous.getMonth(), 1, emptyYear))
    : undefined;

  return { raw, before: before ? { all: before.all, cancelled: before.cancelled } : {} };
}
