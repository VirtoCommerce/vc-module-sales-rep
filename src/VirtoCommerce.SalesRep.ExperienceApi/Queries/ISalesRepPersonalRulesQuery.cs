namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// A rules discovery query whose vocabulary belongs to the caller rather than to the organizations they serve, so it
/// is not narrowed by membership: a task belongs to a person, and the rules a rep is offered depend only on their
/// being a rep. The counterpart of <see cref="ISalesRepScopedRulesQuery"/>, which narrows the other way.
/// </summary>
public interface ISalesRepPersonalRulesQuery;
