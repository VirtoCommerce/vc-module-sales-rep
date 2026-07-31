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

    public virtual Task<SalesRepSearchResult> SearchAsync(SalesRepSearchCriteria criteria, bool clone = true)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return SearchInternalAsync(criteria);
    }

    protected virtual async Task<SalesRepSearchResult> SearchInternalAsync(SalesRepSearchCriteria criteria)
    {
        var result = AbstractTypeFactory<SalesRepSearchResult>.TryCreateInstance();

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

        return IsMemberBackedSort(criteria.SortInfos)
            ? await PageByMemberSortAsync(criteria, rows)
            : await PageByRowSortAsync(criteria, rows);
    }

    protected virtual async Task<List<CandidateRow>> ResolveCandidateRowsAsync(SalesRepSearchCriteria criteria, IList<Role> grantingRoles)
    {
        var grantingRoleIds = grantingRoles.Select(r => r.Id).ToArray();
        var orgScoped = !string.IsNullOrEmpty(criteria.OrganizationId);

        var usersById = new Dictionary<string, ApplicationUser>();
        HashSet<string> globalRoleUserIds = orgScoped ? [] : await LoadGlobalRoleUsersAsync(grantingRoles, usersById);

        string[] orgIds = orgScoped ? [criteria.OrganizationId] : null;
        var scopedCriteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
        scopedCriteria.RoleIds = grantingRoleIds;
        scopedCriteria.OrganizationIds = orgIds;
        var scopedCounts = await _membershipSearchService.GetCountsByUserAsync(scopedCriteria);

        IDictionary<string, int> totalCounts;
        if (orgScoped)
        {
            var totalCriteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
            totalCriteria.RoleIds = grantingRoleIds;
            totalCriteria.UserIds = scopedCounts.Keys.ToArray();
            totalCounts = await _membershipSearchService.GetCountsByUserAsync(totalCriteria);
        }
        else
        {
            totalCounts = scopedCounts;
        }

        HashSet<string> candidateUserIds = [.. globalRoleUserIds];
        candidateUserIds.UnionWith(scopedCounts.Keys);

        await LoadMissingUsersAsync(usersById, candidateUserIds);

        return BuildRows(criteria, candidateUserIds, usersById, totalCounts, globalRoleUserIds);
    }

    private const string EmailSortColumn = "email";
    private const string OrganizationsCountSortColumn = "organizationscount";
    private const string IsLockedSortColumn = "islocked";

    private static readonly HashSet<string> _rowBackedSortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        EmailSortColumn,
        OrganizationsCountSortColumn,
        IsLockedSortColumn,
    };

    protected static bool IsMemberBackedSort(IList<SortInfo> sortInfos)
    {
        var column = sortInfos?.FirstOrDefault()?.SortColumn;
        return string.IsNullOrEmpty(column) || !_rowBackedSortColumns.Contains(column);
    }

    protected virtual async Task<SalesRepSearchResult> PageByMemberSortAsync(SalesRepSearchCriteria criteria, List<CandidateRow> rows)
    {
        var rowsByMemberId = rows.ToDictionary(r => r.MemberId, r => r);
        var take = Math.Max(criteria.Take, 0);

        var memberCriteria = AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
        memberCriteria.ObjectIds = rowsByMemberId.Keys.ToArray();
        memberCriteria.Keyword = criteria.Keyword;
        memberCriteria.RootMembersOnly = false;
        memberCriteria.ResponseGroup = nameof(MemberResponseGroup.WithEmails);
        memberCriteria.Sort = BuildMemberSort(criteria.SortInfos);
        memberCriteria.Skip = criteria.Skip;
        memberCriteria.Take = take;
        var memberSearch = await _memberSearchService.SearchMembersAsync(memberCriteria);

        var items = memberSearch.Results
            .Where(m => rowsByMemberId.ContainsKey(m.Id))
            .Select(m => BuildItem(rowsByMemberId[m.Id], m))
            .ToList();

        var pageResult = AbstractTypeFactory<SalesRepSearchResult>.TryCreateInstance();
        pageResult.TotalCount = memberSearch.TotalCount;
        pageResult.Results = items;
        return pageResult;
    }

    protected virtual async Task<SalesRepSearchResult> PageByRowSortAsync(SalesRepSearchCriteria criteria, List<CandidateRow> rows)
    {
        Dictionary<string, Member> keywordMatches = null;
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var matchedCriteria = AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
            matchedCriteria.ObjectIds = rows.Select(r => r.MemberId).ToArray();
            matchedCriteria.Keyword = criteria.Keyword;
            matchedCriteria.RootMembersOnly = false;
            matchedCriteria.ResponseGroup = nameof(MemberResponseGroup.WithEmails);
            matchedCriteria.Take = rows.Count;
            var matched = await _memberSearchService.SearchMembersAsync(matchedCriteria);
            keywordMatches = matched.Results.ToDictionary(m => m.Id, m => m);
            rows = rows.Where(r => keywordMatches.ContainsKey(r.MemberId)).ToList();
        }

        var ordered = OrderRows(rows, criteria.SortInfos);
        var take = Math.Max(criteria.Take, 0);
        var pageRows = ordered.Skip(criteria.Skip).Take(take).ToList();

        var items = await BuildItemsForPageAsync(pageRows, keywordMatches);

        var rowResult = AbstractTypeFactory<SalesRepSearchResult>.TryCreateInstance();
        rowResult.TotalCount = ordered.Count;
        rowResult.Results = items;
        return rowResult;
    }

    protected static List<CandidateRow> OrderRows(List<CandidateRow> rows, IList<SortInfo> sortInfos)
    {
        var sort = sortInfos?.FirstOrDefault();
        var descending = sort?.SortDirection == SortDirection.Descending;

        Func<CandidateRow, object> selector = sort?.SortColumn?.ToLowerInvariant() switch
        {
            OrganizationsCountSortColumn => r => r.OrganizationsCount,
            IsLockedSortColumn => r => r.IsLocked,
            _ => r => r.Email,
        };

        return (descending ? rows.OrderByDescending(selector) : rows.OrderBy(selector)).ToList();
    }

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

        var membersById = known ?? (await _memberService.GetByIdsAsync(
            pageRows.Select(r => r.MemberId).ToArray(),
            nameof(MemberResponseGroup.WithEmails))).ToDictionary(m => m.Id, m => m);

        return pageRows.Select(r =>
        {
            membersById.TryGetValue(r.MemberId, out var member);
            return BuildItem(r, member);
        }).ToList();
    }

    protected static SalesRepListItem BuildItem(CandidateRow row, Member member)
    {
        var contact = member as Contact;
        var item = AbstractTypeFactory<SalesRepListItem>.TryCreateInstance();
        item.Id = row.MemberId;
        item.UserId = row.UserId;
        item.UserName = row.UserName;
        item.FullName = contact?.FullName ?? member?.Name;
        item.Email = member?.Emails?.FirstOrDefault() ?? row.Email;
        item.OrganizationsCount = row.OrganizationsCount;
        item.IsLocked = row.IsLocked;
        item.HasGlobalSalesRepRole = row.HasGlobalSalesRepRole;
        item.CreatedDate = member?.CreatedDate;
        item.ModifiedDate = member?.ModifiedDate;
        return item;
    }

    protected virtual async Task<HashSet<string>> LoadGlobalRoleUsersAsync(IList<Role> grantingRoles, Dictionary<string, ApplicationUser> usersById)
    {
        HashSet<string> ids = [];
        var roleNames = grantingRoles.Select(r => r.Name).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (roleNames.Length == 0)
        {
            return ids;
        }

        var globalUserCriteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        globalUserCriteria.Roles = roleNames;
        var globalUsers = await _userSearchService.SearchAllAsync(globalUserCriteria);

        foreach (var user in globalUsers)
        {
            usersById[user.Id] = user;
            ids.Add(user.Id);
        }

        return ids;
    }

    protected virtual List<CandidateRow> BuildRows(
        SalesRepSearchCriteria criteria,
        HashSet<string> candidateUserIds,
        Dictionary<string, ApplicationUser> usersById,
        IDictionary<string, int> totalCounts,
        HashSet<string> globalRoleUserIds)
    {
        List<CandidateRow> rows = [];
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

        var missingCriteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        missingCriteria.ObjectIds = missing;
        missingCriteria.Take = missing.Count;
        var loaded = (await _userSearchService.SearchUsersAsync(missingCriteria)).Results;

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
