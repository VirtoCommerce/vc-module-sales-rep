import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { BUYER_ORDER_ROUTE_NAME, CUSTOMER_ORDER_ROUTE_NAME } from "./constants";
import { formatTimeAgo, salesRepOrderRoute } from "./utils";
import type { SalesRepCustomerOrderRowType } from "./types";

// Pinned so the expectations don't depend on the runtime's default locale.
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: { cultureName: "en-US", currencyCode: "USD" },
}));

describe("salesRepOrderRoute", () => {
  const row = (isOwn: boolean): SalesRepCustomerOrderRowType => ({
    id: "o-1",
    number: "CO260821-00001",
    organizationId: "org-of-the-order",
    organizationName: "Contoso Bank",
    createdDate: "2026-08-21T00:00:00Z",
    status: "New",
    statusDisplayValue: "New",
    total: "$10.00",
    isOwn,
  });

  it("sends an order the rep placed to the buyer-facing page", () => {
    expect(salesRepOrderRoute(row(true), "org-in-the-url")).toEqual({
      name: BUYER_ORDER_ROUTE_NAME,
      params: { orderId: "o-1" },
    });
  });

  it("sends someone else's order to the read-only hub page", () => {
    expect(salesRepOrderRoute(row(false), "org-in-the-url")).toEqual({
      name: CUSTOMER_ORDER_ROUTE_NAME,
      params: { organizationId: "org-in-the-url", orderId: "o-1" },
    });
  });

  it("falls back to the order's own customer when the page has none", () => {
    expect(salesRepOrderRoute(row(false))).toEqual({
      name: CUSTOMER_ORDER_ROUTE_NAME,
      params: { organizationId: "org-of-the-order", orderId: "o-1" },
    });
  });
});

describe("formatTimeAgo", () => {
  const NOW = new Date(2026, 8, 15, 12, 0, 0);
  const ago = (seconds: number): string => new Date(NOW.getTime() - seconds * 1000).toISOString();

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("speaks in the past tense", () => {
    expect(formatTimeAgo(ago(3 * 3600))).toBe("3 hours ago");
  });

  // At a unit's threshold the row is already that unit.
  it("switches unit exactly at its threshold", () => {
    expect(formatTimeAgo(ago(60))).toBe("1 minute ago");
    expect(formatTimeAgo(ago(3600))).toBe("1 hour ago");
  });

  // Floored, not rounded up: 119 seconds is still one minute.
  it("rounds down within a unit", () => {
    expect(formatTimeAgo(ago(119))).toBe("1 minute ago");
  });

  it("picks the largest unit that fits", () => {
    expect(formatTimeAgo(ago(2 * 86400 + 5 * 3600))).toBe("2 days ago");
  });

  // 26 hours back from 1 AM is two calendar days back, not "yesterday".
  it("counts days by the calendar, not in 24-hour spans", () => {
    vi.setSystemTime(new Date(2026, 8, 15, 1, 0, 0));

    expect(formatTimeAgo(new Date(2026, 8, 13, 23, 0, 0).toISOString())).toBe("2 days ago");
  });

  it("calls the previous calendar day yesterday", () => {
    expect(formatTimeAgo(new Date(2026, 8, 14, 10, 0, 0).toISOString())).toBe("yesterday");
  });

  it("names anything under a minute without a number", () => {
    expect(formatTimeAgo(ago(30))).toBe("this minute");
  });

  // Clock skew between server and browser must not read as an event still to come.
  it("treats a timestamp slightly ahead of the clock as now", () => {
    expect(formatTimeAgo(ago(-90))).toBe("this minute");
  });

  // Counted back from now, so not a calendar word: 13 days ago is the week before last, not "last week".
  it("gives weeks, months and years as numbers", () => {
    expect(formatTimeAgo(ago(13 * 86400))).toBe("1 week ago");
    expect(formatTimeAgo(ago(56 * 86400))).toBe("1 month ago");
    expect(formatTimeAgo(ago(400 * 86400))).toBe("1 year ago");
  });

  // The bucket says which hour, not when within it.
  it("never puts an hour bucket closer than this hour", () => {
    expect(formatTimeAgo(ago(40 * 60), { precision: "hour" })).toBe("this hour");
    expect(formatTimeAgo(ago(70 * 60), { precision: "hour" })).toBe("1 hour ago");
  });

  it("measures against the clock it is given", () => {
    const later = new Date(NOW.getTime() + 2 * 3600 * 1000);

    expect(formatTimeAgo(ago(3600), { now: later })).toBe("3 hours ago");
  });
});
