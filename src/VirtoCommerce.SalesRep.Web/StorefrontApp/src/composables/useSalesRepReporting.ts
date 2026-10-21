import { computed, toValue } from "vue";
import { globals, Logger } from "@vc-frontend/core";
import { SalesRepMonthlyOrderStatisticsDocument, SalesRepOrderFilterRulesDocument } from "../api/graphql/types";
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
import type {
  SalesRepMonthlyOrderStatisticsQuery,
  SalesRepMonthlyOrderStatisticsQueryVariables,
} from "../api/graphql/types";
import type { ReportingPeriodTotalsType, ReportingPeriodType, ReportingRawMonthType } from "../reporting";
import type { MaybeRefOrGetter } from "vue";

type StatisticsType = NonNullable<SalesRepMonthlyOrderStatisticsQuery["salesRepCustomerOrderStatistics"]>;
type SlotKeyType = Exclude<keyof StatisticsType, "currencyCode">;
type SlotValueType = StatisticsType[SlotKeyType];

// The order status rules are data-derived: the Cancelled rule exists only while the scope has cancelled orders.
const CANCELLED_RULE_PATTERN = /^cancel/i;

type UseSalesRepReportingOptionsType = {
  period: MaybeRefOrGetter<ReportingPeriodType>;
  /** One customer; empty for all the rep's customers. */
  organizationId: MaybeRefOrGetter<string>;
};

function totals(value: SlotValueType): ReportingPeriodTotalsType | undefined {
  return value ? { amount: value.total.amount, count: value.count } : undefined;
}

function slotValue(statistics: StatisticsType | undefined, key: string): SlotValueType {
  return statistics?.[key as SlotKeyType];
}

/** Monthly revenue, orders and AOV for the Reporting page, net of cancelled orders, with last year beside them. */
export function useSalesRepReporting(options: UseSalesRepReportingOptionsType) {
  const organizationId = computed(() => toValue(options.organizationId) || undefined);
  const months = computed(() => buildReportingMonths(toValue(options.period)));
  const previousMonth = computed(() => previousMonthWindow(toValue(options.period)));
  const isMock = isReportingMockEnabled();

  const rules = useSalesRepHubQuery(
    SalesRepOrderFilterRulesDocument,
    computed(() => ({
      storeId: globals.storeId,
      cultureName: globals.cultureName,
      organizationId: organizationId.value,
    })),
    { enabled: !isMock },
  );

  const cancelledFilter = computed(
    () =>
      rules.result.value?.salesRepOrderFilterRules?.find((rule) => CANCELLED_RULE_PATTERN.test(rule?.name ?? ""))?.name,
  );

  const variables = computed(() => {
    const slots: Record<string, string | boolean | undefined> = {};
    for (let i = 0; i < REPORTING_MONTH_SLOTS; i++) {
      const month = months.value[i];
      slots[`use${i}`] = Boolean(month) && month.state !== "upcoming";
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
      cancelledFilter: cancelledFilter.value,
      noCancelled: !cancelledFilter.value,
      prevFrom: previousMonth.value.from,
      prevTo: previousMonth.value.to,
      ...slots,
    } as SalesRepMonthlyOrderStatisticsQueryVariables;
  });

  // Waits for the rule list: without it, cancelled orders would be counted and then drop out a moment later.
  const statistics = useSalesRepHubQuery(SalesRepMonthlyOrderStatisticsDocument, variables, {
    fetchPolicy: HUB_FETCH_POLICY,
    enabled: computed(() => !isMock && !rules.loading.value),
    keepPreviousResult: true,
  });

  rules.onError((error) => {
    Logger.error("[sales-rep] salesRepOrderFilterRules (reporting) failed:", error);
  });
  statistics.onError((error) => {
    Logger.error("[sales-rep] salesRepCustomerOrderStatistics (reporting) failed:", error);
  });

  const result = computed(() => statistics.result.value?.salesRepCustomerOrderStatistics ?? undefined);

  const rows = computed(() => {
    if (isMock) {
      const mock = buildReportingMock(months.value, organizationId.value ?? "");
      return withMonthChanges(
        months.value.map((month, i) => toReportingMonth(month, mock.raw[i] ?? {})),
        mock.before,
      );
    }
    const current = months.value.map((month, i) => {
      const raw: ReportingRawMonthType = {
        all: totals(slotValue(result.value, `m${i}`)),
        cancelled: totals(slotValue(result.value, `mc${i}`)),
        lastYear: totals(slotValue(result.value, `ly${i}`)),
        lastYearCancelled: totals(slotValue(result.value, `lyc${i}`)),
      };
      return toReportingMonth(month, raw);
    });
    return withMonthChanges(current, {
      all: totals(slotValue(result.value, "prev")),
      cancelled: totals(slotValue(result.value, "prevc")),
    });
  });

  return {
    months: rows,
    summary: computed(() => summarizeReporting(rows.value)),
    currencyCode: computed(() => result.value?.currencyCode ?? globals.currencyCode),
    loading: computed(() => !isMock && (rules.loading.value || statistics.loading.value)),
    error: computed(() => (isMock ? null : (rules.error.value ?? statistics.error.value))),
    /** True when the page shows example figures (see reporting-mock.ts), not the backend's. */
    isMock,
  };
}
