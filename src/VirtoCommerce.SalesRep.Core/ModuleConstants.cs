using System.Collections.Generic;
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
            public static SettingDescriptor SalesRepEnabled { get; } = new()
            {
                Name = "SalesRep.Enabled",
                GroupName = "Sales Rep|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
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
