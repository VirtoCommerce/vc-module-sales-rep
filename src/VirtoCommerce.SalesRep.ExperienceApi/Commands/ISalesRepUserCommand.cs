namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

// Opt-in: SalesRepCommandBuilder stamps the caller onto it. Not blanket, so a command whose UserId names
// someone else is never overwritten.
public interface ISalesRepUserCommand
{
    string UserId { get; set; }
}
