import { enableAutoUnmount, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createWrapperFactory } from "@vc-frontend/core/testing";
import ActivityRow from "./activity-row.vue";
import type { SalesRepActivityItemType } from "../types";

vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: { storeId: "test-store", cultureName: "en-US", currencyCode: "USD" },
}));

// No messages are loaded, so t() answers with its key; the named arguments ride along so a test can see them.
vi.mock("vue-i18n", async (importOriginal) => {
  const actual = await importOriginal<typeof import("vue-i18n")>();
  return {
    ...actual,
    useI18n: () => ({
      ...actual.useI18n(),
      t: (key: string, named?: unknown) => (typeof named === "object" ? `${key} ${JSON.stringify(named)}` : key),
    }),
  };
});

// The rows share one clock; unmounting stops it, so each test starts its own.
enableAutoUnmount(afterEach);

afterEach(() => {
  vi.useRealTimers();
});

function item(overrides: Partial<SalesRepActivityItemType> = {}): SalesRepActivityItemType {
  return {
    category: "orders",
    type: "orderPlaced",
    occurredAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
    precision: "exact",
    count: 1,
    organizationId: "org-1",
    organizationName: "Acme",
    orderId: "ord1",
    orderNumber: "CU0001",
    status: "New",
    statusDisplayValue: "New",
    orderTotal: "$100.00",
    searchTerm: "",
    productId: "",
    productCode: "",
    productName: "",
    ...overrides,
  };
}

function productView(overrides: Partial<SalesRepActivityItemType> = {}): SalesRepActivityItemType {
  return item({
    category: "productViews",
    type: "productView",
    precision: "hour",
    productId: "p1",
    productName: "Gloves",
    productCode: "SKU-1",
    ...overrides,
  });
}

// Props-exposing stub so the link targets can be asserted, not just their presence.
const VcLinkStub = { name: "VcLinkStub", props: ["to"], template: "<a><slot /></a>" };
const findLink = (wrapper: ReturnType<typeof createWrapper>) => wrapper.findComponent({ name: "VcLinkStub" });

const createWrapper = createWrapperFactory(mount, ActivityRow, {
  global: {
    renderStubDefaultSlot: false,
    stubs: {
      VcIcon: true,
      VcLink: VcLinkStub,
      OrderStatus: true,
    },
  },
});

const timeText = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.get('[data-test-id="activity-row-time-label"]').text();
const findCount = (wrapper: ReturnType<typeof createWrapper>) =>
  wrapper.find('[data-test-id="activity-row-count-label"]');

describe("ActivityRow honest timestamps", () => {
  // GA hour-buckets must never read as an exact moment (contract): compact rows carry the "~" marker…
  it("prefixes the relative time with ~ for an hour-precision compact row", () => {
    const wrapper = createWrapper({ props: { item: item({ precision: "hour" }), compact: true } });

    expect(timeText(wrapper).startsWith("~")).toBe(true);
  });

  it("keeps the plain relative time for an exact compact row", () => {
    const wrapper = createWrapper({ props: { item: item(), compact: true } });

    expect(timeText(wrapper).startsWith("~")).toBe(false);
  });

  it("puts a bucket of the current hour at this hour, not at minutes ago", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 8, 15, 14, 40, 0));

    const occurredAt = new Date(2026, 8, 15, 14, 0, 0).toISOString();
    const wrapper = createWrapper({ props: { item: item({ precision: "hour", occurredAt }), compact: true } });

    expect(timeText(wrapper)).toBe("~this hour");
  });

  it("ages the relative time while the row stays on screen", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 8, 15, 14, 0, 0));

    const occurredAt = new Date(2026, 8, 15, 13, 50, 0).toISOString();
    const wrapper = createWrapper({ props: { item: item({ occurredAt }), compact: true } });
    expect(timeText(wrapper)).toBe("10 minutes ago");

    await vi.advanceTimersByTimeAsync(60 * 60 * 1000);

    expect(timeText(wrapper)).toBe("1 hour ago");
  });

  // …and full rows use the "during the hour of" phrasing instead of a fake exact timestamp.
  it("uses the during-hour phrasing for an hour-precision full row, with the bucket's hour", () => {
    const occurredAt = new Date(2026, 8, 15, 14, 0, 0).toISOString();
    const wrapper = createWrapper({
      props: { item: item({ category: "logins", type: "login", precision: "hour", count: 3, occurredAt }) },
    });

    expect(timeText(wrapper)).toContain("sales_rep.activity.time.during_hour");
    expect(timeText(wrapper)).toMatch(/"time":"2:00\sPM"/);
  });
});

