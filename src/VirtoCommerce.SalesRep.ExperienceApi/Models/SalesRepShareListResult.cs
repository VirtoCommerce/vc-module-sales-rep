using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepShareListResult
{
    public bool Succeeded { get; set; }

    public string ListId { get; set; }

    public string SharingKey { get; set; }

    public string SharingUrl { get; set; }

    public IList<string> SharedWithOrganizationIds { get; set; } = [];

    public IList<string> Warnings { get; set; } = [];
}
