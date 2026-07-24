using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SaveLayoutCommandHandler(ILayoutService layoutService)
    : IRequestHandler<SaveLayoutCommand, Layout>
{
    // Full-document replace: the storefront always holds the whole layout, so we persist it verbatim
    // (keyed on the caller's own user id, so a rep can only read/write their own layout). Scope is required
    // by the NonNull input field and validated by the service — same as the query path.
    public virtual async Task<Layout> Handle(SaveLayoutCommand request, CancellationToken cancellationToken)
    {
        var layout = AbstractTypeFactory<Layout>.TryCreateInstance();
        layout.SchemaVersion = request.SchemaVersion;
        layout.Regions = request.Regions;

        await layoutService.SaveLayoutAsync(request.UserId, request.Scope, layout, request.StoreId);

        return layout;
    }
}
