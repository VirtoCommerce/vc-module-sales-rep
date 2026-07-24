using VirtoCommerce.SalesRep.ExperienceApi.Commands;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepCommunicationResponseGroupParser
{
    string GetResponseGroup(SendCustomerCommunicationCommand command);
}
