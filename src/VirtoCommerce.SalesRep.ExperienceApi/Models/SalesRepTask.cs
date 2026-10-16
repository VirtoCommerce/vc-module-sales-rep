using System;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.TaskManagement.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepTask
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Type { get; set; }

    public string Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public bool IsActive { get; set; }

    public bool? Completed { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    // The stored link, served or not. The schema's customerId shows it only while the caller serves that customer.
    public string CustomerId { get; set; }

    public static SalesRepTask FromWorkTask(WorkTask task)
    {
        var result = AbstractTypeFactory<SalesRepTask>.TryCreateInstance();
        result.MapFrom(task);
        return result;
    }

    protected virtual void MapFrom(WorkTask task)
    {
        Id = task.Id;
        Name = task.Name;
        Description = task.Description;
        Type = task.Type;
        Priority = task.Priority.ToString();
        DueDate = task.DueDate;
        IsActive = task.IsActive;
        Completed = task.Completed;
        CreatedDate = task.CreatedDate;
        ModifiedDate = task.ModifiedDate;
        CustomerId = IsCustomerLink(task) ? task.ObjectId : null;
    }

    /// <summary>
    /// Whether the task's object link (ObjectType/ObjectId) is this module's customer link. The read above and the
    /// write in SalesRepTaskCommandHandlerBase both decide by this pair, so they agree on whose link a task carries.
    /// </summary>
    internal static bool IsCustomerLink(WorkTask task)
    {
        return task.ObjectType.EqualsIgnoreCase(ModuleConstants.Tasks.CustomerObjectType);
    }

    /// <summary>
    /// Whether the task links a record of another kind, a workflow task about an order, say: any link data that is not
    /// a customer link, an ObjectId without an ObjectType included. Such a link is never shown, cleared or replaced.
    /// </summary>
    internal static bool IsLinkedToAnotherRecord(WorkTask task)
    {
        return (!string.IsNullOrEmpty(task.ObjectType) || !string.IsNullOrEmpty(task.ObjectId)) && !IsCustomerLink(task);
    }
}
