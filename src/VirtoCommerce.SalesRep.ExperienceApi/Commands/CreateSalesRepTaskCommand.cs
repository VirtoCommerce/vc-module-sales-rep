using System;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class CreateSalesRepTaskCommand : ICommand<SalesRepTask>, ISalesRepTaskInput, ISalesRepMemberCommand
{
    public string Name { get; set; }

    public string Description { get; set; }

    public string Type { get; set; }

    public string Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public string UserId { get; set; }

    public string MemberId { get; set; }
}
