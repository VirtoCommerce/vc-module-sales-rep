import { describe, expect, it } from "vitest";
import { buildCustomerPhones, normalizePhone } from "./phones";
import type { CustomerPhoneSourceType } from "./phones";

function details(overrides: Partial<CustomerPhoneSourceType> = {}): CustomerPhoneSourceType {
  return { organizationName: "Acme Corp", ...overrides };
}

const contact = (phones: string[], fullName = "Jane Smith") => ({ fullName, name: "jsmith", phones });

describe("normalizePhone", () => {
  it.each([
    ["+1 (555) 010-2030", "+15550102030"],
    ["555.010.2030", "5550102030"],
    ["  +44 20 7946 0958  ", "+442079460958"],
    ["ext. only", ""],
    ["", ""],
  ])("normalizes %j to %j", (value, expected) => {
    expect(normalizePhone(value)).toBe(expected);
  });
});

describe("buildCustomerPhones", () => {
  it("has nothing to call when no number is stored", () => {
    expect(buildCustomerPhones(details({ phone: " ", address: { phone: undefined } }))).toEqual([]);
  });

  it("names a contact's numbers after the contact, in their order", () => {
    const phones = buildCustomerPhones(details({ primaryContact: contact(["+1 555 010 2030", "+1 555 010 9999"]) }));

    expect(phones).toEqual([
      { name: "Jane Smith", number: "+1 555 010 2030", href: "tel:+15550102030" },
      { name: "Jane Smith", number: "+1 555 010 9999", href: "tel:+15550109999" },
    ]);
  });

  it("falls back to the contact's display name, then the organization's", () => {
    expect(buildCustomerPhones(details({ primaryContact: { name: "jsmith", phones: ["1"] } }))[0].name).toBe("jsmith");
    expect(buildCustomerPhones(details({ primaryContact: { phones: ["1"] } }))[0].name).toBe("Acme Corp");
  });

  it("names an organization-level number after the organization", () => {
    expect(buildCustomerPhones(details({ phone: "+1 555 000 0000" }))).toEqual([
      { name: "Acme Corp", number: "+1 555 000 0000", href: "tel:+15550000000" },
    ]);
  });

  it("keeps the contact's numbers first, then the primary number and the address phone", () => {
    const phones = buildCustomerPhones(
      details({ primaryContact: contact(["111"]), phone: "222", address: { phone: "333" } }),
    );

    expect(phones.map((phone) => phone.number)).toEqual(["111", "222", "333"]);
  });

  it("lists a number stored in two places once, under its first owner", () => {
    const phones = buildCustomerPhones(
      details({
        primaryContact: contact(["+1 (555) 010-2030"]),
        // The API's primary number is the contact's first phone, and the address repeats it in another spelling.
        phone: "+1 (555) 010-2030",
        address: { phone: "+1 555 010 2030" },
      }),
    );

    expect(phones).toEqual([{ name: "Jane Smith", number: "+1 (555) 010-2030", href: "tel:+15550102030" }]);
  });

  it("drops values with no digits", () => {
    expect(buildCustomerPhones(details({ primaryContact: contact(["n/a", "555"]) })).map((p) => p.number)).toEqual([
      "555",
    ]);
  });
});
