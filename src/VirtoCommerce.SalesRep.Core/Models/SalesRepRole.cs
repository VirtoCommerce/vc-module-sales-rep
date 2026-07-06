namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// A role that grants the "sales-rep:access" permission — selectable as a Sales Rep's role
/// (applied both as the global role and the per-organization membership role).
/// </summary>
public class SalesRepRole
{
    public string Id { get; set; }
    public string Name { get; set; }
}
