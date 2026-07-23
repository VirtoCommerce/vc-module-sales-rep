using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCommunicationResult
{
    /// <summary>True when at least one requested channel was accepted for delivery.</summary>
    public bool Succeeded => PushSent || EmailSent;

    public bool PushSent { get; set; }

    public bool EmailSent { get; set; }

    /// <summary>
    /// Stable outcome codes (see <c>ModuleConstants.Communication.Warnings</c>) explaining any channel that
    /// did not deliver. Empty on full success. The storefront maps each code to a localized message.
    /// </summary>
    public IList<string> Warnings { get; set; } = [];
}
