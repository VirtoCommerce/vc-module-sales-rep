using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
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
/// The union has no single queryable source (roles/accounts live in platform security, memberships in the
/// customer module, and the account↔member link is on the platform side), so the candidate <b>id</b> set is
/// resolved from both sources and unioned in memory — bounded by the (admin) Sales Rep population.
/// Member <b>detail</b> (name/email/dates) is then fetched ONLY for the returned page:
///   - member-backed sorts (name/created/modified) delegate keyword-filtering, sorting and paging to the
///     member search (runs in the DB, returns just the page);
///   - account-backed sorts (email/orgcount/locked) sort the bounded candidate rows in memory and enrich
///     only the page.
/// </summary>
public class SalesRepSearchService : ISalesRepSearchService
{
    private readonly IUserSearchService _userSearchService;
    private readonly IMemberService _memberService;
    private readonly IMemberSearchService _memberSearchService;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;
    private readonly ISalesRepRoleResolver _roleResolver;

    public SalesRepSearchService(
        IUserSearchService userSearchService,
        IMemberService memberService,
        IMemberSearchService memberSearchService,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepRoleResolver roleResolver)
    {
        _userSearchService = userSearchService;
        _memberService = memberService;
        _memberSearchService = memberSearchService;
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

        var rows = await ResolveCandidateRowsAsync(criteria, grantingRoles);
        if (rows.Count == 0)
        {
            return result;
        }

        // Fetch member detail only for the page: DB-side when the sort is member-backed, otherwise sort the
        // bounded rows in memory (their account/count fields are already known) and enrich just the page.
        return IsMemberBackedSort(criteria.SortInfos)
            ? await PageByMemberSortAsync(criteria, rows)
            : await PageByRowSortAsync(criteria, rows);
    }

    /// <summary>Resolve the bounded candidate set (ids + account-side fields) from sources A and B, apply the
    /// id-level filters, and dedupe to one row per member. No member detail is fetched here.</summary>
    protected virtual async Task<List<CandidateRow>> ResolveCandidateRowsAsync(SalesRepSearchCriteria criteria, IList<Role> grantingRoles)
    {
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

        return BuildRows(criteria, candidateUserIds, usersById, totalCounts, globalRoleUserIds);
    }

