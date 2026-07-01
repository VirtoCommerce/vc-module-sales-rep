using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Searches Sales Reps as the union of:
///   A) users whose global role grants "sales-rep:access", and
///   B) users who hold a role granting "sales-rep:access" in any OrganizationMembership.
/// The heavy per-org aggregation is pushed to the database (one row per rep);
/// the resulting candidate set is then enriched, keyword-filtered, sorted and paged.
/// </summary>
public class SalesRepSearchService : ISalesRepSearchService
{
    private readonly IUserSearchService _userSearchService;
    private readonly IMemberService _memberService;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;
    private readonly ISalesRepRoleResolver _roleResolver;

    public SalesRepSearchService(
        IUserSearchService userSearchService,
        IMemberService memberService,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepRoleResolver roleResolver)
    {
        _userSearchService = userSearchService;
        _memberService = memberService;
        _membershipSearchService = membershipSearchService;
        _roleResolver = roleResolver;
    }

    public virtual Task<SalesRepSearchResult> SearchAsync(SalesRepSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return SearchInternalAsync(criteria);
    }

    protected virtual async Task<SalesRepSearchResult> SearchInternalAsync(SalesRepSearchCriteria criteria)
    {
        var result = new SalesRepSearchResult();

        var grantingRoles = await _roleResolver.GetRolesGrantingAccessAsync();
        if (grantingRoles.Count == 0)
        {
            return result;
        }

        var grantingRoleIds = grantingRoles.Select(r => r.Id).ToArray();
        var orgScoped = !string.IsNullOrEmpty(criteria.OrganizationId);

        // Source A: users whose GLOBAL role grants the permission (skipped for an org-scoped view).
        var usersById = new Dictionary<string, ApplicationUser>();
        HashSet<string> globalRoleUserIds = orgScoped ? [] : await LoadGlobalRoleUsersAsync(grantingRoles, usersById);

        // Source B: per-org reps via a DB-side aggregate. An org-scoped view counts only users serving that
        // org, so total org counts are then resolved separately to reflect all of a rep's served organizations.
        var orgIds = orgScoped ? new[] { criteria.OrganizationId } : null;
        var scopedCounts = await _membershipSearchService.GetCountsByUserAsync(new OrganizationMembershipSearchCriteria
        {
            RoleIds = grantingRoleIds,
            OrganizationIds = orgIds,
        });
        var totalCounts = orgScoped
            ? await _membershipSearchService.GetCountsByUserAsync(new OrganizationMembershipSearchCriteria
            {
                RoleIds = grantingRoleIds,
                UserIds = scopedCounts.Keys.ToArray(),
            })
            : scopedCounts;

        var candidateUserIds = new HashSet<string>(globalRoleUserIds);
        candidateUserIds.UnionWith(scopedCounts.Keys);

        await LoadMissingUsersAsync(usersById, candidateUserIds);

        var rows = BuildRows(criteria, candidateUserIds, usersById, totalCounts, globalRoleUserIds);

        var items = await EnrichAsync(rows);
        items = ApplyKeyword(items, criteria.Keyword);
        items = ApplySort(items, criteria.SortInfos);

        result.TotalCount = items.Count;
        var take = criteria.Take <= 0 ? items.Count : criteria.Take;
        result.Results = items.Skip(criteria.Skip).Take(take).ToList();

        return result;
    }

    /// <summary>Load users whose global role grants the permission into <paramref name="usersById"/> and return their ids.</summary>
    protected virtual async Task<HashSet<string>> LoadGlobalRoleUsersAsync(IList<Role> grantingRoles, Dictionary<string, ApplicationUser> usersById)
    {
        var ids = new HashSet<string>();
        var roleNames = grantingRoles.Select(r => r.Name).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (roleNames.Length == 0)
        {
            return ids;
        }

        // Fetch ALL users holding a granting role. IUserSearchService exposes only SearchUsersAsync and does
        // NOT implement ISearchService, so the platform SearchAllAsync paging helper is unavailable here; a
        // bounded Take would silently truncate the result set. The unbounded Take is therefore intentional and
        // relies on the bounded admin (Sales Rep) population — acceptable per the search's documented scope.
        var globalUsers = (await _userSearchService.SearchUsersAsync(new UserSearchCriteria
        {
            Roles = roleNames,
            Take = int.MaxValue,
        })).Results;

        foreach (var user in globalUsers)
        {
            usersById[user.Id] = user;
            ids.Add(user.Id);
        }

        return ids;
    }

