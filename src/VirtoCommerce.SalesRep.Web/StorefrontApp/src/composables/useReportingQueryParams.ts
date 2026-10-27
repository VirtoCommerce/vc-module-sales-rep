import { computed, watch } from "vue";
import { useRouteQueryParam } from "@vc-frontend/core";
import { isReportingPeriod, REPORTING_ALL_CUSTOMERS, REPORTING_DEFAULT_PERIOD } from "../reporting";
import type { ReportingPeriodType } from "../reporting";
import type { SalesRepCustomerOptionType } from "./useSalesRepCustomerOptions";
import type { Ref } from "vue";

type CustomerOptionsType = {
  options: Ref<SalesRepCustomerOptionType[]>;
  totalCount: Ref<number>;
  loading: Ref<boolean>;
  failed: Ref<boolean>;
};

/** The Reporting selection kept in the URL (`?period=`, `?customer=`); a value the page cannot show reads as the default. */
export function useReportingQueryParams(customers: CustomerOptionsType) {
  const periodParam = useRouteQueryParam<string>("period", {
    defaultValue: REPORTING_DEFAULT_PERIOD,
    updateMethod: "replace",
  });

  // Guarded here, not by the param's validator: the host skips that for an empty value (`?period=`).
  const period = computed<ReportingPeriodType>({
    get: () => (isReportingPeriod(periodParam.value) ? periodParam.value : REPORTING_DEFAULT_PERIOD),
    set: (value) => {
      periodParam.value = value;
    },
  });

  const customerId = useRouteQueryParam<string>("customer", {
    defaultValue: REPORTING_ALL_CUSTOMERS,
    // A repeated `?customer=` arrives as an array.
    validator: (value) => typeof value === "string",
    updateMethod: "replace",
  });

  // A customer the rep no longer serves (or a mistyped id) would show an empty report under a blank picker. Only a
  // complete list can tell: past the options limit the id may still be one of the rep's.
  watch(
    [customerId, customers.options, customers.loading, customers.failed],
    () => {
      if (customers.loading.value || customers.failed.value || customerId.value === REPORTING_ALL_CUSTOMERS) {
        return;
      }
      const isComplete = customers.options.value.length >= customers.totalCount.value;
      const isKnown = customers.options.value.some((option) => option.organizationId === customerId.value);
      if (isComplete && !isKnown) {
        customerId.value = REPORTING_ALL_CUSTOMERS;
      }
    },
    { immediate: true },
  );

  return { period, customerId };
}
