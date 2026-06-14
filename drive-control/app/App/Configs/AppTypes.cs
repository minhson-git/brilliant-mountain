using IOApp.Features;
using IOCore.Gens;
using System.Collections.Generic;

namespace IOApp.Configs
{
    public class AppTypes
    {
        public enum UsbFileSystem
        {
            NTFS,
            FAT32,
            exFAT
        }

        public enum UsbUsageProfile
        {
            MaximumCompatibility,
            LargeFiles,
            Mixed,
        }

        public static readonly Dictionary<UsbFileSystem, string> USB_FILE_SYSTEMS = new()
        {
            { UsbFileSystem.NTFS,       "NTFS"  },
            { UsbFileSystem.FAT32,      "FAT32" },
            { UsbFileSystem.exFAT,      "exFAT" }
        };

        public enum CleanMode
        {
            DeleteEmptyFolders,
            Empty,
        }

        public static readonly Dictionary<CleanMode, L> CLEAN_MODES = new()
        {
            { CleanMode.DeleteEmptyFolders,     L.DeleteEmptyFolders },
            { CleanMode.Empty,                  L.Empty },
        };

        public enum SecureFilter
        {
            All,
            Unsecured,
            Secured
        }

        public static readonly Dictionary<SecureFilter, (string, L)> SECURE_FILTERS = new()
        {
            { SecureFilter.All,       ("\uE71C", L.All) },
            { SecureFilter.Unsecured, ("\uE785", L.Unsecure) },
            { SecureFilter.Secured,   ("\uE72E", L.Secure) },
        };

        public static readonly Dictionary<SecureType, (L, L)> SECURE_TYPES = new()
        {
            { SecureType.Unsecured,                     (L.Status_Unsecured,                       L.Status_Unsecured) },
            { SecureType.HideFilesAndFolders,           (L.Features_HideFilesAndFolders,           L.Status_Hidden) },
            { SecureType.EncryptFilesAndFolders,        (L.Features_EncryptFilesAndFolders,        L.Status_Encrypted) },
            { SecureType.HideAndEncryptFilesAndFolders, (L.Features_HideAndEncryptFilesAndFolders, L.Status_HiddenAndEncrypted) },
        };

        public enum SyncMode
        {
            Sync,
            Copy
        }

        public static readonly Dictionary<SyncMode, L> SYNC_MODES = new()
        {
            { SyncMode.Sync, L.Sync },
            { SyncMode.Copy, L.Copy },
        };
    }
}