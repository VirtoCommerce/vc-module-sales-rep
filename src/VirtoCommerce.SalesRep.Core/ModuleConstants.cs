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
            public const string Diagnostics = "sales-rep:diagnostics";
            public const string DocumentsRead = "sales-rep-documents:read";
            public const string DocumentsWrite = "sales-rep-documents:write";

            public static string[] AllPermissions { get; } =
            [
                Access,
                Diagnostics,
                DocumentsRead,
                DocumentsWrite,
            ];
        }

        public static class Roles
        {
            // The two membership roles: assigned on an OrganizationMembership, and what makes someone a rep.
            public const string SalesRepRoleName = "Sales Representative";
            public const string AdvancedSalesRepRoleName = "Advanced Sales Representative";

            // The back-office role: a platform role for whoever administers the feature, never a membership.
            // IMPORTANT (keep): it now carries analytics diagnostics as well as the documents library, so
            // "Sales Rep Administrator" would name it better. The string is kept because it shipped in 3.1006.0 —
            // the seeder skips a role that already exists by name, so renaming it would strand the old role on
            // every existing install and seed a second one beside it. Rename only together with an upgrade step
            // that renames the existing role in place.
            public const string DocumentsManagerRoleName = "Sales Rep Documents Manager";
        }
    }

    // A field carrying part of the rep's scope must never appear here - see SanitizeFacet.
    public static class OrderFacets
    {
        public const string Status = "status";
        public const string CustomerName = "organizationname";

        public static string[] All { get; } = [Status, CustomerName];
    }

    public static class Sharing
    {
        // Wishlist scope for a list a Sales Rep publishes to specific customer organizations (VCST-5332). Not a
        // CartSharingScope member; SalesRepCustomerCartSharingScopePolicy owns its rules and SharedWithId space.
        public const string CustomerScope = "Customer";
    }

    public static class Activities
    {
        // The feed's paging depth, checked before the native/merged split so it caps every request. The merged view
        // sets it: a merged page slices the top Skip+Take rows of every category, so "All" at a full page reads at
        // most 3,000 rows. 26 pages of 20 (skip 0–500); past that the feed reports no rows.
        public const int MaxSkip = 500;

        // Past page one, the merged view rounds its source window up to a multiple of this, so consecutive pages
        // ask the same (cached) question — see SalesRepActivityService.
        public const int PagingWindowBucket = 100;

        public static class Categories
        {
            public const string Orders = "orders";
            public const string Customers = "customers";
            public const string Searches = "searches";
            public const string ProductViews = "productViews";
            public const string Logins = "logins";
        }

        public static class Types
        {
            public const string OrderPlaced = "orderPlaced";
            public const string CustomerAssigned = "customerAssigned";
            public const string Search = "search";
            public const string ProductView = "productView";
            public const string Login = "login";
        }

        public static class Precision
        {
            public const string Exact = "exact";
            public const string Hour = "hour";
        }
    }

    public static class Analytics
    {
        // Values of the storefront's 'session_kind' user dimension. Reads pin Self: 'impersonated' is a rep working
        // the account, and showing it would report the rep's own browsing as the customer's.
        public static class SessionKinds
        {
            public const string Self = "self";
            public const string Impersonated = "impersonated";
        }
    }

    public static class Insights
    {
        public const int DefaultTake = 5;
        public const int MinTake = 1;
        public const int MaxTake = 20;

        public static class Sort
        {
            public const string Count = "count";
            public const string Date = "date";
        }
    }

    public static class DiagnosticsStages
    {
        public const string FeatureQuery = "featureQuery";
    }

    public static class Documents
    {
        public const int CategoryMaxLength = 32;
    }

    public static class Communication
    {
        public const int MaxTitleLength = 128;

        public const int MaxMessageLength = 1000;

        public const int MaxOrganizations = 1000;

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

    public static class Profile
    {
        // The ContactEntity column lengths these values are persisted into.
        public const int NameMaxLength = 128;
        public const int SalutationMaxLength = 256;
    }

    public static class Tasks
    {
        // Mirrors TaskManagement's WorkTaskEntity column widths. Description is deliberately absent: that column
        // has no [StringLength], so capping it here would invent a limit the storage does not have.
        public const int MaxNameLength = 256;

        public const int MaxTypeLength = 128;

        public const int MaxResponsibleNameLength = 256;
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

            // The second axis of each family's behavior, alongside its expiration above (matrix in the README).
            // Flippable at runtime, but it governs entries created from now on: one cached while the flag was off
            // carries no token and cannot be evicted until it expires.
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

            // The heaviest query, and nothing on the hub needs it fresh to the second: deliberately TTL-only.
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

                // Top sellers is here because it aggregates the orders' line items.
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
