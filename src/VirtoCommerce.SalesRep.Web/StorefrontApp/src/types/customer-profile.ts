import type { CustomerPhoneType } from "../phones";

// View models for the customer profile (VCST-5308): a non-null, flattened shape for the UI.
export type SalesRepCustomerProfileType = {
  organizationId: string;
  organizationName: string;
  iconUrl: string;
  // Only fields the backend exposes; the design's payment terms / balances have no source yet.
  accountType: string;
  /** Every number to call, the default first (VCST-6256). */
  phones: CustomerPhoneType[];
  shipTo: string;
  primaryContactName: string;
};
