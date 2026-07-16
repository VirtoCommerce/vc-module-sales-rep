using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Translates the GraphQL field selection of a Sales Rep member projection — a customer organization
/// (<c>SalesRepCustomer</c>/<c>SalesRepCustomerDetails</c>) or a rep contact (<c>SalesRepContact</c>) — into the
/// minimal <c>MemberResponseGroup</c> needed to populate exactly those fields, so the member load pulls collections
/// (addresses, phones, emails) only when the caller selected fields that need them (scalars like <c>iconUrl</c>,
/// <c>organizationName</c>, <c>photoUrl</c> load with <c>Default</c>). Mirrors
/// <see cref="ISalesRepOrderResponseGroupParser"/>; shared by the customers list/details and the
/// <c>customerSalesReps</c> query so they can't drift.
/// </summary>
public interface ISalesRepMemberResponseGroupParser
{
    /// <param name="includeFields">Requested GraphQL selection paths (e.g. "address.city", "phone", "emails").</param>
    /// <returns>A <c>MemberResponseGroup</c> flags string for <c>MembersSearchCriteria.ResponseGroup</c> / <c>IMemberService.GetByIdsAsync</c>.</returns>
    string GetResponseGroup(IList<string> includeFields);
}
