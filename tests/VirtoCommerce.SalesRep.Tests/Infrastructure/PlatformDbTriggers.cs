using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.ChangeLog;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Data.ChangeLog;
using VirtoCommerce.Platform.Data.Extensions;
using VirtoCommerce.Platform.Security;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// The platform's audit stamping, added by its own <c>UseDbTriggers</c> as Platform.Web's Startup adds it: an inserted
/// row gets CreatedDate = ModifiedDate = now and the current user's name, an updated row keeps its created stamp and
/// gets a new ModifiedDate and name. Without it a list saved through the module would keep the model's empty dates,
/// and the cleanup, which reads ModifiedDate, would see lists no deployment ever has.
/// </summary>
/// <remarks>
/// The triggers are process-wide statics that resolve their services from the provider they were added with, so they
/// are added once, at assembly load, over a provider of their own holding the services Startup gives them: the
/// platform's user name resolver (the request's user, the platform's "unknown" outside a request) and its last-changes
/// notifier.
/// </remarks>
internal static class PlatformDbTriggers
{
    [ModuleInitializer]
    internal static void Register()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCaching(new ConfigurationBuilder().Build());
        services.AddScoped<IUserNameResolver, HttpContextUserResolver>();
        services.AddOptions<LastChangesOptions>();
        services.AddSingleton<ILastChangesService, LastChangesService>();
        services.AddSingleton<ILastChangesNotifier, LastChangesNotifier>();

        new ApplicationBuilder(services.BuildServiceProvider()).UseDbTriggers();
    }
}
