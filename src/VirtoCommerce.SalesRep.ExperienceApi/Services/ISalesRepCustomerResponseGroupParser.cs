using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Translates the GraphQL field selection of a Sales Rep customer query into the minimal
/// <c>MemberResponseGroup</c> needed to populate exactly those fields — so the member load pulls collections
/// (addresses, phones) only when the caller selected fields that need them (scalars like <c>iconUrl</c> and
/// <c>organizationName</c> load with <c>Default</c>). Mirrors <see cref="ISalesRepOrderResponseGroupParser"/>;
/// shared by the customers list and the single-customer details query so the two can't drift.
/// </summary>
public interface ISalesRepCustomerResponseGroupParser
{
    /// <param name="includeFields">Requested GraphQL selection paths (e.g. "address.city", "phone").</param>
    /// <returns>A <c>MemberResponseGroup</c> flags string for <c>MembersSearchCriteria.ResponseGroup</c> / <c>IMemberService.GetByIdsAsync</c>.</returns>
    string GetResponseGroup(IList<string> includeFields);
}
