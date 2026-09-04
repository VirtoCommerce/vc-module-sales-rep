using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Settings;

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

    // A field carrying part of the rep's scope must never appear here - see SanitizeFacet.
    public static class OrderFacets
    {
        public const string Status = "status";
        public const string CustomerName = "organizationname";

        public static string[] All { get; } = [Status, CustomerName];
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

    public static class Profile
    {
        // The ContactEntity column lengths these values are persisted into.
        public const int NameMaxLength = 128;
        public const int SalutationMaxLength = 256;
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

            public static IEnumerable<SettingDescriptor> AllCachingSettings
            {
                get
                {
                    yield return OrderStatisticsCacheExpiration;
                    yield return CartStatisticsCacheExpiration;
                    yield return CustomerCountsCacheExpiration;
                    yield return TopSellerCacheExpiration;
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
