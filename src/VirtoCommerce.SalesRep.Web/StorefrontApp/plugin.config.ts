import { definePluginManifest, settingEnabled } from "@vc-frontend/core/manifest";
import {
  CUSTOMER_SHARING_SCOPE,
  ENABLED_KEY,
  NAV_LINK_ID,
  NAV_PRIORITY,
  ROUTE_NAME,
  ROUTE_SEGMENT,
} from "./src/constants";

// What the storefront learns before any of this plugin's code is fetched. init() still registers all of it.
//
// The hub's own routes are not declared: they clear the "Company" parent's `requiresOrganization` (a rep
// serves organizations they do not belong to), and a declared route's placeholder cannot, so a rep with no
// organization of their own would be sent to Account before the plugin arrives. Undeclared, the same deep
// link resolves once init() has registered the route. Their menu links need a declared route, so the hub's
// left-rail section arrives with the plugin as well.
export default definePluginManifest({
  when: settingEnabled(ENABLED_KEY),
  routes: [{ path: ROUTE_SEGMENT, parent: "Company", name: ROUTE_NAME }],
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
