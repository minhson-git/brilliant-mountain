using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Core;
using IOCore.Base;
using IOCore.Exs;
using IOCore.Gens;
using System;
using System.Diagnostics;
using System.Linq;
using Windows.Win32;
using ZAnnotation.IOCore;
using static IOApp.Configs.AppTypes;
using static IOApp.Features.Share;

namespace IOApp.Dialogs
{
    [BindingProxy]
    internal sealed partial class SettingUSBDialog : VolumeFeatureDialog
    {
        public ObservableCollectionEx<OptionItem<UsbFileSystem>> UsbFileSystemItems { get; } = [];
        public int UsbFileSystemIndex
        {
            get;
            set
            {
                if (SetAndNotify(ref field, value))
                    Notify(nameof(UsbFileSystem));
            }
        } = -1;
        public UsbFileSystem UsbFileSystem => UsbFileSystemItems.GetAtOrDefault(UsbFileSystemIndex, UsbFileSystem.NTFS);

        //public bool IsReadOnly { get; set { SetAndNotify(ref field, value); } } = false;

        public SettingUSBDialog(WindowEx window, VolumeItem item) : base(window)
        {
            InitializeComponent();
            DataContext = this;

            VolumeItem = item;
            VolumeNameTextBox.Text = item.VolumeName;

            UsbFileSystemItems.Replace(USB_FILE_SYSTEMS.Select(i => new OptionItem<UsbFileSystem>(i.Key, i.Value)));
            var fileSystemOfVolume = USB_FILE_SYSTEMS.FirstOrDefault(x => x.Value == item.FileSystem).Key;
            UsbFileSystemIndex = UsbFileSystemItems.IndexOf(i => i.Key == fileSystemOfVolume);

            //IsReadOnly = IsVolumeReadOnly(item.Letter);
        }

        void FormatUSB(VolumeItem item, UsbFileSystem fileSystem)
        {
            var psi = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoLogo -NoProfile -WindowStyle Hidden " +
                    "-Command \"Get-Volume -DriveLetter '" + item.Name +
                    "' | Format-Volume -FileSystem '" + USB_FILE_SYSTEMS[fileSystem] +
                    "' -NewFileSystemLabel '" + item.VolumeName +
                    "' -Confirm:$false -ErrorAction SilentlyContinue\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var proc = Process.Start(psi);
            proc?.WaitForExit();
        }

        //bool IsVolumeReadOnly(string letter)
        //{
        //    try
        //    {
        //        var searcher = new ManagementObjectSearcher($"SELECT WriteProtection FROM Win32_Volume WHERE DriveLetter = '{letter}:'"
        //        );

        //        foreach (var volume in searcher.Get())
        //        {
        //            var value = volume["WriteProtection"];
        //            if (value != null && int.TryParse(value.ToString(), out int wp))
        //                return wp == 1;
        //        }
        //    }
        //    catch
        //    {
        //    }

        //    return false;
        //}

        //void SetVolumeReadOnly(string volumeLetter, bool readOnly)
        //{
        //    if (string.IsNullOrWhiteSpace(volumeLetter))
        //        return;

        //    var script = $@"
        //                    select volume {volumeLetter}
        //                    attributes volume {(readOnly ? "set" : "clear")} readonly
        //                    ";

        //    var temp = Path.GetTempFileName();
        //    File.WriteAllText(temp, script);

        //    var psi = new ProcessStartInfo
        //    {
        //        FileName = "diskpart.exe",
        //        Arguments = $"/s \"{temp}\"",
        //        Verb = "runas",
        //        UseShellExecute = true,
        //        CreateNoWindow = true,
        //        WindowStyle = ProcessWindowStyle.Hidden
        //    };

        //    try
        //    {
        //        using var proc = Process.Start(psi);
        //        proc?.WaitForExit();
        //    }
        //    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        //    {
        //    }
        //    finally
        //    {
        //        if (File.Exists(temp))
        //            File.Delete(temp);
        //    }
        //}

        [RelayCommand]
        void Save()
        {
            if (VolumeItem is null)
                return;

            if (Status.IsBusy)
                return;

            Status.SetAndNotify(S.Processing);

            try
            {
                VolumeItem.Status.SetAndNotify(StorageItem.S.Processing);

                if (USB_FILE_SYSTEMS[UsbFileSystem] != VolumeItem.FileSystem)
                {
                    var title = "Change Format";
                    var desc = "Formatting this USB drive will permanently delete all stored data.\r\nPlease make sure you have backed up any important files before proceeding.";

                    Window.Tip.Confirm(null, title, desc, () => FormatUSB(VolumeItem, UsbFileSystem));
                }

                if (!string.IsNullOrWhiteSpace(VolumeNameTextBox.Text) && VolumeNameTextBox.Text != VolumeItem.VolumeName)
                    PInvoke.SetVolumeLabel(VolumeItem.Path, VolumeNameTextBox.Text);

                //var curentReadOnly = IsVolumeReadOnly(VolumeItem.Letter);
                //if (curentReadOnly != IsReadOnly)
                //{
                //    SetVolumeReadOnly(VolumeItem.Letter, IsReadOnly);
                //    DriveManager.Inst.Update();
                //}

                VolumeItem.Status.SetAndNotify(StorageItem.S.Processed);
            }
            catch (Exception ex)
            {
                if (ex is ArgumentOutOfRangeException)
                {
                    VolumeItem.Message = string.Format(R.T(L.Premium_Title), Constants.LIMIT_TEXT);
                    VolumeItem.Status.SetAndNotify(StorageItem.S.ProcessStopped);
                }
                else
                {
                    VolumeItem.Message = ex.Message;
                    VolumeItem.Status.SetAndNotify(StorageItem.S.ProcessFailed);
                }
            }

            Status.SetAndNotify(S.Processed);

            Window.Tip.Message(null, R.T(L.Status_Processed));
        }
    }
}