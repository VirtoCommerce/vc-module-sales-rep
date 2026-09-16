using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.TaskManagement.Core;
using VirtoCommerce.TaskManagement.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskTypesQueryHandler : SalesRepTaskQueryHandlerBase, IQueryHandler<SalesRepTaskTypesQuery, IList<string>>
{
    private readonly ISettingsManager _settingsManager;

    public SalesRepTaskTypesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskSearchService> taskSearchService,
        ISettingsManager settingsManager)
        : base(organizationAccessService, taskSearchService)
    {
        _settingsManager = settingsManager;
    }

    public virtual async Task<IList<string>> Handle(SalesRepTaskTypesQuery request, CancellationToken cancellationToken)
    {
        if (!await CanReadAsync(request.UserId))
        {
            return [];
        }

        // A dictionary setting keeps its values in AllowedValues; the descriptor's list is only the seed.
        var setting = await _settingsManager.GetObjectSettingAsync(ModuleConstants.Settings.General.TaskTypes.Name);

        return setting?.AllowedValues?.OfType<string>().ToList() ?? [];
    }
}
