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
            /// Stable id of the global "Sales Representative" role. Seeded once (create-if-absent),
            /// so an admin may rename it without it being re-seeded.
            /// </summary>
            public const string SalesRepRoleId = "sales-rep";
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
