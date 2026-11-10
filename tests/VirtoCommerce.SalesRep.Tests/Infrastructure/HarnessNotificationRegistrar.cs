using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// The notifications module's registrar (<c>NotificationRegistrar</c>, vc-module-notification 3.1018.0) restated for a
/// process that hosts a platform per test: it registers a notification type as the module's does, but hands the
/// notification's builder to the type factory when the harness starts (<see cref="Publish"/>), not when it is registered.
/// </summary>
/// <remarks>
/// The module's registrar points the process-wide type factory at a new, empty builder the moment a notification is
/// registered, and the registering module fills the builder's templates only after that call returns
/// (<c>RegisterNotification&lt;T&gt;().WithTemplatesFromPath(...)</c>). A platform does that once, at startup, before it
/// serves anything. Here every harness's startup does it while the other test classes run, and for that moment the
/// shared factory built the notification without its templates (about one build in a hundred while harnesses start). A
/// test sending email then saved an empty message, which failed the message validation, and the send reported
/// EmailSendFailed: now and then, under the full suite's load only. Each harness's builder is the same notification with
/// the same templates, so handing over complete builders only is all it takes for the factory never to build one
/// without them.
/// </remarks>
internal sealed class HarnessNotificationRegistrar(IServiceProvider serviceProvider) : INotificationRegistrar
{
    private readonly Lock _lock = new();
    private readonly List<(TypeInfo<Notification> TypeInfo, NotificationBuilder Builder)> _builders = [];

    public IEnumerable<Notification> AllRegisteredNotifications =>
        AbstractTypeFactory<Notification>.AllTypeInfos.Select(x => AbstractTypeFactory<Notification>.TryCreateInstance(x.TypeName));

    public NotificationBuilder Notification<TNotification>() where TNotification : Notification
    {
        var typeInfo = AbstractTypeFactory<Notification>.RegisterType<TNotification>();
        return Keep(typeInfo, new NotificationBuilder(serviceProvider, typeof(TNotification), typeInfo.Factory));
    }

    public NotificationBuilder RegisterNotification<TNotification>(Func<Notification> factory = null) where TNotification : Notification
    {
        var typeInfo = AbstractTypeFactory<Notification>.RegisterType<TNotification>();
        return Keep(typeInfo, new NotificationBuilder(serviceProvider, typeof(TNotification), factory));
    }

    public NotificationBuilder OverrideNotificationType<TOldNotificationType, TNewNotificationType>(Func<Notification> factory = null)
        where TOldNotificationType : Notification
        where TNewNotificationType : Notification
    {
        var typeInfo = AbstractTypeFactory<Notification>.OverrideType<TOldNotificationType, TNewNotificationType>();
        return Keep(typeInfo, new NotificationBuilder(serviceProvider, typeof(TNewNotificationType), factory));
    }

    /// <summary>Hands each builder registered since the last call, complete by now, to the type factory.</summary>
    public void Publish()
    {
        lock (_lock)
        {
            foreach (var (typeInfo, builder) in _builders)
            {
                typeInfo.WithFactory(builder.Build);
            }

            _builders.Clear();
        }
    }

    private NotificationBuilder Keep(TypeInfo<Notification> typeInfo, NotificationBuilder builder)
    {
        lock (_lock)
        {
            _builders.Add((typeInfo, builder));
        }

        return builder;
    }
}
