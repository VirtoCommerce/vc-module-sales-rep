using System;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public interface ISalesRepTaskInput
{
    string Name { get; set; }

    string Description { get; set; }

    string Type { get; set; }

    string Priority { get; set; }

    DateTime? DueDate { get; set; }
}
