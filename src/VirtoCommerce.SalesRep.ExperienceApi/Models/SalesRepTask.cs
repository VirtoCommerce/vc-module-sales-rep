using System;
using VirtoCommerce.Platform.Core.Common;
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
    }
}
