import { computed, ref, toValue } from "vue";
import { Logger } from "@vc-frontend/core";
import { SalesRepCustomerPhonesDocument } from "../api/graphql/types";
import { HUB_FETCH_POLICY } from "../constants";
import { buildCustomerPhones } from "../phones";
import { PAGE_SIZE } from "./useSalesRepCustomers";
import { useSalesRepHubQuery } from "./useSalesRepHubQuery";
import type {
  CustomerPhoneFieldsFragment,
  SalesRepCustomerPhonesQuery,
  SalesRepCustomerPhonesQueryVariables,
} from "../api/graphql/types";
import type { CustomerPhonesStateType } from "../phones";
import type { MaybeRefOrGetter } from "vue";

/** The query carries one alias per My customers row; a page never holds more. */
export const CUSTOMER_PHONES_SLOTS = PAGE_SIZE;

function slotOf(
  result: SalesRepCustomerPhonesQuery | undefined,
  index: number,
): CustomerPhoneFieldsFragment | undefined {
  return result?.[`c${index}` as keyof SalesRepCustomerPhonesQuery] ?? undefined;
}

/**
 * The numbers of the customers on one My customers page (VCST-6256). The list query has no phone fields, so this reads
 * every row's details in one request; it runs again only when the set of customers on the page changes.
 */
export function useSalesRepCustomerPhones(organizationIds: MaybeRefOrGetter<string[]>) {
  const ids = computed(() => toValue(organizationIds).slice(0, CUSTOMER_PHONES_SLOTS));

  const variables = computed(() => {
    const slots: Record<string, string | boolean> = {};
    for (let i = 0; i < CUSTOMER_PHONES_SLOTS; i++) {
      slots[`use${i}`] = i < ids.value.length;
      slots[`id${i}`] = ids.value[i] ?? "";
    }
    return slots as SalesRepCustomerPhonesQueryVariables;
  });

  const { result, loading, error, onResult, onError } = useSalesRepHubQuery(SalesRepCustomerPhonesDocument, variables, {
    fetchPolicy: HUB_FETCH_POLICY,
    enabled: computed(() => ids.value.length > 0),
  });

  // Apollo starts a query `enabled` just turned on a tick later, so `loading` is briefly false with nothing in flight.
  // Rows wait for an answer about this very page, or a customer that has a number would flash "No phone number".
  const pageKey = computed(() => ids.value.join(","));
  const answeredFor = ref<string | undefined>(undefined);

  onResult((queryResult) => {
    if (!queryResult.loading) {
      answeredFor.value = pageKey.value;
    }
  });

  onError((queryError) => {
    answeredFor.value = pageKey.value;
    // No toast: each row's call action says it could not load.
    Logger.error("[sales-rep] salesRepCustomer (phones) failed:", queryError);
  });

  // Keyed by id, so a row only reads numbers fetched for it, never a slot left over from the previous page.
  const phonesById = computed(() => {
    const map = new Map<string, ReturnType<typeof buildCustomerPhones>>();
    ids.value.forEach((id, index) => {
      const details = slotOf(result.value, index);
      if (details?.organizationId === id) {
        map.set(id, buildCustomerPhones(details));
      }
    });
    return map;
  });

  function phonesOf(organizationId: string): CustomerPhonesStateType {
    const phones = phonesById.value.get(organizationId);
    if (phones) {
      return { status: "ready", phones };
    }
    if (loading.value || answeredFor.value !== pageKey.value) {
      return { status: "loading" };
    }
    if (error.value) {
      return { status: "failed" };
    }
    // Settled without this customer: the rep no longer serves it, so there is nothing to call.
    return { status: "ready", phones: [] };
  }

  return { phonesOf };
}