describe("ActivityRow per-type rendering", () => {
  it("links an order row to the order page", () => {
    const wrapper = createWrapper({ props: { item: item() } });
    const link = findLink(wrapper);

    expect(link.props("to")).toEqual({ name: "OrderDetails", params: { orderId: "ord1" } });
    expect(link.text()).toBe("#CU0001");
  });

  // Search-term rows deep-link to the catalog search results exactly as the header search navigates.
  it("links a search row to the catalog search results for that term", () => {
    const wrapper = createWrapper({
      props: { item: item({ category: "searches", type: "search", precision: "hour", searchTerm: "gloves" }) },
    });
    const link = findLink(wrapper);

    expect(link.props("to")).toEqual({ name: "Search", query: { q: "gloves" } });
    // A new tab, as a product row opens: following a term must not cost the rep the feed they were reading.
    expect(link.attributes("target")).toBe("_blank");
    expect(link.attributes("rel")).toBe("noopener noreferrer");
  });

  // The quote marks differ per language, so they come with the message.
  it("quotes the search term through the locale", () => {
    const wrapper = createWrapper({
      props: { item: item({ category: "searches", type: "search", precision: "hour", searchTerm: "gloves" }) },
    });

    expect(findLink(wrapper).text()).toBe('sales_rep.activity.quoted_term {"term":"gloves"}');
  });

  // By product id, never a slug: /product/{id} always resolves, whereas the tracked SEO segment alone
  // is not a valid catalog URL (VCST-5337).
  it("links a resolved product view by its id and shows the bucket size", () => {
    const wrapper = createWrapper({ props: { item: productView({ count: 4 }) } });
    const link = findLink(wrapper);

    expect(link.props("to")).toEqual({ name: "Product", params: { productId: "p1" } });
    expect(findCount(wrapper).text()).toBe('sales_rep.activity.rows.count_suffix {"count":"4"}');
  });

  it("formats the bucket size in the active culture", () => {
    const wrapper = createWrapper({ props: { item: productView({ count: 1234 }) } });

    expect(findCount(wrapper).text()).toContain('"count":"1,234"');
  });

  it("shows no bucket size for a single view", () => {
    const wrapper = createWrapper({ props: { item: productView({ count: 1 }) } });

    expect(findCount(wrapper).exists()).toBe(false);
  });

  // A sign-in row says "3 sign-ins" in words, so it repeats no count.
  it("shows no bucket size beside a sign-in row", () => {
    const wrapper = createWrapper({
      props: { item: item({ category: "logins", type: "login", precision: "hour", count: 3 }) },
    });

    expect(findCount(wrapper).exists()).toBe(false);
  });

  // Unresolvable codes still identify the product — as plain text, since there is nowhere to link.
  it("renders an unresolved product view as plain text", () => {
    const wrapper = createWrapper({
      props: { item: item({ category: "productViews", type: "productView", productCode: "GONE-1" }) },
    });

    expect(findLink(wrapper).exists()).toBe(false);
    expect(wrapper.text()).toContain("GONE-1");
  });

  // A category the backend adds later still renders as a dated row instead of a blank.
  it("falls back to the generic wording for an unknown type", () => {
    const wrapper = createWrapper({ props: { item: item({ category: "webinars", type: "webinarAttended" }) } });

    expect(wrapper.text()).toContain("sales_rep.activity.rows.unknown");
  });
});
