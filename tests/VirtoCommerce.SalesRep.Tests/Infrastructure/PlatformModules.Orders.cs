using System;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.NotificationsModule.Data.Repositories;
using VirtoCommerce.NotificationsModule.Data.Senders;
using VirtoCommerce.NotificationsModule.Data.Services;
using VirtoCommerce.NotificationsModule.LiquidRenderer;
using VirtoCommerce.NotificationsModule.LiquidRenderer.Filters;
using VirtoCommerce.NotificationsModule.TemplateLoader.FileSystem;
using VirtoCommerce.OrdersModule.Core.Extensions;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Search.Indexed;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.OrdersModule.Data.Search.Indexed;
using VirtoCommerce.OrdersModule.Data.Services;
using VirtoCommerce.OrdersModule.Data.Validators;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.PushMessages.Core.Events;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.PushMessages.Core.Services;
using VirtoCommerce.PushMessages.Data.Repositories;
using VirtoCommerce.PushMessages.Data.Services;
using VirtoCommerce.PushMessages.ExperienceApi.Authorization;
using VirtoCommerce.PushMessages.ExperienceApi.Extensions;
using VirtoCommerce.PushMessages.ExperienceApi.Handlers;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.TaskManagement.Core.Services;
using VirtoCommerce.TaskManagement.Data.Repositories;
using VirtoCommerce.TaskManagement.Data.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;
using NotificationsConstants = VirtoCommerce.NotificationsModule.Core.ModuleConstants;
using OrdersConstants = VirtoCommerce.OrdersModule.Core.ModuleConstants;
using PushMessagesConstants = VirtoCommerce.PushMessages.Core.ModuleConstants;
using PushMessagesExperienceApiMarker = VirtoCommerce.PushMessages.ExperienceApi.AssemblyMarker;
using TaskManagementConstants = VirtoCommerce.TaskManagement.Core.ModuleConstants;
using TaskPermissions = VirtoCommerce.TaskManagement.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

internal static partial class PlatformModules
{
    /// <summary>
    /// Orders (vc-module-order 3.1018.0): the order services, their validators (registered by Orders.Web's
    /// AddValidators, restated), the totals calculator and, with Search:OrderFullTextSearchEnabled on as the harness
    /// configures it, the indexed order search, and the purchased products. Left out: the change handlers and the jobs
    /// they enqueue (inventory, payments, notifications, change log, indexing), PDF invoices, and the REST API's
    /// polymorphic operation converter (in the module's Web project, not on NuGet).
    /// </summary>
    private static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<OrderDbContext>();

