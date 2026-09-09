using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SaveLayoutCommandBuilder
    : SalesRepCommandBuilder<SaveLayoutCommand, Layout, InputSalesRepLayoutType, SalesRepLayoutType>
{
    protected override string Name => "saveSalesRepLayout";

    public SaveLayoutCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
