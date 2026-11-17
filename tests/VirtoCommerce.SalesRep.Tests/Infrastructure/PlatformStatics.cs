using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CatalogModule.Data.Authorization;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Data.Model;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.MarketingModule.Core.Promotions;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.NotificationsModule.Data.Model;
using VirtoCommerce.NotificationsModule.LiquidRenderer;
using VirtoCommerce.OrdersModule.Data.Authorization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.JsonConverters;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.ShippingModule.Core.Security;
using VirtoCommerce.StoreModule.Core.Security;
using VirtoCommerce.TaskManagement.Data.Authorization;
using VirtoCommerce.Xapi.Core.Models;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// What the hosted modules set process-wide in their Initialize and PostInitialize, set once per process as the platform
/// sets it once per start: type factory registrations and overrides, JSON discriminators, GraphQL's naming switches.
/// Done per harness instead, each start would change them under the test classes already running (the factory's own
/// registration is thread-safe on this platform, but a re-registration replaces what the others use), and a
/// registration that captures services would hand every later harness the first one's. The notifications the module
/// under test registers in its own PostInitialize stay per harness; <see cref="HarnessNotificationRegistrar"/> says how.
/// </summary>
/// <remarks>
/// Statics that hold services take them from a provider of the process's own (<see cref="Services"/>), as the platform's
/// hold its one application's: a harness's provider would be disposed under the next one. Two registrations of the
/// modules capture services, and differ here for that reason:
/// <list type="bullet">
/// <item>The notifications module's script object (the Liquid renderer's) comes from <see cref="Services"/>, holding what
/// it is made of: the platform's translations and the assets' URL resolver. Every harness renders with the same ones.</item>
/// <item>Marketing's DynamicPromotion is registered without the setup action that hands it one provider's coupon and
/// usage searches: they serve only a promotion's evaluation at checkout, which no path of this module runs.</item>
/// </list>
/// </remarks>
internal static class PlatformStatics
{
    private static readonly Lazy<IServiceProvider> _services = new(PlatformModules.BuildProcessServices);

    /// <summary>
    /// The process's own platform services, for the statics that hold services: the notification script object's, and
    /// the dynamic property metadata the platform's JSON contract reads (an E2E host initializes it, as Startup does).
    /// </summary>
    public static IServiceProvider Services => _services.Value;

