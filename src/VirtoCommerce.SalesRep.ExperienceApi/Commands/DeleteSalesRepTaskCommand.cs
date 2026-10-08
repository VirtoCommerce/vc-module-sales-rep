using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class DeleteSalesRepTaskCommand : ICommand<bool>, ISalesRepMemberCommand
{
    public string Id { get; set; }

    public string UserId { get; set; }

    public string MemberId { get; set; }
}
