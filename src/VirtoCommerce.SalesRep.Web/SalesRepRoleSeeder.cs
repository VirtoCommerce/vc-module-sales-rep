using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Web;

/// <summary>
/// Seeds the default "Sales Representative" role once at startup — but only if no role already grants
/// sales-rep:access (<see cref="ISalesRepRoleResolver.EnsureSalesRepRoleAsync"/> is create-if-none). Seeding
/// here (not lazily on a GET) keeps read endpoints side-effect-free.
///
/// Runs as an <see cref="IHostedService"/> so the work is awaited (not blocked via GetAwaiter().GetResult()),
/// and under a cluster-wide distributed lock so that when several instances boot at once only one performs the
/// create; the others wait (tryLockTimeout), re-check, and find the role — no duplicate. With Redis this is a
/// real lock; without Redis the platform registers a no-op lock, which is fine for a single instance.
/// </summary>
public class SalesRepRoleSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public SalesRepRoleSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
        var roleResolver = scope.ServiceProvider.GetRequiredService<ISalesRepRoleResolver>();

        await lockService.ExecuteAsync(
            "SalesRep:SeedDefaultRole",
            () => roleResolver.EnsureSalesRepRoleAsync(),
            // tryLockTimeout + retryInterval must BOTH be set to make a contending instance wait-and-retry;
            // omitting either falls back to a single-shot acquire that throws immediately if the lock is held.
            tryLockTimeout: TimeSpan.FromSeconds(15),
            retryInterval: TimeSpan.FromSeconds(1),
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
