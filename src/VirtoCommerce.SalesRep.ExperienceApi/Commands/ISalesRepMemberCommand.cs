namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

// Task ownership keys on the member, not the platform user, so a task command needs both stamped.
public interface ISalesRepMemberCommand : ISalesRepUserCommand
{
    string MemberId { get; set; }
}
