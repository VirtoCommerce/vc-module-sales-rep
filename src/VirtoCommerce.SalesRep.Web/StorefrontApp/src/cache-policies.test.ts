import { InMemoryCache } from "@apollo/client/core";
import { describe, expect, it } from "vitest";
import { SalesRepCustomerDocument, SalesRepCustomerPhonesDocument } from "./api/graphql/types";
import { customerTypePolicies } from "./cache-policies";
import type { SalesRepCustomerPhonesQueryVariables } from "./api/graphql/types";

const ORGANIZATION_ID = "org-1";

const phoneFields = {
  __typename: "SalesRepCustomerDetails" as const,
  organizationId: ORGANIZATION_ID,
  organizationName: "Acme",
  phone: "+1 555 010 2030",
  address: { __typename: "SalesRepAddress" as const, phone: "+1 555 010 4000" },
  primaryContact: {
    __typename: "SalesRepContact" as const,
    id: "contact-1",
    fullName: "Jane Smith",
    name: "jane",
    phones: ["+1 555 010 2030"],
  },
};

const profile = {
  salesRepCustomer: {
    ...phoneFields,
    iconUrl: "https://example.com/acme.png",
    accountType: "Retail",
    address: { ...phoneFields.address, city: "Austin", regionName: "Texas" },
  },
};

function phonesVariables(): SalesRepCustomerPhonesQueryVariables {
  const variables: Record<string, string | boolean> = {};
  for (let i = 0; i < 10; i++) {
    variables[`use${i}`] = i === 0;
    variables[`id${i}`] = i === 0 ? ORGANIZATION_ID : "";
  }
  return variables as SalesRepCustomerPhonesQueryVariables;
}

function visitProfileThenList(cache: InMemoryCache) {
  cache.writeQuery({
    query: SalesRepCustomerDocument,
    variables: { organizationId: ORGANIZATION_ID },
    data: profile,
  });
  cache.writeQuery({
    query: SalesRepCustomerPhonesDocument,
    variables: phonesVariables(),
    data: { c0: phoneFields },
  });

  return cache.readQuery({ query: SalesRepCustomerDocument, variables: { organizationId: ORGANIZATION_ID } });
}

describe("customer cache policies", () => {
  it("without the policies, the phones query drops the profile's fields", () => {
    expect(visitProfileThenList(new InMemoryCache())).toBeNull();
  });

  it("with the policies, the profile stays complete after the phones query", () => {
    const result = visitProfileThenList(new InMemoryCache({ typePolicies: customerTypePolicies }));

    expect(result?.salesRepCustomer).toMatchObject({
      iconUrl: "https://example.com/acme.png",
      accountType: "Retail",
      address: { city: "Austin", regionName: "Texas" },
    });
  });
});
