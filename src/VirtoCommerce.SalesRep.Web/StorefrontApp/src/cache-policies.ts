// `SalesRepCustomerDetails` and its `address` have no `id`, so Apollo would store them inline and replace them on
// every write: the My customers phones query, which selects fewer fields, would drop the profile's. Keyed by
// `organizationId` with a merged `address`, both queries add up to one entity.
import type { TypePolicies } from "@apollo/client/core";

export const customerTypePolicies: TypePolicies = {
  SalesRepCustomerDetails: {
    keyFields: ["organizationId"],
    fields: {
      address: { merge: true },
    },
  },
};
