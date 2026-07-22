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
            public const string Access = "sales-rep:access";

            public static string[] AllPermissions { get; } =
            [
                Access,
            ];
        }

        public static class Roles
        {
            public const string SalesRepRoleName = "Sales Representative";
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
