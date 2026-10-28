using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.TaskManagement.Core.Models;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// A filter rule may restrict the priorities or the linked records itself. The page's filters narrow what it left, the way
/// the rules narrow the due-date window they are given, rather than replace it.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepTasksQueryHandlerFilterTests
{
    [Fact]
    public void Priorities_NarrowTheOnesTheRuleAllows()
    {
        var criteria = new WorkTaskSearchCriteria { Priorities = ["High", "Highest"] };

        var narrowed = new TestHandler().NarrowPriorities(Query(priorities: ["low", "HIGH"]), criteria);

        narrowed.Priorities.Should().Equal("High");
    }

    [Fact]
    public void Priorities_OutsideTheOneTheRuleAllows_MatchNothing()
    {
        var criteria = new WorkTaskSearchCriteria { Priority = nameof(TaskPriority.High) };

        new TestHandler().NarrowPriorities(Query(priorities: ["Low"]), criteria).Should().BeNull();
    }

    [Fact]
    public async Task Customers_NarrowTheRecordsTheRuleAllows()
    {
        var criteria = new WorkTaskSearchCriteria
        {
            LinkedObjectType = ModuleConstants.Tasks.CustomerObjectType,
            LinkedObjectIds = ["org-a", "org-b"],
        };

        var narrowed = await new TestHandler(served: ["org-a", "org-b", "org-c"]).NarrowCustomersAsync(Query(customerIds: ["org-b", "org-c"]), criteria);

        narrowed.LinkedObjectIds.Should().Equal("org-b");
    }

    [Fact]
    public async Task Customers_UnderAnotherLinkedRecordType_MatchNothing()
    {
        var criteria = new WorkTaskSearchCriteria { LinkedObjectType = "Order" };

        var narrowed = await new TestHandler(served: ["org-a"]).NarrowCustomersAsync(Query(customerIds: ["org-a"]), criteria);

        narrowed.Should().BeNull();
    }

    private static SalesRepTasksQuery Query(IList<string> priorities = null, IList<string> customerIds = null)
    {
        return new SalesRepTasksQuery { UserId = "rep-user", Priorities = priorities, CustomerIds = customerIds };
    }

    // Exposes the two filter steps. Only the customer step reads a service: the customers the rep serves.
    private sealed class TestHandler(IList<string> served = null)
        : SalesRepTasksQueryHandler(new ServedOrganizations(served ?? []), taskSearchService: null, filterRuleResolver: null, sortRuleResolver: null)
    {
        public WorkTaskSearchCriteria NarrowPriorities(SalesRepTasksQuery request, WorkTaskSearchCriteria criteria)
        {
            return ApplyPriorities(request, criteria);
        }

        public Task<WorkTaskSearchCriteria> NarrowCustomersAsync(SalesRepTasksQuery request, WorkTaskSearchCriteria criteria)
        {
            return ApplyCustomersAsync(request, criteria);
        }
    }

    private sealed class ServedOrganizations(IList<string> served) : ISalesRepOrganizationAccessService
    {
        public Task<IList<string>> GetServedOrganizationIdsAsync(string userId) => Task.FromResult(served);

        public Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(IList<string> userIds = null, IList<string> organizationIds = null) => throw new NotSupportedException();

        public Task<bool> ServesOrganizationAsync(string userId, string organizationId) => throw new NotSupportedException();

        public Task<bool> ServesAllOrganizationsAsync(string userId, IList<string> organizationIds) => throw new NotSupportedException();

        public Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId) => throw new NotSupportedException();

        public Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId) => throw new NotSupportedException();
    }
}
