import "./styles.css";
import {
  EXTENSION_NAMES,
  globals,
  Logger,
  registerCacheTypePolicies,
  registerLocaleLoader,
  ROUTES,
  useExtensionRegistry,
  useNavigations,
  useUser,
  useWishlistSharingScopes,
} from "@vc-frontend/core";
import { computed, defineAsyncComponent } from "vue";
import { useSharedSalesRepCustomersCount } from "./composables/useSalesRepCustomersCount";
import { isSalesRepsEnabled, isSalesRepTasksEnabled, isSalesRepUser } from "./composables/useSalesRepsConfig";
import {
  TASKS_NAV_LINK_ID,
  TASKS_ROUTE_NAME,
  CUSTOMER_SHARING_SCOPE,
  DASHBOARD_LAYOUT_SCOPE,
  DASHBOARD_NAV_LINK_ID,
  DASHBOARD_ROUTE_NAME,
  DOCUMENTS_NAV_LINK_ID,
  DOCUMENTS_ROUTE_NAME,
  HUB_NAV_PRIORITY,
  HUB_SECTION_ID,
  MY_CUSTOMERS_NAV_LINK_ID,
  MY_CUSTOMERS_ROUTE_NAME,
  SALES_REP_ACCESS_PERMISSION,
  SALES_REP_DOCUMENTS_READ_PERMISSION,
} from "./constants";
import { layoutTypePolicies } from "./layout/cache-policies";
import { documentsBlock } from "./layout/documents-block";
import { registerBlock } from "./layout/registry";
import { tasksBlock } from "./layout/tasks-block";
import { salesRepMenuSchema } from "./menu";
import {
  tasksRoute,
  allCustomerOrdersRoute,
  customerOrderRoute,
  customerOrdersRoute,
  customerProfileRoute,
  dashboardRoute,
  documentsRoute,
  myCustomersRoute,
  salesRepsRoute,
} from "./routes";
import type { ExtendedMenuLinkType, I18n, ILanguage } from "@vc-frontend/core";

const FALLBACK_LOCALE = "en";

// The MF loader re-invokes the entry on HMR, and neither addRoute nor mergeMenuSchema is
// idempotent — mergeMenuSchema concatenates arrays, so a second call duplicates the link.
let registered = false;

/**
 * Merges this plugin's messages for one locale, plus the `en` fallback when the locale is not `en`.
 *
 * Merges into a FRESH object via `setLocaleMessage` — never `mergeLocaleMessage`, which mutates in
 * place: the host's base messages can be a frozen JSON-module namespace and mutating one throws. A
 * shallow spread is enough because every key of ours lives under the unique top-level `sales_rep`.
 */
async function mergeLocale(i18n: I18n, locale: string): Promise<void> {
  const load = async (name: string): Promise<Record<string, unknown>> => {
    try {
      const module = (await import(`./locales/${name}.json`)) as { default?: Record<string, unknown> };
      return module.default ?? (module as Record<string, unknown>);
    } catch (error) {
      Logger.error(`[sales-rep] locale "${name}" not found`, error);
      return {};
    }
  };

  const isFallback = locale === FALLBACK_LOCALE;
  const [fallbackMessages, messages] = await Promise.all([
    isFallback ? Promise.resolve<Record<string, unknown>>({}) : load(FALLBACK_LOCALE),
    load(locale),
  ]);

  const global = i18n.global;
  // The fallback first, so a key missing from the active locale still resolves.
  const targets: [string, Record<string, unknown>][] = isFallback
    ? [[FALLBACK_LOCALE, messages]]
    : [
        [FALLBACK_LOCALE, fallbackMessages],
        [locale, messages],
      ];

  for (const [target, incoming] of targets) {
    const existing = global.getLocaleMessage(target) as Record<string, unknown>;
    global.setLocaleMessage(target, { ...existing, ...incoming });
  }
}

/** Re-merges on every runtime locale switch, not only at init — the host re-runs each registered loader. */
function loadLocale(): void {
  registerLocaleLoader("module:sales-rep", (i18n: I18n, language: ILanguage) =>
    mergeLocale(i18n, language?.twoLetterLanguageName || FALLBACK_LOCALE),
  );

  const { locale } = globals.i18n.global;
  const current = typeof locale === "object" ? locale.value : locale;
  // i18n carries the full culture ("en-US"); the message files are two-letter, like the
  // `twoLetterLanguageName` the host's own loader passes.
  void mergeLocale(globals.i18n, current?.split("-")[0] || FALLBACK_LOCALE);
}

