import { afterEach, describe, expect, it, vi } from "vitest";
import { DASHBOARD_ROUTE_NAME } from "./constants";
import {
  activitiesRoute,
  allCustomerOrdersRoute,
  customerActivitiesRoute,
  customerOrderRoute,
  customerOrdersRoute,
  customerProfileRoute,
  dashboardRoute,
  documentsRoute,
  isMyCustomersArea,
  myCustomersRoute,
  reportingRoute,
  salesRepsRoute,
  tasksRoute,
} from "./routes";
import type {
  NavigationGuard,
  RouteLocationNormalized,
  RouteLocationNormalizedLoaded,
  RouteRecordRaw,
} from "vue-router";

const access = vi.hoisted(() => ({ isRep: true, tasksEnabled: true, analyticsEnabled: true }));

vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useUser: () => ({ checkPermissions: () => access.isRep }),
}));
vi.mock("./composables/useSalesRepsConfig", () => ({
  isSalesRepsEnabled: () => true,
  isSalesRepTasksEnabled: () => access.tasksEnabled,
  // A rep's check, as the real one is: a non-rep never has the analytics surfaces.
  isSalesRepAnalyticsEnabled: () => access.isRep && access.analyticsEnabled,
}));

const to = { params: { organizationId: "org-1", orderId: "o-1" } } as unknown as RouteLocationNormalized;

function runGuard(route: RouteRecordRaw) {
  const next = vi.fn();
  (route.beforeEnter as NavigationGuard)(to, to, next);
  return next;
}

afterEach(() => {
  access.isRep = true;
  access.tasksEnabled = true;
  access.analyticsEnabled = true;
});

const at = (name: string) => ({ name, query: {} }) as unknown as RouteLocationNormalizedLoaded;

// The rep-facing hub pages mount under the org-gated "/company" parent but must stay reachable for a
// sales rep with zero org memberships (their access is `sales-rep:access`, not org membership). They
// clear the inherited `requiresOrganization` gate; the buyer-facing "Sales reps" page keeps it. VCST-5494.
describe("sales-rep routes", () => {
  it.each([
    ["dashboard", dashboardRoute],
    ["reporting", reportingRoute],
    ["my customers", myCustomersRoute],
    ["customer profile", customerProfileRoute],
    ["customer orders", customerOrdersRoute],
    ["customer order", customerOrderRoute],
    ["all customer orders", allCustomerOrdersRoute],
    ["documents", documentsRoute],
    ["activities", activitiesRoute],
    ["customer activities", customerActivitiesRoute],
  ])("clears requiresOrganization on the rep-facing %s route", (_name, route) => {
    expect(route.meta?.requiresOrganization).toBe(false);
  });

  // One page, two routes: the rep's own feed takes no customer, and a customer's sits under their profile, as their
  // orders do, with the id from the path.
  it("serves the rep's feed and one customer's from the same page", () => {
    expect(customerActivitiesRoute.component).toBe(activitiesRoute.component);
    expect(activitiesRoute.props).toBeUndefined();
    expect(customerActivitiesRoute.path).toBe("my-customers/:organizationId/activities");
    expect(customerActivitiesRoute.props).toBe(true);
  });

  it("nests the customer orders list under the customer", () => {
    expect(customerOrdersRoute.path).toBe("my-customers/:organizationId/orders");
  });

  it("nests one order under the customer's order list", () => {
    expect(customerOrderRoute.path).toBe("my-customers/:organizationId/orders/:orderId");
  });

  // A sibling of My customers, since "my-customers/orders" would match the :organizationId segment.
  it("keeps the all-customers order list out of the customer path", () => {
    expect(allCustomerOrdersRoute.path).toBe("customer-orders");
  });

  // guardSalesRep only calls next() to bounce a non-rep, so a beforeEnter that forgets to call it on the way
  // through leaves the click doing nothing at all.
  it.each([
    ["dashboard", dashboardRoute],
    ["reporting", reportingRoute],
    ["my customers", myCustomersRoute],
    ["customer profile", customerProfileRoute],
    ["customer orders", customerOrdersRoute],
    ["customer order", customerOrderRoute],
    ["all customer orders", allCustomerOrdersRoute],
    ["documents", documentsRoute],
    ["tasks", tasksRoute],
    ["activities", activitiesRoute],
    ["customer activities", customerActivitiesRoute],
  ])("lets a sales rep through the %s route", (_name, route: RouteRecordRaw) => {
    const next = runGuard(route);

    expect(next).toHaveBeenCalledTimes(1);
    expect(next).toHaveBeenCalledWith();
  });

  it.each([
    ["dashboard", dashboardRoute],
    ["reporting", reportingRoute],
    ["my customers", myCustomersRoute],
    ["customer profile", customerProfileRoute],
    ["customer orders", customerOrdersRoute],
    ["customer order", customerOrderRoute],
    ["all customer orders", allCustomerOrdersRoute],
    ["documents", documentsRoute],
    ["tasks", tasksRoute],
    ["activities", activitiesRoute],
    ["customer activities", customerActivitiesRoute],
  ])("resolves the %s route exactly once, to the account Dashboard, for a non-rep", (_name, route: RouteRecordRaw) => {
    access.isRep = false;

    const next = runGuard(route);

    expect(next).toHaveBeenCalledTimes(1);
    expect(next).toHaveBeenCalledWith({ name: "Dashboard" });
  });

  // The customer activity widget's page goes with the widget, which needs the analytics module; the rep lands on
  // that customer's profile instead.
  it("sends a sales rep to the customer's profile when the analytics module is not installed", () => {
    access.analyticsEnabled = false;

    const next = runGuard(customerActivitiesRoute);

    expect(next).toHaveBeenCalledTimes(1);
    expect(next).toHaveBeenCalledWith({ name: "SalesRepCustomerProfile", params: { organizationId: "org-1" } });
  });

  it("sends a sales rep to the hub dashboard when task management is not installed", () => {
    access.tasksEnabled = false;

    const next = runGuard(tasksRoute);

    expect(next).toHaveBeenCalledTimes(1);
    expect(next).toHaveBeenCalledWith({ name: DASHBOARD_ROUTE_NAME });
  });

  // No `requiresOrganization: false` override -> it keeps the "/company" parent's org gate.
  it("does not clear the org gate on the sales-reps route", () => {
    expect(salesRepsRoute.meta?.requiresOrganization).not.toBe(false);
  });
});

// The rail marks a link active by route record, so an area spanning sibling records has to say so. My activity
// needs no rule: a customer's feed is a route record of its own.
describe("sales-rep account rail areas", () => {
  it.each([
    ["the customer list", true, at("SalesRepMyCustomers")],
    ["a customer profile", true, at("SalesRepCustomerProfile")],
    ["a customer's orders", true, at("SalesRepCustomerOrders")],
    ["one of a customer's orders", true, at("SalesRepCustomerOrder")],
    ["a customer's activity", true, at("SalesRepCustomerActivities")],
    ["the rep's own activity", false, at("SalesRepActivities")],
    ["the all-customers order list", false, at("SalesRepAllCustomerOrders")],
    ["the dashboard", false, at("SalesRepDashboard")],
  ])("on %s, lights My customers: %s", (_name, inCustomers, route) => {
    expect(isMyCustomersArea(route)).toBe(inCustomers);
  });
});
