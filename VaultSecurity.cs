using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace TremauxLock
{
    internal static class VaultSecurity
    {
        public static void TryHardenPath(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    HardenDirectory(path);
                    return;
                }

                if (File.Exists(path))
                {
                    HardenFile(path);
                }
            }
            catch
            {
            }
        }

        [SupportedOSPlatform("windows")]
        private static void HardenDirectory(string path)
        {
            var directoryInfo = new DirectoryInfo(path);
            DirectorySecurity security = directoryInfo.GetAccessControl(AccessControlSections.Access);
            ApplyRestrictedAcl(security, inheritToChildren: true);
            directoryInfo.SetAccessControl(security);
        }

        [SupportedOSPlatform("windows")]
        private static void HardenFile(string path)
        {
            var fileInfo = new FileInfo(path);
            FileSecurity security = fileInfo.GetAccessControl(AccessControlSections.Access);
            ApplyRestrictedAcl(security, inheritToChildren: false);
            fileInfo.SetAccessControl(security);
        }

        [SupportedOSPlatform("windows")]
        private static void ApplyRestrictedAcl(FileSystemSecurity security, bool inheritToChildren)
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            SecurityIdentifier? userSid = identity.User;
            if (userSid == null)
            {
                return;
            }

            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            AuthorizationRuleCollection existingRules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: false,
                targetType: typeof(SecurityIdentifier));

            foreach (AuthorizationRule existingRule in existingRules)
            {
                if (existingRule is FileSystemAccessRule fileRule)
                {
                    security.RemoveAccessRuleSpecific(fileRule);
                }
            }

            AddAllowRule(security, userSid, inheritToChildren);
            AddAllowRule(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), inheritToChildren);
            AddAllowRule(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), inheritToChildren);
        }

        [SupportedOSPlatform("windows")]
        private static void AddAllowRule(FileSystemSecurity security, IdentityReference identity, bool inheritToChildren)
        {
            var inheritanceFlags = inheritToChildren
                ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit
                : InheritanceFlags.None;

            var rule = new FileSystemAccessRule(
                identity,
                FileSystemRights.FullControl,
                inheritanceFlags,
                PropagationFlags.None,
                AccessControlType.Allow);

            security.AddAccessRule(rule);
        }
    }
}