export function init(): void {
  if (registered || !isSalesRepsEnabled()) {
    return;
  }
  registered = true;

  const { router } = globals;

  // Relative routes -> mount under the Company parent (/company/sales-reps, /company/dashboard, /company/my-customers).
  router.addRoute(ROUTES.COMPANY.NAME, salesRepsRoute);
  router.addRoute(ROUTES.COMPANY.NAME, dashboardRoute);
  router.addRoute(ROUTES.COMPANY.NAME, myCustomersRoute);
  // Customer profile (VCST-5308) -> /company/my-customers/:organizationId.
  router.addRoute(ROUTES.COMPANY.NAME, customerProfileRoute);
  router.addRoute(ROUTES.COMPANY.NAME, customerOrdersRoute);
  router.addRoute(ROUTES.COMPANY.NAME, customerOrderRoute);
  router.addRoute(ROUTES.COMPANY.NAME, allCustomerOrdersRoute);
  // Document library (VCST-5730) -> /company/documents (its own beforeEnter checks documents:read).
  router.addRoute(ROUTES.COMPANY.NAME, documentsRoute);
  // Tasks (VCST-5732) -> /company/tasks (its own beforeEnter checks the tasks module is installed).
  router.addRoute(ROUTES.COMPANY.NAME, tasksRoute);

  const { mergeMenuSchema, registerAccountSection } = useNavigations();
  const { checkPermissions } = useUser();

  // The user is resolved before module init (app-runner sets it first) and permissions only change
  // with a re-login, so a one-shot check here is the module's registration seam for permission-gated
  // surfaces (VCST-5730): without documents:read the widget, the page route guard and the nav link
  // all stay invisible.
  const canReadDocuments = checkPermissions(SALES_REP_ACCESS_PERMISSION, SALES_REP_DOCUMENTS_READ_PERMISSION);
  const tasksEnabled = isSalesRepTasksEnabled();

  // Same one-shot registration seam as the documents widget, and the same caveat: while the tasks module is
  // absent the block is unknown to the layout registry, so a layout SAVED in that state drops its persisted
  // position/settings and the widget returns at its defaults once the module is back.
  if (tasksEnabled) {
    registerBlock(DASHBOARD_LAYOUT_SCOPE, tasksBlock);
  }

  if (canReadDocuments) {
    // Caveat: while the permission is absent the block is unknown to the layout registry, so a layout
    // SAVED in that state drops the block's persisted position/settings (reconcileLayout discards
    // unregistered types); when the permission returns, the widget comes back at its defaults.
    registerBlock(DASHBOARD_LAYOUT_SCOPE, documentsBlock);
  }

  // My customers links showing the total-customer count badge. Desktop needs its own
  // component for the sibling-route highlight; mobile only contributes the count, so the
  // host renders its own menu link with it.
  const { register, registerContribution } = useExtensionRegistry();
  register("accountMenu", MY_CUSTOMERS_NAV_LINK_ID, {
    component: defineAsyncComponent(() => import("./components/link-my-customers.vue")),
  });
  registerContribution("mobileMenu", MY_CUSTOMERS_NAV_LINK_ID, {
    use: useSharedSalesRepCustomersCount,
  });

  // "Sales reps" contact-info link for buyers (VCST-5409) — stays in the Corporate widget.
  mergeMenuSchema(salesRepMenuSchema);

  // Publishing a list to a customer (VCST-5332): core only learns that another sharing option exists.
  useWishlistSharingScopes().registerSharingScope({
    scope: CUSTOMER_SHARING_SCOPE,
    labelKey: "sales_rep.list_sharing.scope_label",
    statusKey: "sales_rep.list_sharing.status",
    icon: "user-plus",
    order: 30,
    supportsLink: true,
    shoppable: true,
    isAvailable: isSalesRepUser,
    element: defineAsyncComponent(() => import("./components/wishlist-customer-sharing.vue")),
  });

  // Gated on the scope alone: the viewer is the customer, not a rep.
  register("sharedList", EXTENSION_NAMES.sharedList.provenanceNote, {
    component: defineAsyncComponent(() => import("./components/wishlist-rep-provenance.vue")),
    // Compared as a plain string: this module owns the value, not core's generated enum.
    condition: (sharingSetting) => (sharingSetting?.scope as string | undefined) === CUSTOMER_SHARING_SCOPE,
  });

  // Tasks sits between My customers and the library, and disappears with the tasks module.
  const tasksNavLink: ExtendedMenuLinkType[] = tasksEnabled
    ? [
        {
          id: TASKS_NAV_LINK_ID,
          // The one "Tasks" label — shared by this nav link, the page H1 and the breadcrumb.
          title: "sales_rep.tasks.title",
          icon: "calendar",
          route: { name: TASKS_ROUTE_NAME },
        },
      ]
    : [];

  // The Documents link exists only for reps who may read the library — dropped from the section
  // outright (both menus render section children), the same one-shot gate as the widget above.
  const documentsNavLink: ExtendedMenuLinkType[] = canReadDocuments
    ? [
        {
          id: DOCUMENTS_NAV_LINK_ID,
          // The one "Document library" label — shared by this nav link, the page H1 and the widget.
          title: "sales_rep.documents.title",
          icon: "document-text",
          route: { name: DOCUMENTS_ROUTE_NAME },
        },
      ]
    : [];

  // "Sales Rep hub" left-rail widget — visible only when the user is a Sales Rep (VCST-5469).
  registerAccountSection({
    id: HUB_SECTION_ID,
    title: "sales_rep.hub.title",
    icon: "users",
    priority: HUB_NAV_PRIORITY,
    children: [
      {
        id: DASHBOARD_NAV_LINK_ID,
        title: "sales_rep.hub.dashboard.navigation.link",
        icon: "view-grid",
        route: { name: DASHBOARD_ROUTE_NAME },
      },
      {
        id: MY_CUSTOMERS_NAV_LINK_ID,
        title: "sales_rep.my_customers.navigation.link",
        icon: "users",
        route: { name: MY_CUSTOMERS_ROUTE_NAME },
      },
      ...tasksNavLink,
      ...documentsNavLink,
    ],
    isVisible: computed(() => isSalesRepsEnabled() && checkPermissions(SALES_REP_ACCESS_PERMISSION)),
  });

  // Layout regions and blocks carry ids that repeat across surfaces, so Apollo would normalize them
  // into entities shared by every scope. See layout/cache-policies.ts.
  registerCacheTypePolicies(layoutTypePolicies, { owner: "sales-rep" });

  loadLocale();
}