        services.AddSingleton<IValidator<IOperation>, OrderDocumentCountValidator>();
        services.AddSingleton<IValidator<CustomerOrder>, CustomerOrderValidator>();
        services.AddSingleton<IValidator<PaymentIn>, PaymentInValidator>();
        services.AddSingleton<IValidator<OrderPaymentInfo>, PaymentRequestValidator>();

        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddTransient<Func<IOrderRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IOrderRepository>());
        services.AddTransient<ICustomerOrderSearchService, CustomerOrderSearchService>();
        services.AddTransient<ICustomerOrderService, CustomerOrderService>();
        services.AddTransient<ICustomerOrderDataProtectionService, CustomerOrderDataProtectionService>();
        services.AddTransient<IPaymentDataProtectionService, PaymentDataProtectionService>();
        services.AddTransient<IShipmentDataProtectionService, ShipmentDataProtectionService>();
        services.AddTransient<IMemberOrdersService, CustomerOrderService>();
        services.AddTransient<IPaymentSearchService, PaymentSearchService>();
        services.AddTransient<IPaymentService, PaymentService>();
        services.AddTransient<IShipmentService, ShipmentService>();
        services.AddTransient<IShipmentSearchService, ShipmentSearchService>();
        services.AddTransient<ICustomerOrderBuilder, CustomerOrderBuilder>();
        services.AddTransient<ICustomerOrderTotalsCalculator, DefaultCustomerOrderTotalsCalculator>();
        services.AddTransient<ICustomerOrderPaymentService, CustomerOrderPaymentService>();
        services.AddTransient<IPaymentFlowService, PaymentFlowService>();
        services.AddTransient<IPaymentRequestConverter, PaymentRequestDefaultConverter>();
        services.AddTransient<IIndexedCustomerOrderSearchService, IndexedCustomerOrderSearchService>();
        services.AddTransient<ICustomerOrderStatisticService, CustomerOrderStatisticService>();

        // Purchased products: X-Order's product pipeline asks whether the buyer bought a product.
        services.AddTransient<IPurchasedProductsService, PurchasedProductsService>();
        services.AddTransient<PurchasedProductsChangesProvider>();
        services.AddTransient<PurchasedProductsDocumentBuilder>();
        services.AddTransient<PurchasedProductsIndexConfigurator>();

        if (configuration.IsOrderFullTextSearchEnabled())
        {
            services.AddTransient<CustomerOrderSearchRequestBuilder>();
            services.AddSingleton<CustomerOrderChangesProvider>();
            services.AddSingleton<CustomerOrderDocumentBuilder>();
            services.AddSingleton(provider => new IndexDocumentConfiguration
            {
                DocumentType = OrdersConstants.OrderIndexDocumentType,
                DocumentSource = new IndexDocumentSource
                {
                    ChangesProvider = provider.GetService<CustomerOrderChangesProvider>(),
                    DocumentBuilder = provider.GetService<CustomerOrderDocumentBuilder>(),
                },
            });
        }

        services.AddOptions<PaymentDistributedLockOptions>().Bind(configuration.GetSection("Orders:PaymentDistributedLockOptions")).ValidateDataAnnotations();

        return services;
    }

    /// <summary>
    /// Notifications (vc-module-notification 3.1018.0): the notifications and their messages, the registrar, the file
    /// system template loader and the Liquid renderer. No email gateway is configured (Notifications:Gateway), as on a
    /// platform without one: a scheduled notification is rendered and saved as a message, which is what the tests read.
    /// Left out: export and import, the password-reset handler and the jobs.
    /// </summary>
    private static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDatabase<NotificationDbContext>();
        services.AddTransient<INotificationRepository, NotificationRepository>();
        services.AddTransient<Func<INotificationRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetService<INotificationRepository>());
        services.AddTransient<INotificationService, NotificationService>();
        services.AddTransient<INotificationSearchService, NotificationSearchService>();
        // The module's NotificationRegistrar, restated to hand each notification's builder to the type factory at start
        // (HarnessNotificationRegistrar says why).
        services.AddSingleton<HarnessNotificationRegistrar>();
        services.AddSingleton<INotificationRegistrar>(provider => provider.GetRequiredService<HarnessNotificationRegistrar>());
        services.AddTransient<INotificationMessageService, NotificationMessageService>();
        services.AddTransient<INotificationMessageSearchService, NotificationMessageSearchService>();
        services.AddTransient<INotificationSender, NotificationSender>();
        services.AddTransient<NotificationScriptObject>();
        services.AddTransient<IEmailAttachmentService, EmailAttachmentService>();

        services.AddTransient<INotificationLayoutService, NotificationLayoutService>();
        services.AddTransient<INotificationLayoutSearchService, NotificationLayoutSearchService>();
        services.AddSingleton<INotificationLayoutRegistrar, NotificationLayoutRegistrar>();

        services.AddFileSystemTemplateLoader(options => configuration.GetSection("Notifications:Templates").Bind(options));
        services.AddSingleton<INotificationMessageSenderFactory, NotificationMessageSenderFactory>();
        services.AddOptions<EmailSendingOptions>().Bind(configuration.GetSection("Notifications")).ValidateDataAnnotations();
        services.AddOptions<SmsSendingOptions>().Bind(configuration.GetSection("Notifications")).ValidateDataAnnotations();

        services.AddLiquidRenderer(builder =>
        {
            builder.AddCustomLiquidFilterType(typeof(TranslationFilter));
            builder.AddCustomLiquidFilterType(typeof(UrlFilters));
            builder.AddCustomLiquidFilterType(typeof(ArrayFilter));
            builder.SetRendererLoopLimit(configuration["Notifications:LiquidRenderOptions:LoopLimit"].TryParse(NotificationsConstants.DefaultLiquidRendererLoopLimit));
            builder.SetTemplateLanguage(configuration["Notifications:LiquidRenderOptions:TemplateLanguage"]);
        });

        return services;
    }

    /// <summary>
    /// Push messages (vc-module-push-messages 3.1004.0): the messages, their recipients and FCM tokens, and the module's
    /// GraphQL (the storefront reads a buyer's messages and subscribes to new ones: every store has them on by default)
    /// over its in-process message hub, as without Redis. Left out: the change handlers that fan a message out to its
    /// recipients through jobs, Firebase, and the recurring jobs.
    /// </summary>
    private static IServiceCollection AddPushMessagesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PushMessageOptions>();
        services.AddModuleDatabase<PushMessagesDbContext>();
        services.AddTransient<IPushMessagesRepository, PushMessagesRepository>();
        services.AddTransient<Func<IPushMessagesRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IPushMessagesRepository>());
        services.AddTransient<IPushMessageService, PushMessageService>();
        services.AddTransient<IPushMessageSearchService, PushMessageSearchService>();
        services.AddTransient<IPushMessageRecipientService, PushMessageRecipientService>();
        services.AddTransient<IPushMessageRecipientSearchService, PushMessageRecipientSearchService>();
        services.AddTransient<IFcmTokenService, FcmTokenService>();
        services.AddTransient<IFcmTokenSearchService, FcmTokenSearchService>();

        _ = new GraphQLBuilder(services, builder => builder.AddSchema(services, typeof(PushMessagesExperienceApiMarker)));
        services.AddDistributedMessageService(configuration);
        services.AddSingleton<XapiPushMessageRecipientChangedEventHandler>();
        services.AddSingleton<IAuthorizationHandler, PushMessagesAuthorizationHandler>();
        services.AddSingleton<ScopedSchemaFactory<PushMessagesExperienceApiMarker>>();

        return services;
    }

    /// <summary>
    /// Task management (vc-module-task-management, the VCST-6157 alpha the module references), optional to the module
    /// under test: a context without it is a deployment without the module. Left out: the notification handler and its
    /// job, and the module's own GraphQL, which no test path reaches.
    /// </summary>
    private static IServiceCollection AddTaskManagementModule(this IServiceCollection services)
    {
        // WorkTaskEntity.Number is an identity on a non-key column, which SQLite can't generate.
        services.AddModuleDatabase<TaskManagementDbContext>(options => options.ReplaceService<IModelCustomizer, WorkTaskNumberSqliteModelCustomizer>());
        services.AddTransient<IWorkTaskRepository, WorkTaskRepository>();
        services.AddTransient<Func<IWorkTaskRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IWorkTaskRepository>());
        services.AddTransient<IWorkTaskService, WorkTaskService>();
        services.AddTransient<IWorkTaskSearchService, WorkTaskSearchService>();
        services.AddTransient<Func<IWorkTaskSearchService>>(provider => provider.GetRequiredService<IWorkTaskSearchService>);

        return services;
    }

    /// <summary>
    /// Google Analytics (vc-module-google-ecommerce-analytics 4.1006.0), optional to the module under test: its whole
    /// read path, store settings included, down to the report client, which calls Google's Data API. Registered by the
    /// stand-in for that API (<c>TestAnalytics</c>), which replaces the report client after it.
    /// </summary>
    public static IServiceCollection AddGoogleAnalyticsModule(this IServiceCollection services)
    {
        services.AddTransient<IGoogleAnalyticsSettingsManager, GoogleAnalyticsSettingsManager>();
        services.AddSingleton<IGoogleAnalyticsReportClient, GoogleAnalyticsReportClient>();
        services.AddTransient<IAnalyticsDataSource, GoogleAnalyticsDataSource>();
        services.AddTransient<IAnalyticsSettingsResolver, AnalyticsSettingsResolver>();
        services.AddTransient<IAnalyticsService, AnalyticsService>();
        services.AddTransient<IAnalyticsDiagnosticsService, AnalyticsDiagnosticsService>();

        return services;
    }

    /// <summary>
    /// The orders, notifications, push messages, task management and analytics modules' PostInitialize: their
    /// settings, permissions, dynamic property types and the order index's request builder.
    /// </summary>
    private static void PostInitializeOrderModules(IApplicationBuilder app, HostedModules hosted)
    {
        var provider = app.ApplicationServices;
        var configuration = provider.GetRequiredService<IConfiguration>();
        var settings = provider.GetRequiredService<ISettingsRegistrar>();
        var permissions = provider.GetRequiredService<IPermissionsRegistrar>();
        var dynamicProperties = provider.GetRequiredService<IDynamicPropertyRegistrar>();

        dynamicProperties.RegisterType<CustomerOrder>();
        dynamicProperties.RegisterType<PaymentIn>();
        dynamicProperties.RegisterType<OrdersModule.Core.Model.Shipment>();
        dynamicProperties.RegisterType<Capture>();
        dynamicProperties.RegisterType<Refund>();
        dynamicProperties.RegisterType<OrdersModule.Core.Model.LineItem>();
        var orderSettings = OrdersConstants.Settings.General.AllSettings;
        if (configuration.IsOrderFullTextSearchEnabled())
        {
            orderSettings = orderSettings.Union(OrdersConstants.Settings.IndexationSettings);
            provider.GetRequiredService<ISearchRequestBuilderRegistrar>()
                .Register(OrdersConstants.OrderIndexDocumentType, provider.GetService<CustomerOrderSearchRequestBuilder>);
        }

        settings.RegisterSettings(orderSettings, "VirtoCommerce.Orders");
        settings.RegisterSettingsForType(OrdersConstants.Settings.StoreLevelSettings, nameof(Store));
        permissions.RegisterPermissions("VirtoCommerce.Orders", "Orders", OrdersConstants.Security.Permissions.AllPermissions);
        // The purchased products in the product index, when its setting is on (off by default).
        provider.GetRequiredService<PurchasedProductsIndexConfigurator>().Configure().GetAwaiter().GetResult();

        settings.RegisterSettings(NotificationsConstants.Settings.AllSettings, "VirtoCommerce.Notifications");
        permissions.RegisterPermissions("VirtoCommerce.Notifications", "Notifications", NotificationsConstants.Security.Permissions.AllPermissions);

        settings.RegisterSettings(PushMessagesConstants.Settings.AllSettings, "VirtoCommerce.PushMessages");
        settings.RegisterSettingsForType(PushMessagesConstants.Settings.StoreSettings, nameof(Store));
        permissions.RegisterPermissions("VirtoCommerce.PushMessages", "Push Messages", PushMessagesConstants.Security.Permissions.AllPermissions);
        app.UseScopedSchema<PushMessagesExperienceApiMarker>("pushMessages");
        app.RegisterEventHandler<PushMessageRecipientChangedEvent, XapiPushMessageRecipientChangedEventHandler>();

        if (hosted.TaskManagement)
        {
            settings.RegisterSettings(TaskManagementConstants.Settings.General.AllSettings, "VirtoCommerce.TaskManagement");
            permissions.RegisterPermissions(TaskPermissions.AllPermissions
                .Select(x => new Permission { ModuleId = "VirtoCommerce.TaskManagement", GroupName = "TaskManagement", Name = x })
                .ToArray());
        }

        if (provider.GetRequiredService<IServiceProviderIsService>().IsService(typeof(IAnalyticsService)))
        {
            permissions.RegisterPermissions(AnalyticsConstants.Security.Permissions.AllPermissions
                .Select(x => new Permission { ModuleId = "VirtoCommerce.GoogleEcommerceAnalytics", GroupName = "GoogleAnalytics4", Name = x })
                .ToArray());
            settings.RegisterSettings(AnalyticsConstants.Settings.General.AllSettings.Concat(AnalyticsConstants.Settings.DataApi.AllSettings), "VirtoCommerce.GoogleEcommerceAnalytics");
            settings.RegisterSettingsForType(AnalyticsConstants.Settings.StoreLevelSettings, nameof(Store));
        }
    }

    /// <summary>
    /// The notifications module's step once the application has started: every registered notification is saved, without
    /// its predefined templates (they stay with the registration), so the searches find it.
    /// </summary>
    private static async Task SaveRegisteredNotificationsAsync(IServiceProvider provider)
    {
        var registrar = provider.GetRequiredService<INotificationRegistrar>();
        var notifications = registrar.AllRegisteredNotifications.Select(x =>
        {
            x.ReduceDetails(nameof(NotificationResponseGroup.Default));
            return x;
        }).ToArray();

        await provider.GetRequiredService<INotificationService>().SaveChangesAsync(notifications);
    }
}
