import {
  DASHBOARD_ROUTE_NAME,
  HUB_HEADER_LINK_ID,
  HUB_HEADER_LINK_PRIORITY,
  NAV_PRIORITY,
  NAV_LINK_ID,
  ROUTE_NAME,
} from "./constants";
import type { MenuType } from "@vc-frontend/core";
import type { DeepPartial } from "utility-types";

// Injected into the Corporate section on both desktop and mobile. The Corporate widget is
// already gated on isCorporateMember by the host, so no extra visibility guard is needed.
const link = {
  id: NAV_LINK_ID,
  title: "sales_rep.navigation.link",
  icon: "user-group",
  route: { name: ROUTE_NAME },
  priority: NAV_PRIORITY,
};

export const salesRepMenuSchema: DeepPartial<MenuType> = {
  header: {
    desktop: { corporate: { children: [{ ...link }] } },
    mobile: { corporate: { children: [{ ...link }] } },
  },
};

const hubHeaderLink = {
  id: HUB_HEADER_LINK_ID,
  title: "sales_rep.hub.header_link",
  icon: "briefcase-business",
  route: { name: DASHBOARD_ROUTE_NAME },
  priority: HUB_HEADER_LINK_PRIORITY,
};

// The header icon row is not permission-filtered by the host, so init merges this only for reps.
export const salesRepHubHeaderMenuSchema: DeepPartial<MenuType> = {
  header: {
    desktop: { main: [{ ...hubHeaderLink }] },
    mobile: { main: [{ ...hubHeaderLink }] },
  },
};
