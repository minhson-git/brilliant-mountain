using IOApp.Configs;
using IOCore;
using IOCore.Core;
using IOCore.Base;
using IOCore.Exs;
using IOCore.Saver;
using IOCore.Utils;
using IOWMI;
using System;
using System.Linq;
using System.Management;
using System.Text.Json.Serialization;
using ZAnnotation.IOCore;

namespace IOApp.Features
{
    public enum SecureType
    {
        Unsecured,
        HideFilesAndFolders,
        EncryptFilesAndFolders,
        HideAndEncryptFilesAndFolders
    }

    public partial class StorageItem : BaseItem, IDisposable
    {
        public enum S
        {
            Loading,
            Ready,

            ProcessInQueue,
            Processing,

            Processed,
            ProcessFailed,

            ProcessPaused,
            ProcessStopped
        };

        public IOStatus<StorageItem, S> Status { get; }

        public ManagementObject ManagementObject { get; protected set; }

        public virtual int Index => ManagementObject.GetIntOrDefault(nameof(Index));
        public long Size => ManagementObject.GetLongOrDefault(nameof(Size), 0);

        public StorageItem(ManagementObject managementObject)
        {
            ManagementObject = managementObject;
            Status = new(this);
        }

        public void Dispose()
        {
            ManagementObject.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public partial class DiskItem : StorageItem
    {
        public Disk Disk { get; protected set; }

        public string Caption => ManagementObject.GetStringOrDefault(nameof(Caption), string.Empty);
        public string Model => ManagementObject.GetStringOrDefault(nameof(Model));
        public string DeviceID => ManagementObject.GetStringOrDefault(nameof(DeviceID));
        public string SerialNumber => ManagementObject.GetStringOrDefault(nameof(SerialNumber));

        public long FreeSpace => VolumeItems.Sum(i => i.FreeSpace);
        public bool IsSecured => VolumeItems.Any(i => i.SecureType is not SecureType.Unsecured);

        public ListEx<VolumeItem> VolumeItems { get; } = [];

        public DiskItem(Disk disk) : base(disk.ManagementObject)
        {
            Disk = disk;

            VolumeItems.Replace([.. disk.Volumes.Select(volume => new VolumeItem(volume))]);
        }
    }

    public partial class VolumeItem : StorageItem
    {
        public override int Index => base.Index + 1;

        public string Name => ManagementObject.GetStringOrDefault(nameof(Name)); // D:
        public string Letter => Name[..^1]; // D
        public string Path => Name + System.IO.Path.DirectorySeparatorChar; // D://
        public string VolumeName => ManagementObject.GetStringOrDefault(nameof(VolumeName), "USB Drive");

        public string FileSystem => ManagementObject.GetStringOrDefault(nameof(FileSystem)); // NTFS
        public string VolumeStatus => ManagementObject.GetStringOrDefault(nameof(Status));
        public long FreeSpace => ManagementObject.GetLongOrDefault(nameof(FreeSpace), 0);

        public long UsedSize => Size - FreeSpace;

        public string DiskCaption { get; private set; }
        public string SerialNumber { get; private set; }
        public string DeviceID { get; private set; }

        public SecureType SecureType
        {
            get;
            set
            {
                if (SetAndNotify(ref field, value))
                    Notify(nameof(SecureTypeText));
            }
        }

        public string SecureTypeText => R.T(AppTypes.SECURE_TYPES[SecureType].Item2);

        public readonly Saver<SecureMetadata> SecureMetadataSaver;

        public VolumeItem(Volume volume) : base(volume.ManagementObject)
        {
            DiskCaption = volume.Disk.ManagementObject.GetStringOrDefault(nameof(DiskItem.Caption));
            SerialNumber = volume.Disk.ManagementObject.GetStringOrDefault(nameof(DiskItem.SerialNumber));
            DeviceID = volume.Disk.ManagementObject.GetStringOrDefault(nameof(DiskItem.DeviceID));

            SecureMetadataSaver = new Saver<SecureMetadata>(SecureMetadataJsonContext.Default.SecureMetadata, true, null, GetMetadataFilePath(Path));
            SecureType = SecureMetadataSaver.Data.SecureType;
        }

        public void Refresh(ManagementObject volume, string diskCaption, string serialNumber, string deviceId)
        {
            ManagementObject = volume;

            DiskCaption = diskCaption;
            SerialNumber = serialNumber;
            DeviceID = deviceId;

            Status.Notify();
            Notify(null);
        }

        //

        #region Metadata

        public static string GetDataFolderPath(string drivePath) => EnvironmentUtils.GetHiddenPath(drivePath, "io-data");
        public static string GetMetadataFilePath(string drivePath) => EnvironmentUtils.GetHiddenPath(drivePath);

        #endregion
    }

    [DataSaver(false)]
    public partial class SecureMetadata
    {
        public partial SecureMetadata() { }

        public SecureType SecureType { get; set => field = value; } = SecureType.Unsecured;
        public string Password { get; set => field = value; } = string.Empty;
    }

    [JsonSerializable(typeof(SecureMetadata))]
    public partial class SecureMetadataJsonContext : JsonSerializerContext { }
}