    /// <summary>Project the candidate users into rows, applying the id-level filters and deduping to one row per member.</summary>
    protected virtual List<CandidateRow> BuildRows(
        SalesRepSearchCriteria criteria,
        HashSet<string> candidateUserIds,
        Dictionary<string, ApplicationUser> usersById,
        IDictionary<string, int> totalCounts,
        HashSet<string> globalRoleUserIds)
    {
        var rows = new List<CandidateRow>();
        foreach (var userId in candidateUserIds)
        {
            if (!usersById.TryGetValue(userId, out var user) || string.IsNullOrEmpty(user.MemberId))
            {
                continue;
            }

            var orgCount = totalCounts.TryGetValue(userId, out var count) ? count : 0;
            var isLocked = IsAccountLocked(user);

            if (!PassesFilters(criteria, isLocked, orgCount))
            {
                continue;
            }

            rows.Add(new CandidateRow
            {
                MemberId = user.MemberId,
                UserId = userId,
                UserName = user.UserName,
                Email = user.Email,
                IsLocked = isLocked,
                OrganizationsCount = orgCount,
                HasGlobalSalesRepRole = globalRoleUserIds.Contains(userId),
            });
        }

        // One account per member expected; dedupe defensively.
        return rows.GroupBy(r => r.MemberId).Select(g => g.First()).ToList();
    }

    protected static bool PassesFilters(SalesRepSearchCriteria criteria, bool isLocked, int orgCount)
    {
        if (criteria.OnlyBlocked && !isLocked)
        {
            return false;
        }

        return !criteria.OnlyUnassigned || orgCount == 0;
    }

    protected virtual async Task LoadMissingUsersAsync(Dictionary<string, ApplicationUser> usersById, HashSet<string> candidateUserIds)
    {
        var missing = candidateUserIds.Where(id => !usersById.ContainsKey(id)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        // Reuse the platform user search (honors ObjectIds, eager-loads roles) rather than hand-chunking UserManager.Users.
        var loaded = (await _userSearchService.SearchUsersAsync(new UserSearchCriteria
        {
            ObjectIds = missing,
            Take = missing.Count,
        })).Results;

        foreach (var user in loaded)
        {
            usersById[user.Id] = user;
        }
    }

    protected virtual async Task<List<SalesRepListItem>> EnrichAsync(List<CandidateRow> rows)
    {
        var memberIds = rows.Select(r => r.MemberId).Distinct().ToArray();
        var members = memberIds.Length > 0
            ? await _memberService.GetByIdsAsync(memberIds, MemberResponseGroup.WithEmails.ToString())
            : [];
        var membersById = members.ToDictionary(m => m.Id, m => m);

        return rows.Select(r =>
        {
            membersById.TryGetValue(r.MemberId, out var member);
            var contact = member as Contact;
            return new SalesRepListItem
            {
                Id = r.MemberId,
                UserId = r.UserId,
                UserName = r.UserName,
                FullName = contact?.FullName ?? member?.Name,
                Email = member?.Emails?.FirstOrDefault() ?? r.Email,
                OrganizationsCount = r.OrganizationsCount,
                IsLocked = r.IsLocked,
                HasGlobalSalesRepRole = r.HasGlobalSalesRepRole,
                CreatedDate = member?.CreatedDate,
                ModifiedDate = member?.ModifiedDate,
            };
        }).ToList();
    }

    protected static List<SalesRepListItem> ApplyKeyword(List<SalesRepListItem> items, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return items;
        }

        var kw = keyword.Trim();
        return items.Where(i =>
                Contains(i.FullName, kw) ||
                Contains(i.Email, kw) ||
                Contains(i.UserName, kw))
            .ToList();
    }

    // Logical sort column (lower-cased) -> key selector. Default (and any unknown column) is FullName.
    private static readonly Dictionary<string, Func<SalesRepListItem, object>> _sortSelectors = new()
    {
        ["email"] = i => i.Email,
        ["organizationscount"] = i => i.OrganizationsCount,
        ["createddate"] = i => i.CreatedDate,
        ["modifieddate"] = i => i.ModifiedDate,
        ["islocked"] = i => i.IsLocked,
        ["fullname"] = i => i.FullName,
    };

    protected static List<SalesRepListItem> ApplySort(List<SalesRepListItem> items, IList<SortInfo> sortInfos)
    {
        var sort = sortInfos?.FirstOrDefault();
        var column = sort?.SortColumn?.ToLowerInvariant();
        var descending = sort?.SortDirection == SortDirection.Descending;

        var selector = column != null && _sortSelectors.TryGetValue(column, out var found)
            ? found
            : _sortSelectors["fullname"];

        return (descending ? items.OrderByDescending(selector) : items.OrderBy(selector)).ToList();
    }

    private static bool Contains(string source, string value)
    {
        return source != null && source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    protected static bool IsAccountLocked(ApplicationUser user)
    {
        return user?.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
    }

    protected class CandidateRow
    {
        public string MemberId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool IsLocked { get; set; }
        public int OrganizationsCount { get; set; }
        public bool HasGlobalSalesRepRole { get; set; }
    }
}