    // Sort columns backed by an account/aggregate field already on the candidate row (so they can't be a
    // member-DB sort). Everything else — including the default and sort-by-name — is member-backed.
    private static readonly HashSet<string> _rowBackedSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "email",
        "organizationscount",
        "islocked",
    };

    protected static bool IsMemberBackedSort(IList<SortInfo> sortInfos)
    {
        var column = sortInfos?.FirstOrDefault()?.SortColumn;
        return string.IsNullOrEmpty(column) || !_rowBackedSortColumns.Contains(column);
    }

    /// <summary>Member-backed sort: let the member search filter (keyword), sort and page in the database over
    /// the candidate member ids, returning only the requested page.</summary>
    protected virtual async Task<SalesRepSearchResult> PageByMemberSortAsync(SalesRepSearchCriteria criteria, List<CandidateRow> rows)
    {
        var rowsByMemberId = rows.ToDictionary(r => r.MemberId, r => r);
        var take = criteria.Take <= 0 ? rows.Count : criteria.Take;

        var memberSearch = await _memberSearchService.SearchMembersAsync(new MembersSearchCriteria
        {
            ObjectIds = rowsByMemberId.Keys.ToArray(),
            Keyword = criteria.Keyword,
            // Candidates are contacts that ARE org members; disable the "root members only" default that would
            // otherwise exclude them.
            RootMembersOnly = false,
            ResponseGroup = MemberResponseGroup.WithEmails.ToString(),
            Sort = BuildMemberSort(criteria.SortInfos),
            Skip = criteria.Skip,
            Take = take,
        });

        var items = memberSearch.Results
            .Where(m => rowsByMemberId.ContainsKey(m.Id))
            .Select(m => BuildItem(rowsByMemberId[m.Id], m))
            .ToList();

        return new SalesRepSearchResult
        {
            TotalCount = memberSearch.TotalCount,
            Results = items,
        };
    }

    /// <summary>Account-backed sort (email/orgcount/locked): sort the bounded candidate rows in memory and
    /// enrich only the page. A keyword still needs member name/email, so it is resolved via the member search
    /// over the candidate ids (bounded) before sorting.</summary>
    protected virtual async Task<SalesRepSearchResult> PageByRowSortAsync(SalesRepSearchCriteria criteria, List<CandidateRow> rows)
    {
        Dictionary<string, Member> keywordMatches = null;
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var matched = await _memberSearchService.SearchMembersAsync(new MembersSearchCriteria
            {
                ObjectIds = rows.Select(r => r.MemberId).ToArray(),
                Keyword = criteria.Keyword,
                RootMembersOnly = false,
                ResponseGroup = MemberResponseGroup.WithEmails.ToString(),
                Take = rows.Count,
            });
            keywordMatches = matched.Results.ToDictionary(m => m.Id, m => m);
            rows = rows.Where(r => keywordMatches.ContainsKey(r.MemberId)).ToList();
        }

        var ordered = OrderRows(rows, criteria.SortInfos);
        var take = criteria.Take <= 0 ? ordered.Count : criteria.Take;
        var pageRows = ordered.Skip(criteria.Skip).Take(take).ToList();

        var items = await BuildItemsForPageAsync(pageRows, keywordMatches);

        return new SalesRepSearchResult
        {
            TotalCount = ordered.Count,
            Results = items,
        };
    }

    protected static List<CandidateRow> OrderRows(List<CandidateRow> rows, IList<SortInfo> sortInfos)
    {
        var sort = sortInfos?.FirstOrDefault();
        var descending = sort?.SortDirection == SortDirection.Descending;

        // Only reached for the row-backed columns; default arm is "email".
        Func<CandidateRow, object> selector = sort?.SortColumn?.ToLowerInvariant() switch
        {
            "organizationscount" => r => r.OrganizationsCount,
            "islocked" => r => r.IsLocked,
            _ => r => r.Email,
        };

        return (descending ? rows.OrderByDescending(selector) : rows.OrderBy(selector)).ToList();
    }

    /// <summary>Map the Sales Rep sort token to a Member DB column (default and sort-by-name → Name).</summary>
    protected static string BuildMemberSort(IList<SortInfo> sortInfos)
    {
        var sort = sortInfos?.FirstOrDefault();
        var direction = sort?.SortDirection == SortDirection.Descending ? "desc" : "asc";
        var column = sort?.SortColumn?.ToLowerInvariant() switch
        {
            "createddate" => "CreatedDate",
            "modifieddate" => "ModifiedDate",
            _ => "Name",
        };

        return $"{column}:{direction}";
    }

    protected virtual async Task<List<SalesRepListItem>> BuildItemsForPageAsync(List<CandidateRow> pageRows, Dictionary<string, Member> known)
    {
        if (pageRows.Count == 0)
        {
            return [];
        }

        // Reuse members already loaded by the keyword pass; otherwise fetch only the page's members.
        var membersById = known ?? (await _memberService.GetByIdsAsync(
            pageRows.Select(r => r.MemberId).ToArray(),
            MemberResponseGroup.WithEmails.ToString())).ToDictionary(m => m.Id, m => m);

        return pageRows.Select(r =>
        {
            membersById.TryGetValue(r.MemberId, out var member);
            return BuildItem(r, member);
        }).ToList();
    }

    protected static SalesRepListItem BuildItem(CandidateRow row, Member member)
    {
        var contact = member as Contact;
        return new SalesRepListItem
        {
            Id = row.MemberId,
            UserId = row.UserId,
            UserName = row.UserName,
            FullName = contact?.FullName ?? member?.Name,
            Email = member?.Emails?.FirstOrDefault() ?? row.Email,
            OrganizationsCount = row.OrganizationsCount,
            IsLocked = row.IsLocked,
            HasGlobalSalesRepRole = row.HasGlobalSalesRepRole,
            CreatedDate = member?.CreatedDate,
            ModifiedDate = member?.ModifiedDate,
        };
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
