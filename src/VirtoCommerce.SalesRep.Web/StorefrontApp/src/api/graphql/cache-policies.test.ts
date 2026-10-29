import { InMemoryCache } from "@apollo/client/core";
import { describe, expect, it } from "vitest";
import { statisticsTypePolicies } from "./cache-policies";
import { SalesRepCustomerOrderStatisticsDocument, SalesRepMonthlyOrderStatisticsDocument } from "./types";
import type {
  SalesRepCustomerOrderStatisticsQueryVariables,
  SalesRepMonthlyOrderStatisticsQueryVariables,
} from "./types";
import type { TypePolicies } from "@apollo/client/core";

// As the host registers it: a `MoneyType` replaces the one in the cache instead of merging into it.
const HOST_POLICIES: TypePolicies = { MoneyType: { merge: false } };

const ROOT = { storeId: "B2B-store", currencyCode: "USD", cultureName: "en-US" };

// The Dashboard's month-to-date and Reporting's current month: one window, so one bucket in the cache.
const OCTOBER_TO_DATE = { from: "2026-10-01T00:00:00.000Z", to: "2026-10-28T23:59:59.999Z" };
const CURRENT_SLOT = 9;

const DASHBOARD_VARIABLES: SalesRepCustomerOrderStatisticsQueryVariables = {
  ...ROOT,
  mtdFrom: OCTOBER_TO_DATE.from,
  mtdTo: OCTOBER_TO_DATE.to,
  ytdFrom: "2026-01-01T00:00:00.000Z",
  ytdTo: OCTOBER_TO_DATE.to,
  withMtd: true,
  withYtd: true,
  withAverageOrderValue: false,
  withWeek: false,
  withNewOrders: false,
  withMonthOverMonth: false,
  withYearOverYear: false,
};

const REPORTING_VARIABLES = {
  ...ROOT,
  ...Object.fromEntries(Array.from({ length: 12 }, (_, i) => [`use${i}`, i === CURRENT_SLOT])),
  prevFrom: "2026-09-01T00:00:00.000Z",
  prevTo: "2026-09-30T23:59:59.999Z",
  [`from${CURRENT_SLOT}`]: OCTOBER_TO_DATE.from,
  [`to${CURRENT_SLOT}`]: OCTOBER_TO_DATE.to,
  [`lyFrom${CURRENT_SLOT}`]: "2025-10-01T00:00:00.000Z",
  [`lyTo${CURRENT_SLOT}`]: "2025-10-31T23:59:59.999Z",
} as SalesRepMonthlyOrderStatisticsQueryVariables;

const money = (amount: number) => ({ __typename: "MoneyType", amount, formattedAmount: `$${amount}` });
const bucket = (amount: number, count: number) => ({
  __typename: "CustomerOrderStatisticsPeriod",
  total: money(amount),
  count,
});
const amountOnly = (amount: number, count: number) => ({
  __typename: "CustomerOrderStatisticsPeriod",
  total: { __typename: "MoneyType", amount },
  count,
});

const DASHBOARD_DATA = {
  salesRepCustomerOrderStatistics: {
    __typename: "CustomerOrderStatistics",
    currencyCode: "USD",
    mtd: bucket(1200, 3),
    ytd: bucket(9800, 25),
  },
};

const REPORTING_DATA = {
  salesRepCustomerOrderStatistics: {
    __typename: "CustomerOrderStatistics",
    currencyCode: "USD",
    prev: amountOnly(1500, 4),
    [`m${CURRENT_SLOT}`]: amountOnly(1200, 3),
    [`ly${CURRENT_SLOT}`]: amountOnly(1100, 2),
  },
};

function writeBoth(cache: InMemoryCache) {
  cache.writeQuery({
    query: SalesRepCustomerOrderStatisticsDocument,
    variables: DASHBOARD_VARIABLES,
    data: DASHBOARD_DATA,
  });
  cache.writeQuery({
    query: SalesRepMonthlyOrderStatisticsDocument,
    variables: REPORTING_VARIABLES,
    data: REPORTING_DATA,
  });
}

const readDashboard = (cache: InMemoryCache) =>
  cache.readQuery({ query: SalesRepCustomerOrderStatisticsDocument, variables: DASHBOARD_VARIABLES });
const readReporting = (cache: InMemoryCache) =>
  cache.readQuery({ query: SalesRepMonthlyOrderStatisticsDocument, variables: REPORTING_VARIABLES });

describe("statistics cache policies", () => {
  it("without the policies, Reporting replaces the Dashboard's statistics", () => {
    const cache = new InMemoryCache({ typePolicies: HOST_POLICIES });

    writeBoth(cache);

    expect(readDashboard(cache)).toBeNull();
  });

  it("with the policies, the Dashboard and Reporting both keep their figures", () => {
    const cache = new InMemoryCache({ typePolicies: { ...HOST_POLICIES, ...statisticsTypePolicies } });

    writeBoth(cache);

    expect(readDashboard(cache)).toEqual(DASHBOARD_DATA);
    expect(readReporting(cache)).toEqual(REPORTING_DATA);
  });
});
