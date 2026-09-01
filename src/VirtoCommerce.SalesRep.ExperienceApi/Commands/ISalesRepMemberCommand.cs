namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

/// <summary>
/// A command that also acts on behalf of the caller's contact record. Ownership of a task keys on the member, not the
/// platform user, so anything that reads or writes one needs both stamped.
/// </summary>
public interface ISalesRepMemberCommand : ISalesRepUserCommand
{
    string MemberId { get; set; }
}
