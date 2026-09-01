using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class ChangeSalesRepTaskStatusCommand : ICommand<SalesRepTask>
{
    public string Id { get; set; }

    public bool Completed { get; set; }

    public string UserId { get; set; }

    public string MemberId { get; set; }
}
