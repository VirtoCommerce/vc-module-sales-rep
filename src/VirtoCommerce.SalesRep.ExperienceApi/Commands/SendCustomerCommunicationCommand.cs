using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SendCustomerCommunicationCommand : ICommand<SalesRepCommunicationResult>
{
    public string OrganizationId { get; set; }

    public bool SendPush { get; set; }

    public bool SendEmail { get; set; }

    public string Title { get; set; }

    public string Message { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string UserId { get; set; }
}
