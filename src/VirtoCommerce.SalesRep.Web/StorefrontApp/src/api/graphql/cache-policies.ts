// The statistics types have no id, so Apollo stores them inline under `salesRepCustomerOrderStatistics(...)` and its
// `period(from, to)` buckets, keyed by arguments alone. The Dashboard, the customer profile and Reporting write the same
// entries with different selections; the current month's window is both the Dashboard's month-to-date and Reporting's
// current month. Merging keeps every query's fields. `total` merges too, although the host replaces `MoneyType`, because
// a bucket's total is one figure that each query selects only part of.
import type { TypePolicies } from "@apollo/client/core";

export const statisticsTypePolicies: TypePolicies = {
  CustomerOrderStatistics: { merge: true },
  CustomerOrderStatisticsPeriod: { merge: true, fields: { total: { merge: true } } },
};
