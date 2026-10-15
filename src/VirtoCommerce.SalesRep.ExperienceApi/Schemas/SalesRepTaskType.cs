using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepTaskType : ExtendableGraphType<SalesRepTask>
{
    public SalesRepTaskType()
    {
        Name = "SalesRepTask";

        Field(x => x.Id, nullable: false).Description("Task id.");
        Field(x => x.Name, nullable: false).Description("Task title.");
        Field(x => x.Description, nullable: true).Description("Free-text notes.");
        Field(x => x.Type, nullable: true).Description("Task type - free text, typically one of the values salesRepTaskTypes offers.");
        Field(x => x.Priority, nullable: true).Description("Priority name: Lowest, Low, Normal, High or Highest.");
        Field(x => x.DueDate, nullable: true).Description("When the task is due, as an instant.");
        Field(x => x.IsActive, nullable: false).Description("False once the task has been completed or cancelled.");
        Field(x => x.Completed, nullable: true).Description("True when finished as done. A reopened task also reports false, so read it with isActive: inactive and not true = cancelled.");
        Field(x => x.CreatedDate, nullable: false).Description("When the task was created.");
        Field(x => x.ModifiedDate, nullable: true).Description("When the task was last changed.");

        Field<StringGraphType>("customerId")
            .Description("Organization id of the customer the task is about. Null when there is none, or once the caller no longer serves that customer.")
            .Resolve(context => LoadCustomer(context).Then(customer => customer?.Id));
        Field<StringGraphType>("customerName")
            .Description("Name of that customer, under the same rule as customerId.")
            .Resolve(context => LoadCustomer(context).Then(customer => customer?.Name));
    }

    // Only customers the caller still serves: a task outlives the membership that let the rep link it.
    protected virtual IDataLoaderResult<Member> LoadCustomer(IResolveFieldContext<SalesRepTask> context)
    {
        var customerId = context.Source.CustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            return new DataLoaderResult<Member>((Member)null);
        }

        var userId = context.GetCurrentUserId();
        var services = context.RequestServices;

        var loader = context.GetRequiredService<IDataLoaderContextAccessor>().Context.GetOrAddBatchLoader<string, Member>(
            $"{nameof(SalesRepTaskType)}.CustomerById",
            async customerIds =>
            {
                // All of the caller's customers rather than these ids: authorizing the request has already loaded that list.
                var servedIds = await services.GetRequiredService<ISalesRepOrganizationAccessService>().GetServedOrganizationIdsAsync(userId);
                var ids = customerIds.Intersect(servedIds, StringComparer.OrdinalIgnoreCase).ToArray();
                if (ids.Length == 0)
                {
                    return new Dictionary<string, Member>();
                }

                var customers = await services.GetRequiredService<IMemberService>().GetByIdsAsync(ids, nameof(MemberResponseGroup.Default), [nameof(Organization)]);

                return customers.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            });

        return loader.LoadAsync(customerId);
    }
}
