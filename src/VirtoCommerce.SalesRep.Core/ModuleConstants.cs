using System.Collections.Generic;
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

    public static class Communication
    {
        // Mirrors PushMessageEntity.Topic [StringLength(128)] — a longer title fails the push save.
        public const int MaxTitleLength = 128;

        // Kept below PushMessageEntity.ShortMessage [StringLength(1024)].
        public const int MaxMessageLength = 1000;

        // Stable, string outcome codes returned in SalesRepCommunicationResult.Warnings and mapped to a
        // localized message by the storefront. Strings (not an enum) so downstream projects can contribute
        // their own codes without recompiling this contract.
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

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings;
            }
        }
    }
}
