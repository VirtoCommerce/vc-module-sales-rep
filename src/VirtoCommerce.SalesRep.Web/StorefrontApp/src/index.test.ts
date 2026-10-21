import { beforeEach, describe, expect, it, vi } from "vitest";
import { CUSTOMER_PROFILE_LAYOUT_SCOPE } from "./constants";

const config = vi.hoisted(() => ({ analyticsEnabled: true }));
// Block ids init() registered, per scope.
const registered = vi.hoisted(() => new Map<string, string[]>());

vi.mock("./composables/useSalesRepsConfig", () => ({
  isSalesRepsEnabled: () => true,
  isSalesRepUser: () => true,
  isSalesRepTasksEnabled: () => false,
  isSalesRepAnalyticsEnabled: () => config.analyticsEnabled,
}));

// init() adds routes, merges its locales into the host's i18n and registers its blocks into the host's layout
// registry, so all three have to exist.
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  globals: {
    storeId: "test-store",
    cultureName: "en-US",
    router: { addRoute: vi.fn() },
    i18n: { global: { locale: "en", getLocaleMessage: () => ({}), setLocaleMessage: vi.fn() } },
  },
  registerBlock: (scope: string, block: { id: string }) => {
    registered.set(scope, [...(registered.get(scope) ?? []), block.id]);
  },
}));

// init() registers once per module instance (the MF loader re-invokes the entry on HMR), so each case loads a fresh
// one and reads what that instance registered.
async function profileBlocksAfterInit(): Promise<string[]> {
  vi.resetModules();
  registered.clear();
  const { init } = await import("./index");

  init();

  return registered.get(CUSTOMER_PROFILE_LAYOUT_SCOPE) ?? [];
}

beforeEach(() => {
  config.analyticsEnabled = true;
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
