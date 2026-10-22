import { beforeEach, describe, expect, it, vi } from "vitest";
import { CUSTOMER_PROFILE_LAYOUT_SCOPE } from "./constants";

const config = vi.hoisted(() => ({ analyticsEnabled: true, couponsEnabled: true }));

vi.mock("./composables/useSalesRepsConfig", () => ({
  isSalesRepsEnabled: () => true,
  isSalesRepUser: () => true,
  isSalesRepTasksEnabled: () => false,
  isSalesRepAnalyticsEnabled: () => config.analyticsEnabled,
  isSalesRepCouponsEnabled: () => config.couponsEnabled,
}));

// init() adds routes and merges its locales into the host's i18n, so both have to exist.
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: {
    storeId: "test-store",
    cultureName: "en-US",
    router: { addRoute: vi.fn() },
    i18n: { global: { locale: "en", getLocaleMessage: () => ({}), setLocaleMessage: vi.fn() } },
  },
}));

// init() registers once per module instance (the MF loader re-invokes the entry on HMR), so each case loads a fresh
// one, and reads the layout registry that instance registered into.
async function profileBlocksAfterInit(): Promise<string[]> {
  vi.resetModules();
  const [{ init }, { getBlockRegistry }] = await Promise.all([import("./index"), import("./layout/registry")]);

  init();

  return getBlockRegistry(CUSTOMER_PROFILE_LAYOUT_SCOPE).map((block) => block.id);
}

beforeEach(() => {
  config.analyticsEnabled = true;
  config.couponsEnabled = true;
});

// The customer activity widget shows tracked behaviour: it exists only while the analytics module does.
describe("plugin init: the customer activity widget", () => {
  it("is on the customer profile while the analytics module is installed and enabled", async () => {
    expect(await profileBlocksAfterInit()).toContain("customer_activity");
  });

  it("is not on the customer profile without the analytics module", async () => {
    config.analyticsEnabled = false;

    const blocks = await profileBlocksAfterInit();

    expect(blocks).not.toContain("customer_activity");
    expect(blocks.length).toBeGreaterThan(0);
  });
});

// The coupons widget (VCST-6158) reads vc-module-marketing: it exists only while that module does.
describe("plugin init: the coupons widget", () => {
  it("is on the customer profile while the marketing module is installed", async () => {
    expect(await profileBlocksAfterInit()).toContain("coupons");
  });

  it("is not on the customer profile without the marketing module", async () => {
    config.couponsEnabled = false;

    const blocks = await profileBlocksAfterInit();

    expect(blocks).not.toContain("coupons");
    expect(blocks.length).toBeGreaterThan(0);
  });
});
