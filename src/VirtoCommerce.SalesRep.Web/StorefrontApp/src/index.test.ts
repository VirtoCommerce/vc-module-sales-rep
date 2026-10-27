import { beforeEach, describe, expect, it, vi } from "vitest";
import { CUSTOMER_PROFILE_LAYOUT_SCOPE, DASHBOARD_ROUTE_NAME, HUB_HEADER_LINK_ID } from "./constants";
import type { MenuType } from "@vc-frontend/core";
import type { DeepPartial } from "utility-types";

const config = vi.hoisted(() => ({ analyticsEnabled: true, isSalesRep: true }));
const mergeMenuSchema = vi.hoisted(() => vi.fn<(schema: DeepPartial<MenuType>) => void>());

vi.mock("./composables/useSalesRepsConfig", () => ({
  isSalesRepsEnabled: () => true,
  isSalesRepUser: () => true,
  isSalesRepTasksEnabled: () => false,
  isSalesRepAnalyticsEnabled: () => config.analyticsEnabled,
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
  useNavigations: () => ({ mergeMenuSchema, registerAccountSection: () => {} }),
  // One switch for every permission init() checks: a rep holds the hub's, a buyer none of them.
  useUser: () => ({ checkPermissions: () => config.isSalesRep }),
}));

// init() registers once per module instance (the MF loader re-invokes the entry on HMR), so each case loads a fresh
// one, and reads the layout registry that instance registered into.
async function profileBlocksAfterInit(): Promise<string[]> {
  vi.resetModules();
  const [{ init }, { getBlockRegistry }] = await Promise.all([import("./index"), import("./layout/registry")]);

  init();

  return getBlockRegistry(CUSTOMER_PROFILE_LAYOUT_SCOPE).map((block) => block.id);
}

// The links a fresh init() merged into the desktop header icon row and the mobile main menu.
async function headerRowLinksAfterInit() {
  vi.resetModules();
  const { init } = await import("./index");

  init();

  return mergeMenuSchema.mock.calls.flatMap(([schema]) => [
    ...(schema.header?.desktop?.main ?? []),
    ...(schema.header?.mobile?.main ?? []),
  ]);
}

beforeEach(() => {
  config.analyticsEnabled = true;
  config.isSalesRep = true;
  mergeMenuSchema.mockClear();
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

// The host does not filter the header icon row by permissions, so init() adds the hub link only for reps.
describe("plugin init: the Sales Rep Hub header link", () => {
  it("opens the hub dashboard from the header icon row, ahead of Bulk order, and the mobile menu", async () => {
    const links = await headerRowLinksAfterInit();

    expect(links).toHaveLength(2);
    for (const link of links) {
      expect(link).toMatchObject({ id: HUB_HEADER_LINK_ID, route: { name: DASHBOARD_ROUTE_NAME } });
      // Bulk order, the row's first host item, has priority 0.
      expect(link?.priority).toBeLessThan(0);
    }
  });

  it("is not added for a user without the Sales Rep role", async () => {
    config.isSalesRep = false;

    expect(await headerRowLinksAfterInit()).toEqual([]);
    // init() did run: the Corporate "Sales reps" link is merged for every user.
    expect(mergeMenuSchema).toHaveBeenCalled();
  });
});
