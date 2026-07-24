using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCommunicationResult
{
    public bool Succeeded => PushSent || EmailSent;

    public bool PushSent { get; set; }

    public bool EmailSent { get; set; }

    public IList<string> Warnings { get; set; } = [];
}
