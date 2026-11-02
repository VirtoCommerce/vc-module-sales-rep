// Call a customer (VCST-6256).

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
  primaryContact?: { fullName?: string; name?: string; phones?: string[] };
};

/** Digits and a leading `+` only: what a dialer needs, and what makes two spellings of one number equal. */
export function normalizePhone(value: string): string {
  const trimmed = value.trim();
  const digits = trimmed.replace(/\D/g, "");
  return digits && trimmed.startsWith("+") ? `+${digits}` : digits;
}

/**
 * Every number the customer details expose, the primary contact's first: its phones, then the customer's primary
 * number and the default address's phone wherever those are not one of them. The first entry is the default number.
 */
export function buildCustomerPhones(details: CustomerPhoneSourceType | undefined | null): CustomerPhoneType[] {
  if (!details) {
    return [];
  }

  const contactName = details.primaryContact?.fullName || details.primaryContact?.name || "";
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