    [ModuleInitializer]
    internal static void Register()
    {
        // X-API's Initialize. Legacy naming keeps the "Type" suffix on object graph types, which keeps
        // DynamicPropertyValueType and the DynamicPropertyValue scalar apart.
#pragma warning disable CS0618 // Type or member is obsolete: the X-API module sets the same switches.
        GraphQL.GlobalSwitches.UseLegacyTypeNaming = true;
        GraphQL.GlobalSwitches.InferFieldNullabilityFromNRTAnnotations = false;
#pragma warning restore CS0618
        AbstractTypeFactory<ProductPrice>.RegisterType<ProductPrice>();

        // The customer module's PostInitialize.
        AbstractTypeFactory<Member>.RegisterType<Organization>().MapToType<OrganizationEntity>();
        AbstractTypeFactory<Member>.RegisterType<Contact>().MapToType<ContactEntity>();
        AbstractTypeFactory<Member>.RegisterType<Vendor>().MapToType<VendorEntity>();
        AbstractTypeFactory<Member>.RegisterType<Employee>().MapToType<EmployeeEntity>();
        AbstractTypeFactory<MemberEntity>.RegisterType<ContactEntity>();
        AbstractTypeFactory<MemberEntity>.RegisterType<OrganizationEntity>();
        AbstractTypeFactory<MemberEntity>.RegisterType<VendorEntity>();
        AbstractTypeFactory<MemberEntity>.RegisterType<EmployeeEntity>();
        PolymorphJsonConverter.RegisterTypeForDiscriminator(typeof(Member), nameof(Member.MemberType));

        // The permission scopes of the store, catalog, shipping, orders and task management modules (the customer's and
        // marketing's are in their Web projects, not on NuGet: no path here checks an admin's scoped permission).
        AbstractTypeFactory<PermissionScope>.RegisterType<StoreSelectedScope>();
        AbstractTypeFactory<PermissionScope>.RegisterType<SelectedCatalogScope>();
        AbstractTypeFactory<PermissionScope>.RegisterType<SelectedStoreScope>();
        AbstractTypeFactory<PermissionScope>.RegisterType<OnlyOrderResponsibleScope>();
        AbstractTypeFactory<PermissionScope>.RegisterType<OrderSelectedStoreScope>();
        AbstractTypeFactory<PermissionScope>.RegisterType<TaskToMyOrganizationScope>();
        AbstractTypeFactory<PermissionScope>.RegisterType<TaskToMeScope>();

        // Marketing's DynamicPromotion override (see the remarks).
        AbstractTypeFactory<Promotion>.RegisterType<DynamicPromotion>();

        // The notifications module's PostInitialize.
        AbstractTypeFactory<NotificationTemplate>.RegisterType<EmailNotificationTemplate>();
        AbstractTypeFactory<NotificationTemplate>.RegisterType<SmsNotificationTemplate>();
        AbstractTypeFactory<NotificationMessage>.RegisterType<EmailNotificationMessage>();
        AbstractTypeFactory<NotificationMessage>.RegisterType<SmsNotificationMessage>();
        AbstractTypeFactory<NotificationEntity>.RegisterType<EmailNotificationEntity>();
        AbstractTypeFactory<NotificationEntity>.RegisterType<SmsNotificationEntity>();
        AbstractTypeFactory<NotificationTemplateEntity>.RegisterType<EmailNotificationTemplateEntity>();
        AbstractTypeFactory<NotificationTemplateEntity>.RegisterType<SmsNotificationTemplateEntity>();
        AbstractTypeFactory<NotificationMessageEntity>.RegisterType<EmailNotificationMessageEntity>();
        AbstractTypeFactory<NotificationMessageEntity>.RegisterType<SmsNotificationMessageEntity>();
        AbstractTypeFactory<NotificationScriptObject>.RegisterType<NotificationScriptObject>()
            .WithFactory(() => Services.GetRequiredService<NotificationScriptObject>());
        PolymorphJsonConverter.RegisterTypeForDiscriminator(typeof(Notification), nameof(Notification.Type));
        PolymorphJsonConverter.RegisterTypeForDiscriminator(typeof(NotificationTemplate), nameof(NotificationTemplate.Type));
        PolymorphJsonConverter.RegisterTypeForDiscriminator(typeof(NotificationMessage), nameof(NotificationMessage.Type));

        // The analytics module's Initialize.
        AbstractTypeFactory<AnalyticsDataApiSettings>.RegisterType<AnalyticsDataApiSettings>();
        AbstractTypeFactory<AnalyticsDataQuery>.RegisterType<AnalyticsDataQuery>();
        AbstractTypeFactory<AnalyticsDiagnosticsCheck>.RegisterType<AnalyticsDiagnosticsCheck>();
        AbstractTypeFactory<AnalyticsDiagnosticsReportShape>.RegisterType<AnalyticsDiagnosticsReportShape>();
        AbstractTypeFactory<AnalyticsDiagnosticsRequest>.RegisterType<AnalyticsDiagnosticsRequest>();
        AbstractTypeFactory<AnalyticsDiagnosticsResult>.RegisterType<AnalyticsDiagnosticsResult>();
        AbstractTypeFactory<AnalyticsDimensionFilter>.RegisterType<AnalyticsDimensionFilter>();
        AbstractTypeFactory<AnalyticsEvent>.RegisterType<AnalyticsEvent>();
        AbstractTypeFactory<AnalyticsEventSearchCriteria>.RegisterType<AnalyticsEventSearchCriteria>();
        AbstractTypeFactory<AnalyticsEventSearchResult>.RegisterType<AnalyticsEventSearchResult>();
        AbstractTypeFactory<AnalyticsEventSummary>.RegisterType<AnalyticsEventSummary>();
        AbstractTypeFactory<AnalyticsEventSummaryCriteria>.RegisterType<AnalyticsEventSummaryCriteria>();
    }
}
