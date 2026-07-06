import { useApiClient } from "@vc-shell/framework";
import { CustomerModuleClient, Organization, MembersSearchCriteria } from "../../../../api_client/virtocommerce.customer";

export default () => {
  const { getApiClient } = useApiClient(CustomerModuleClient);

  /**
   * Loader compatible with VcSelect async options: (keyword, skip, ids) => { results, totalCount }.
   * When `ids` is provided VcSelect is resolving labels for already-selected values.
   */
  const loadOrganizations = async (keyword?: string, skip?: number, ids?: string[]) => {
    const apiClient = await getApiClient();
    const criteria = {
      memberType: "Organization",
      take: 20,
      skip: skip ?? 0,
    } as MembersSearchCriteria;

    if (ids?.length) {
      criteria.objectIds = ids;
      criteria.take = ids.length;
    } else {
      criteria.keyword = keyword;
    }

    const apiResult = await apiClient.searchOrganizations(criteria);
    return {
      results: (apiResult?.results ?? []) as Organization[],
      totalCount: apiResult?.totalCount ?? 0,
    };
  };

  return {
    loadOrganizations,
  };
};
