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
    private readonly IOrganizationMembershipService _membershipService;
    private readonly ISalesRepRoleResolver _roleResolver;

    public SalesRepSearchService(
        IUserSearchService userSearchService,
        IMemberService memberService,
        IOrganizationMembershipService membershipService,
        ISalesRepRoleResolver roleResolver)
    {
        _userSearchService = userSearchService;
        _memberService = memberService;
        _membershipService = membershipService;
        _roleResolver = roleResolver;
    }

    public virtual async Task<SalesRepSearchResult> SearchAsync(SalesRepSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = new SalesRepSearchResult();

        var grantingRoles = await _roleResolver.GetRolesGrantingAccessAsync();
        if (grantingRoles.Count == 0)
        {
            return result;
        }

        var grantingRoleIds = grantingRoles.Select(r => r.Id).ToArray();
        var grantingRoleNames = grantingRoles.Select(r => r.Name).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        var orgScoped = !string.IsNullOrEmpty(criteria.OrganizationId);

        // Source A: users with a global role granting the permission (skipped for an org-scoped view).
        var usersById = new Dictionary<string, ApplicationUser>();
        var globalRoleUserIds = new HashSet<string>();
        if (!orgScoped && grantingRoleNames.Length > 0)
        {
            var globalUsers = (await _userSearchService.SearchUsersAsync(new UserSearchCriteria
            {
                Roles = grantingRoleNames,
                Take = int.MaxValue,
            })).Results;

            foreach (var user in globalUsers)
            {
                usersById[user.Id] = user;
                globalRoleUserIds.Add(user.Id);
            }
        }

        // Source B: per-org reps (DB-side aggregate). When org-scoped, candidates are users serving that org;
        // total org counts are resolved separately so the displayed count reflects all served organizations.
        var orgIds = orgScoped ? new[] { criteria.OrganizationId } : null;
        var scopedCounts = await _membershipService.GetOrganizationCountsByUserAsync(grantingRoleIds, orgIds);
        var totalCounts = orgScoped
            ? await _membershipService.GetOrganizationCountsByUserAsync(grantingRoleIds, organizationIds: null, userIds: scopedCounts.Keys.ToArray())
            : scopedCounts;

        var candidateUserIds = new HashSet<string>(globalRoleUserIds);
        foreach (var userId in scopedCounts.Keys)
        {
            candidateUserIds.Add(userId);
        }

        await LoadMissingUsersAsync(usersById, candidateUserIds);

        // Build candidate rows + apply id-level filters.
        var rows = new List<CandidateRow>();
        foreach (var userId in candidateUserIds)
        {
            if (!usersById.TryGetValue(userId, out var user) || string.IsNullOrEmpty(user.MemberId))
            {
                continue;
            }

            var orgCount = totalCounts.TryGetValue(userId, out var count) ? count : 0;
            var isLocked = IsLocked(user);

            if (criteria.OnlyBlocked && !isLocked)
            {
                continue;
            }
            if (criteria.OnlyUnassigned && orgCount > 0)
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
        rows = rows.GroupBy(r => r.MemberId).Select(g => g.First()).ToList();

        var items = await EnrichAsync(rows);
        items = ApplyKeyword(items, criteria.Keyword);
        items = ApplySort(items, criteria.SortInfos);

        result.TotalCount = items.Count;
        var take = criteria.Take <= 0 ? items.Count : criteria.Take;
        result.Results = items.Skip(criteria.Skip).Take(take).ToList();

        return result;
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

    protected static List<SalesRepListItem> ApplySort(List<SalesRepListItem> items, IList<SortInfo> sortInfos)
    {
        var sort = sortInfos?.FirstOrDefault();
        var column = sort?.SortColumn?.ToLowerInvariant();
        var descending = sort?.SortDirection == SortDirection.Descending;

        IOrderedEnumerable<SalesRepListItem> ordered = column switch
        {
            "email" => Order(items, i => i.Email, descending),
            "organizationscount" => Order(items, i => i.OrganizationsCount, descending),
            "createddate" => Order(items, i => i.CreatedDate, descending),
            "modifieddate" => Order(items, i => i.ModifiedDate, descending),
            "islocked" => Order(items, i => i.IsLocked, descending),
            _ => Order(items, i => i.FullName, descending),
        };

        return ordered.ToList();
    }

    private static IOrderedEnumerable<SalesRepListItem> Order<TKey>(IEnumerable<SalesRepListItem> items, Func<SalesRepListItem, TKey> selector, bool descending)
    {
        return descending ? items.OrderByDescending(selector) : items.OrderBy(selector);
    }

    private static bool Contains(string source, string value)
    {
        return source != null && source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    protected static bool IsLocked(ApplicationUser user)
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
