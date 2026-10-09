import { definePluginManifest, settingEnabled, userCan } from "@vc-frontend/core/manifest";
import {
  ALL_CUSTOMER_ORDERS_ROUTE_NAME,
  ALL_CUSTOMER_ORDERS_ROUTE_SEGMENT,
  CUSTOMER_ORDER_ROUTE_NAME,
  CUSTOMER_ORDER_ROUTE_SEGMENT,
  CUSTOMER_ORDERS_ROUTE_NAME,
  CUSTOMER_ORDERS_ROUTE_SEGMENT,
  CUSTOMER_PROFILE_ROUTE_NAME,
  CUSTOMER_PROFILE_ROUTE_SEGMENT,
  CUSTOMER_SHARING_SCOPE,
  DASHBOARD_ROUTE_NAME,
  DASHBOARD_ROUTE_SEGMENT,
  DOCUMENTS_ROUTE_NAME,
  DOCUMENTS_ROUTE_SEGMENT,
  ENABLED_KEY,
  MODULE_ID,
  MY_CUSTOMERS_ROUTE_NAME,
  MY_CUSTOMERS_ROUTE_SEGMENT,
  NAV_LINK_ID,
  NAV_PRIORITY,
  ROUTE_NAME,
  ROUTE_SEGMENT,
  SALES_REP_ACCESS_PERMISSION,
  SALES_REP_DOCUMENTS_READ_PERMISSION,
  TASKS_ROUTE_NAME,
  TASKS_ROUTE_SEGMENT,
} from "./src/constants";

const isRep = userCan(SALES_REP_ACCESS_PERMISSION);
const hubRoute = (path: string, name: string) => ({ path, parent: "Company" as const, name, when: isRep });

// What the storefront learns before any of this plugin's code is fetched. init() still registers all of it.
//
// The hub's left-rail section is not declared: a declared entry renders its title key before this plugin's
// locales have merged, so the rail would show `sales_rep.hub.title` until they arrive.
export default definePluginManifest({
  when: settingEnabled(MODULE_ID, ENABLED_KEY),
  routes: [
    { path: ROUTE_SEGMENT, parent: "Company", name: ROUTE_NAME },
    hubRoute(DASHBOARD_ROUTE_SEGMENT, DASHBOARD_ROUTE_NAME),
    hubRoute(MY_CUSTOMERS_ROUTE_SEGMENT, MY_CUSTOMERS_ROUTE_NAME),
    hubRoute(CUSTOMER_PROFILE_ROUTE_SEGMENT, CUSTOMER_PROFILE_ROUTE_NAME),
    hubRoute(CUSTOMER_ORDERS_ROUTE_SEGMENT, CUSTOMER_ORDERS_ROUTE_NAME),
    hubRoute(CUSTOMER_ORDER_ROUTE_SEGMENT, CUSTOMER_ORDER_ROUTE_NAME),
    hubRoute(ALL_CUSTOMER_ORDERS_ROUTE_SEGMENT, ALL_CUSTOMER_ORDERS_ROUTE_NAME),
    hubRoute(TASKS_ROUTE_SEGMENT, TASKS_ROUTE_NAME),
    {
      path: DOCUMENTS_ROUTE_SEGMENT,
      parent: "Company",
      name: DOCUMENTS_ROUTE_NAME,
      when: userCan(SALES_REP_ACCESS_PERMISSION, SALES_REP_DOCUMENTS_READ_PERMISSION),
    },
  ],
  menu: [
    {
      surface: "header",
      group: "corporate",
      id: NAV_LINK_ID,
      title: "sales_rep.navigation.link",
      icon: "user-group",
      priority: NAV_PRIORITY,
      routeName: ROUTE_NAME,
    },
  ],
  slots: [
    {
      at: "sharedList/provenance-note",
      policy: "reserve",
      when: (field) => field("scope").eq(CUSTOMER_SHARING_SCOPE),
    },
  ],
});
