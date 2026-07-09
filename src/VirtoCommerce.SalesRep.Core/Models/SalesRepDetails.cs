using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using VirtoCommerce.CustomerModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Rich aggregate describing a Sales Rep: the underlying Contact (member) profile,
/// the login account, and the organizations the rep serves.
/// </summary>
public class SalesRepDetails
{
    /// <summary>Contact member id (canonical id of a Sales Rep).</summary>
    public string Id { get; set; }

    /// <summary>Security account (ApplicationUser) id.</summary>
    public string UserId { get; set; }

    public string UserName { get; set; }

    // Profile (Contact)
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

    // Account
    public string StoreId { get; set; }

    /// <summary>Write-only. When set on create/update, (re)sets the account password. Accepted on input but
    /// never serialized back in a response (it is never populated when reading a rep).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Password { get; set; }

    /// <summary>Account-level lockout (blocked sign-in everywhere).</summary>
    public bool IsLocked { get; set; }

    /// <summary>True when the account holds a global role granting "sales-rep:access".</summary>
    public bool HasGlobalSalesRepRole { get; set; }

    /// <summary>
    /// Id of the role (granting "sales-rep:access") assigned to this rep — applied both as the global role
    /// and as the per-organization membership role. Changing it on edit re-points all existing assignments.
    /// </summary>
    public string RoleId { get; set; }

    public string RoleName { get; set; }

    /// <summary>Organizations the rep serves (per-org role granting "sales-rep:access").</summary>
    public IList<SalesRepOrganization> Organizations { get; set; } = [];
}
