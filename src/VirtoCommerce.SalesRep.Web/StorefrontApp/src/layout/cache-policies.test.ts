import { InMemoryCache } from "@apollo/client/core";
import gql from "graphql-tag";
import { describe, expect, it } from "vitest";
import { layoutTypePolicies } from "./cache-policies";

// The real query's shape, trimmed to the ids that collide.
const QUERY = gql`
  query SalesRepLayout($scope: String!, $storeId: String) {
    layout(scope: $scope, storeId: $storeId) {
      schemaVersion
      regions {
        id
        blocks {
          id
          type
          hidden
        }
      }
    }
  }
`;

const block = (id: string, hidden = false) => ({ __typename: "LayoutBlock", id, type: id, hidden });

function document(...blockIds: string[]) {
  return {
    layout: {
      __typename: "SalesRepLayout",
      schemaVersion: 1,
      regions: [{ __typename: "LayoutRegion", id: "statistics", blocks: blockIds.map((id) => block(id)) }],
    },
  };
}

const read = (cache: InMemoryCache, scope: string) =>
  cache.readQuery<{ layout: { regions: { blocks: { type: string }[] }[] } }>({
    query: QUERY,
    variables: { scope, storeId: "B2B-store" },
  });

const typesIn = (result: ReturnType<typeof read>) => result?.layout.regions[0].blocks.map((b) => b.type);

describe("layout cache policies", () => {
  // Region ids are fixed across surfaces, so the second surface to load overwrites the first.
  it("without the policies, the two scopes clobber each other", () => {
    const cache = new InMemoryCache();

    cache.writeQuery({
      query: QUERY,
      variables: { scope: "dashboard", storeId: "B2B-store" },
      data: document("a", "b"),
    });
    cache.writeQuery({
      query: QUERY,
      variables: { scope: "customerProfile", storeId: "B2B-store" },
      data: document("x", "y"),
    });

    // Reading the dashboard back returns the customer profile's blocks.
    expect(typesIn(read(cache, "dashboard"))).toEqual(["x", "y"]);
  });

  it("with the policies, each scope keeps its own document", () => {
    const cache = new InMemoryCache({ typePolicies: layoutTypePolicies });

    cache.writeQuery({
      query: QUERY,
      variables: { scope: "dashboard", storeId: "B2B-store" },
      data: document("a", "b"),
    });
    cache.writeQuery({
      query: QUERY,
      variables: { scope: "customerProfile", storeId: "B2B-store" },
      data: document("x", "y"),
    });

    expect(typesIn(read(cache, "dashboard"))).toEqual(["a", "b"]);
    expect(typesIn(read(cache, "customerProfile"))).toEqual(["x", "y"]);
  });

  // `orders` is registered on both surfaces, so blocks collide by id too, not just regions.
  it("keeps same-id blocks apart across scopes", () => {
    const cache = new InMemoryCache({ typePolicies: layoutTypePolicies });

    cache.writeQuery({
      query: QUERY,
      variables: { scope: "dashboard", storeId: "B2B-store" },
      data: {
        layout: {
          __typename: "SalesRepLayout",
          schemaVersion: 1,
          regions: [{ __typename: "LayoutRegion", id: "mainLeft", blocks: [block("orders", false)] }],
        },
      },
    });
    cache.writeQuery({
      query: QUERY,
      variables: { scope: "customerProfile", storeId: "B2B-store" },
      data: {
        layout: {
          __typename: "SalesRepLayout",
          schemaVersion: 1,
          regions: [{ __typename: "LayoutRegion", id: "mainLeft", blocks: [block("orders", true)] }],
        },
      },
    });

    expect(read(cache, "dashboard")?.layout.regions[0].blocks[0]).toMatchObject({ hidden: false });
    expect(read(cache, "customerProfile")?.layout.regions[0].blocks[0]).toMatchObject({ hidden: true });
  });
});
