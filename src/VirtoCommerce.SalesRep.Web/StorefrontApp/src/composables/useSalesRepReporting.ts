import { computed, shallowRef, toValue, watch } from "vue";
import { globals, Logger } from "@vc-frontend/core";
import { SalesRepMonthlyOrderStatisticsDocument } from "../api/graphql/types";
import { HUB_FETCH_POLICY } from "../constants";
import {
  buildReportingMonths,
  previousMonthWindow,
  summarizeReporting,
  toReportingMonth,
  withMonthChanges,
  REPORTING_MONTH_SLOTS,
} from "../reporting";
import { buildReportingMock, isReportingMockEnabled } from "../reporting-mock";
import { useSalesRepHubQuery } from "./useSalesRepHubQuery";
import { useToday } from "./useToday";
import type {
  SalesRepMonthlyOrderStatisticsQuery,
  SalesRepMonthlyOrderStatisticsQueryVariables,
} from "../api/graphql/types";
import type { ReportingMonthSlotType, ReportingPeriodTotalsType, ReportingPeriodType } from "../reporting";
import type { MaybeRefOrGetter } from "vue";

type StatisticsType = NonNullable<SalesRepMonthlyOrderStatisticsQuery["salesRepCustomerOrderStatistics"]>;
type SlotKeyType = Exclude<keyof StatisticsType, "currencyCode">;

type UseSalesRepReportingOptionsType = {
  period: MaybeRefOrGetter<ReportingPeriodType>;
  /** One customer; empty for all the rep's customers. */
  organizationId: MaybeRefOrGetter<string>;
};

function totals(statistics: StatisticsType | undefined, key: string): ReportingPeriodTotalsType | undefined {
  const value = statistics?.[key as SlotKeyType];
  return value ? { amount: value.total.amount, count: value.count } : undefined;
}

/** Monthly revenue, orders and AOV for the Reporting page, with last year beside them. */
export function useSalesRepReporting(options: UseSalesRepReportingOptionsType) {
  const today = useToday();
  const organizationId = computed(() => toValue(options.organizationId) || undefined);
  const isMock = isReportingMockEnabled();

  // The months the selection covers, and the month before them (the first month's "vs previous month").
  const plan = computed(() => {
    const period = toValue(options.period);
    const now = new Date(today.value);
    return { months: buildReportingMonths(period, now), previous: previousMonthWindow(period, now) };
  });

  const variables = computed(() => {
    const { months, previous } = plan.value;
    const slots: Record<string, string | boolean | undefined> = {};
    for (let i = 0; i < REPORTING_MONTH_SLOTS; i++) {
      const month = months.at(i);
      slots[`use${i}`] = month !== undefined && month.state !== "upcoming";
      slots[`from${i}`] = month?.from;
      slots[`to${i}`] = month?.to;
      slots[`lyFrom${i}`] = month?.lyFrom;
      slots[`lyTo${i}`] = month?.lyTo;
    }
    return {
      organizationId: organizationId.value,
      storeId: globals.storeId,
      currencyCode: globals.currencyCode,
      cultureName: globals.cultureName,
      prevFrom: previous.from,
      prevTo: previous.to,
      ...slots,
    } as SalesRepMonthlyOrderStatisticsQueryVariables;
  });

  const query = useSalesRepHubQuery(SalesRepMonthlyOrderStatisticsDocument, variables, {
    fetchPolicy: HUB_FETCH_POLICY,
    enabled: !isMock,
    keepPreviousResult: true,
  });

  query.onError((error) => {
    Logger.error("[sales-rep] salesRepCustomerOrderStatistics (reporting) failed:", error);
  });

  // `result` keeps the previous response until the next one lands, and its month aliases are positional (m0…m11),
  // so each response is kept together with the months it was fetched for, never read against the ones just picked.
  const settled = shallowRef<{ months: ReportingMonthSlotType[]; statistics?: StatisticsType }>({ months: [] });

  watch(
    query.result,
    (value) => {
      settled.value = {
        months: plan.value.months,
        statistics: value?.salesRepCustomerOrderStatistics ?? undefined,
      };
    },
    { immediate: true },
  );

  const months = computed(() => {
    if (isMock) {
      const mock = buildReportingMock(plan.value.months, organizationId.value ?? "");
      return withMonthChanges(
        plan.value.months.map((month, i) => toReportingMonth(month, mock.raw[i] ?? {})),
        mock.before,
      );
    }
    const { months: slots, statistics } = settled.value;
    return withMonthChanges(
      slots.map((month, i) =>
        toReportingMonth(month, { totals: totals(statistics, `m${i}`), lastYear: totals(statistics, `ly${i}`) }),
      ),
      totals(statistics, "prev"),
    );
  });

  return {
    months,
    summary: computed(() => summarizeReporting(months.value)),
    currencyCode: computed(() => settled.value.statistics?.currencyCode ?? globals.currencyCode),
    loading: computed(() => !isMock && query.loading.value),
    error: computed(() => (isMock ? null : query.error.value)),
    /** True when the page shows example figures (see reporting-mock.ts), not the backend's. */
    isMock,
  };
}
