using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using VirtoCommerce.CustomerModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDetails
{
    public string Id { get; set; }

    public string UserId { get; set; }

    public string UserName { get; set; }

    public string Salutation { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string LastName { get; set; }
    public string FullName { get; set; }
    public DateTime? BirthDate { get; set; }
    public string TimeZone { get; set; }
    public string DefaultLanguage { get; set; }
    public string CurrencyCode { get; set; }
    public string About { get; set; }
    public string PhotoUrl { get; set; }
    public string Status { get; set; }

    public IList<string> Emails { get; set; } = [];
    public IList<string> Phones { get; set; } = [];
    public IList<Address> Addresses { get; set; } = [];

    public string StoreId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Password { get; set; }

    public bool IsLocked { get; set; }

    public bool HasGlobalSalesRepRole { get; set; }

    public string RoleId { get; set; }

    public string RoleName { get; set; }

    public IList<SalesRepOrganization> Organizations { get; set; } = [];
}
