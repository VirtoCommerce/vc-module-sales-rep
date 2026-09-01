namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

/// <summary>
/// A command that acts on behalf of the signed-in platform user. <see cref="SalesRepCommandBuilder{TCommand, TResult,
/// TCommandGraphType, TResultGraphType}"/> stamps the caller onto it, so the identity can never be taken from input.
/// Opt in rather than stamp every command: a command whose <c>UserId</c> names someone else must not be overwritten.
/// </summary>
public interface ISalesRepUserCommand
{
    string UserId { get; set; }
}
