using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.SalesRep.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            /// <summary>
            /// The permission that DEFINES a Sales Rep (held by the rep, via a role — globally or per-org).
            /// NOT an admin permission: managing reps via the REST API/admin app uses the customer module's
            /// member-management permissions (customer:read/create/update/delete) instead.
            /// </summary>
            public const string Access = "sales-rep:access";

            public static string[] AllPermissions { get; } =
            [
                Access,
            ];
        }

        public static class Roles
        {
            /// <summary>
            /// Display name of the default role created (with a random id) the first time a Sales Rep is saved
            /// and no role yet grants <see cref="Permissions.Access"/>. Admins may rename or delete it — a Sales
            /// Rep is identified by holding the permission, never by this role's id.
            /// </summary>
            public const string SalesRepRoleName = "Sales Representative";
        }
    }

    public static class Settings
    {
        public static class General
        {
            /// <summary>
            /// Per-store, public flag that toggles the Sales Rep UI's visibility on that store's storefront.
            /// It is a presentation switch only — it does NOT gate the backend X-API or the data it returns
            /// (those stay secured by rep membership scoping). Public + registered for the Store type so the
            /// storefront reads it from <c>store.settings.modules</c>; defaults to enabled.
            /// </summary>
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

        /// <summary>
        /// Backend tuning knobs for the dashboard statistics caches (VCST-5309 widgets). Each value is the
        /// time-to-live, in minutes, of the corresponding aggregate's memory cache; 0 (or negative) disables caching
        /// for that query. Module-global and non-public: they are operational tuning, not per-store storefront data,
        /// so they are registered only against the module (not the Store type) and never exposed to the storefront.
        /// All default to <see cref="DefaultCacheLifetimeMinutes"/>; override an individual query if one aggregate
        /// needs a different freshness.
        /// </summary>
        public static class Caching
        {
            /// <summary>Default time-to-live (minutes) applied to every statistics cache unless overridden.</summary>
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
