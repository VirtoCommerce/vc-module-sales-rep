using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Caching;

namespace VirtoCommerce.SalesRep.Core;

public static class ModuleConstants
{
    // The file-experience-api upload scope; must be configured in the FileUpload:Scopes application settings.
    public const string DocumentsScope = "sales-rep-documents";

    public static class Security
    {
        public static class Permissions
        {
            public const string Access = "sales-rep:access";
            public const string DocumentsRead = "sales-rep-documents:read";
            public const string DocumentsWrite = "sales-rep-documents:write";

            public static string[] AllPermissions { get; } =
            [
                Access,
                DocumentsRead,
                DocumentsWrite,
            ];
        }

        public static class Roles
        {
            public const string SalesRepRoleName = "Sales Representative";
            public const string AdvancedSalesRepRoleName = "Advanced Sales Representative";
            public const string DocumentsManagerRoleName = "Sales Rep Documents Manager";
        }
    }

    public static class Sharing
    {
        // Wishlist sharing scope used when a Sales Rep publishes a list to specific customer organizations
        // (VCST-5332). Not a member of CartModule's CartSharingScope: the sharing pipeline accepts any scope
        // string, and SalesRepCartSharingService teaches the platform this value's visibility rules. The scope also
        // defines the id space of CartSharingSetting.SharedWithId (here: a customer organization id).
        public const string CustomerScope = "Customer";
    }

    public static class Documents
    {
        public const int CategoryMaxLength = 32;
    }

    public static class Communication
    {
        public const int MaxTitleLength = 128;

        public const int MaxMessageLength = 1000;

        public static class Warnings
        {
            public const string NoRecipients = "NoRecipients";
            public const string EmailUnavailable = "EmailUnavailable";
            public const string EmailStoreAccessDenied = "EmailStoreAccessDenied";
            public const string EmailNoRecipients = "EmailNoRecipients";
            public const string EmailSendFailed = "EmailSendFailed";
            public const string PushSendFailed = "PushSendFailed";
        }
    }

    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor SalesRepEnabled { get; } = new()
            {
                Name = "SalesRep.Enabled",
                GroupName = "Sales Rep|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
                IsPublic = true,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return SalesRepEnabled;
                }
            }
        }

        public static class Caching
        {
            public const int DefaultCacheLifetimeMinutes = 5;

            public static SettingDescriptor OrderStatisticsCacheExpiration { get; } = new()
            {
                Name = "SalesRep.Statistics.OrderCacheExpirationMinutes",
                GroupName = "Sales Rep|Statistics",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultCacheLifetimeMinutes,
            };

            public static SettingDescriptor CartStatisticsCacheExpiration { get; } = new()
            {
                Name = "SalesRep.Statistics.CartCacheExpirationMinutes",
                GroupName = "Sales Rep|Statistics",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultCacheLifetimeMinutes,
            };

            public static SettingDescriptor CustomerCountsCacheExpiration { get; } = new()
            {
                Name = "SalesRep.Statistics.CustomerCountsCacheExpirationMinutes",
                GroupName = "Sales Rep|Statistics",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultCacheLifetimeMinutes,
            };

            public static SettingDescriptor TopSellerCacheExpiration { get; } = new()
            {
                Name = "SalesRep.Statistics.TopSellerCacheExpirationMinutes",
                GroupName = "Sales Rep|Statistics",
                ValueType = SettingValueType.Integer,
                DefaultValue = DefaultCacheLifetimeMinutes,
            };

            // The second axis of every family's cache behavior (the first is its expiration above):
            //   expiration 0            -> no cache at all, the flag is not consulted
            //   expiration > 0, false   -> pure short-TTL cache
            //   expiration > 0, true    -> cart/order changes evict the organization's entries, TTL is the ceiling
            // Consulted on both sides — entry creation and the event handlers — so it is flippable at runtime.
            public static SettingDescriptor OrderStatisticsInvalidateOnChange { get; } = new()
            {
                Name = "SalesRep.Statistics.OrderInvalidateOnChange",
                GroupName = "Sales Rep|Statistics",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
            };

            public static SettingDescriptor CartStatisticsInvalidateOnChange { get; } = new()
            {
                Name = "SalesRep.Statistics.CartInvalidateOnChange",
                GroupName = "Sales Rep|Statistics",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
            };

            public static SettingDescriptor CustomerCountsInvalidateOnChange { get; } = new()
            {
                Name = "SalesRep.Statistics.CustomerCountsInvalidateOnChange",
                GroupName = "Sales Rep|Statistics",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
            };

            // The heaviest query, and no acceptance criterion touches its freshness: deliberately TTL-only.
            public static SettingDescriptor TopSellerInvalidateOnChange { get; } = new()
            {
                Name = "SalesRep.Statistics.TopSellerInvalidateOnChange",
                GroupName = "Sales Rep|Statistics",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
            };

            public static class Families
            {
                // Order statistics and the used-status vocabulary share one family: same records, same lifetime.
                public static StatisticsCacheFamily Order { get; } =
                    new(nameof(Order), OrderStatisticsCacheExpiration, OrderStatisticsInvalidateOnChange);

                public static StatisticsCacheFamily Cart { get; } =
                    new(nameof(Cart), CartStatisticsCacheExpiration, CartStatisticsInvalidateOnChange);

                public static StatisticsCacheFamily CustomerCounts { get; } =
                    new(nameof(CustomerCounts), CustomerCountsCacheExpiration, CustomerCountsInvalidateOnChange);

                public static StatisticsCacheFamily TopSeller { get; } =
                    new(nameof(TopSeller), TopSellerCacheExpiration, TopSellerInvalidateOnChange);

                // The families an order change concerns: order figures and the status vocabulary, the ordering-customer
                // count, and the top-seller ranking (which aggregates the orders' line items).
                public static StatisticsCacheFamily[] OrderDriven { get; } = [Order, CustomerCounts, TopSeller];
            }

            public static IEnumerable<SettingDescriptor> AllCachingSettings
            {
                get
                {
                    yield return OrderStatisticsCacheExpiration;
                    yield return CartStatisticsCacheExpiration;
                    yield return CustomerCountsCacheExpiration;
                    yield return TopSellerCacheExpiration;
                    yield return OrderStatisticsInvalidateOnChange;
                    yield return CartStatisticsInvalidateOnChange;
                    yield return CustomerCountsInvalidateOnChange;
                    yield return TopSellerInvalidateOnChange;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings.Concat(Caching.AllCachingSettings);
            }
        }
    }
}
