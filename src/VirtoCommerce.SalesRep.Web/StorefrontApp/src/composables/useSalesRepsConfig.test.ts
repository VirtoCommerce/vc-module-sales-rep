import { beforeEach, describe, expect, it, vi } from "vitest";
import { ENABLED_KEY, GOOGLE_ANALYTICS_MODULE_ID, MODULE_ID, SALES_REP_ACCESS_PERMISSION } from "../constants";
import { isSalesRepAnalyticsEnabled, isSalesRepsEnabled, isSalesRepUser } from "./useSalesRepsConfig";

// The store's module list: an id is in it while that module is installed and enabled.
const { isEnabled, installedModules, useModuleSettings } = vi.hoisted(() => {
  const isEnabledFn = vi.fn();
  const installed = new Set<string>();
  return {
    isEnabled: isEnabledFn,
    installedModules: installed,
    useModuleSettings: vi.fn((moduleId: string) => ({
      isEnabled: isEnabledFn,
      hasModuleSettings: { value: installed.has(moduleId) },
    })),
  };
});
vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useModuleSettings,
  useUser: () => ({ checkPermissions }),
}));

const { checkPermissions } = vi.hoisted(() => ({ checkPermissions: vi.fn() }));

beforeEach(() => {
  installedModules.clear();
});

describe("useSalesRepsConfig", () => {
  it("is enabled only when the SalesRep.Enabled storefront setting is on", () => {
    isEnabled.mockReturnValue(true);
    expect(isSalesRepsEnabled()).toBe(true);
    expect(useModuleSettings).toHaveBeenCalledWith(MODULE_ID);
    expect(isEnabled).toHaveBeenCalledWith(ENABLED_KEY);

    isEnabled.mockReturnValue(false);
    expect(isSalesRepsEnabled()).toBe(false);
  });

  describe("isSalesRepUser", () => {
    it("requires both the store setting and the permission", () => {
      isEnabled.mockReturnValue(true);
      checkPermissions.mockReturnValue(true);

      expect(isSalesRepUser()).toBe(true);
      expect(checkPermissions).toHaveBeenCalledWith(SALES_REP_ACCESS_PERMISSION);
    });

    it("is false for a permitted user when the module is off for the store", () => {
      isEnabled.mockReturnValue(false);
      checkPermissions.mockReturnValue(true);

      expect(isSalesRepUser()).toBe(false);
    });

    it("is false for a user without the rep permission", () => {
      isEnabled.mockReturnValue(true);
      checkPermissions.mockReturnValue(false);

      expect(isSalesRepUser()).toBe(false);
    });

    it("does not evaluate the permission when the module is off", () => {
      isEnabled.mockReturnValue(false);
      checkPermissions.mockReset();

      isSalesRepUser();

      expect(checkPermissions).not.toHaveBeenCalled();
    });
  });

  describe("isSalesRepAnalyticsEnabled", () => {
    beforeEach(() => {
      isEnabled.mockReturnValue(true);
      checkPermissions.mockReturnValue(true);
    });

    it("is on for a rep while the analytics module is in the store's module list", () => {
      installedModules.add(GOOGLE_ANALYTICS_MODULE_ID);

      expect(isSalesRepAnalyticsEnabled()).toBe(true);
      expect(useModuleSettings).toHaveBeenCalledWith(GOOGLE_ANALYTICS_MODULE_ID);
    });

    // Not installed and disabled from the admin UI look the same here: the module is not in the list.
    it("is off when the analytics module is not in the list", () => {
      expect(isSalesRepAnalyticsEnabled()).toBe(false);
    });

    it("is off for a user who is not a rep", () => {
      installedModules.add(GOOGLE_ANALYTICS_MODULE_ID);
      checkPermissions.mockReturnValue(false);

      expect(isSalesRepAnalyticsEnabled()).toBe(false);
    });
  });
});
