using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepListItem : IEntity
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }

    public int OrganizationsCount { get; set; }

    public bool IsLocked { get; set; }
    public bool HasGlobalSalesRepRole { get; set; }

    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
