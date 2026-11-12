// Call a customer (VCST-6256).

import { contactDisplayName } from "./utils";
import type { SalesRepPrimaryContactFieldsFragment } from "./api/graphql/types";

/** One number a rep can call, with whose it is. */
export type CustomerPhoneType = {
  /** The contact's name, or the organization's for an organization-level number. */
  name: string;
  /** As stored: what the rep reads. */
  number: string;
  /** `tel:` target built from the normalized number. */
  href: string;
};

/** The phone fields both `salesRepCustomers` items and `salesRepCustomer` details return. */
export type CustomerPhoneSourceType = {
  organizationName?: string;
  phone?: string;
  address?: { phone?: string };
  primaryContact?: SalesRepPrimaryContactFieldsFragment;
};

/** An extension at the end of a stored number: `x45`, `ext. 45`, `extension: 45`, `#45`. */
const EXTENSION = /(?:#|ext(?:ension)?\.?|x)[\s:]*(\d+)$/i;

/**
 * Digits and a leading `+`, then a comma and the extension's digits when one is stored: what a dialer needs, and what
 * makes two spellings of one number equal. The comma is a dialer pause, so the extension is sent as tones once the
 * call connects. RFC 3966 `;ext=` is not used: Android's dialer turns its letters into keypad digits.
 */
export function normalizePhone(value: string): string {
  const trimmed = value.trim();
  const extension = EXTENSION.exec(trimmed);
  const main = extension ? trimmed.slice(0, extension.index) : trimmed;
  const digits = main.replace(/\D/g, "");
  if (!digits) {
    return "";
  }
  const number = main.startsWith("+") ? `+${digits}` : digits;
  return extension ? `${number},${extension[1]}` : number;
}

/**
 * Every number the customer details expose, the primary contact's first: its phones, then the customer's primary
 * number and the default address's phone wherever those are not one of them. The first entry is the default number.
 */
export function buildCustomerPhones(details: CustomerPhoneSourceType): CustomerPhoneType[] {
  const contactName = contactDisplayName(details.primaryContact);
  const organizationName = details.organizationName ?? "";
  const candidates: { name: string; number?: string }[] = [
    ...(details.primaryContact?.phones ?? []).map((number) => ({ name: contactName || organizationName, number })),
    // The API's `phone` falls back to the organization's own number when the contact has none.
    { name: organizationName, number: details.phone },
    { name: organizationName, number: details.address?.phone },
  ];

  const seen = new Set<string>();
  const phones: CustomerPhoneType[] = [];
  for (const { name, number } of candidates) {
    const stored = number?.trim() ?? "";
    const normalized = normalizePhone(stored);
    if (!normalized || seen.has(normalized)) {
      continue;
    }
    seen.add(normalized);
    phones.push({ name, number: stored, href: `tel:${normalized}` });
  }
  return phones;
}
