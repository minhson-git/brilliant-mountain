using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Dialogs;
using IOApp.Features;
using IOCore;
using IOCore.Core;
using IOCore.Exs;
using IOCore.Gens;
using IOCore.UI;
using IOCore.Utils;
using IOWMI;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZAnnotation.IOCore;
using static IOApp.Features.Share;

namespace IOApp.Pages
{
    [BindingProxy]
    internal partial class Disks : MainWindowPage
    {
        public IOStatus<Disks, S> Status { get; }

        public ObservableCollectionEx<DiskItem> DiskItems { get; } = [];

        public DiskItem? SelectedDiskItem
        {
            get;
            set
            {
                var oldField = field;

                if (SetAndNotify(ref field, value ?? DiskItems.FirstOrDefault()))
                {
                    oldField?.IsSelected = false;
                    field?.IsSelected = true;
                }
            }
        }

        public Disks()
        {
            InitializeComponent();
            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                if (Status.IsProcessed)
                    _ = AskSaver.Inst.ToRate(Window, null, 0, true, TimeSpan.FromDays(2));

                Window.Cover.ShowLoading(Status.IsBusy, Cover.CanvasType.Opacity);
                Window.SetBusy(Status.IsBusy);
            };
        }

        protected override void OnNavigatedToEx(NavigationEventArgs e)
        {
            DriveManager.Inst.Execute((data) =>
            {
                DiskItems.Replace(data.Disks.Select(disk => new DiskItem(disk)));
                SelectedDiskItem = DiskItems.FirstOrDefault();
            });

            Window.Cover.ShowLoading(Status.IsBusy, Cover.CanvasType.Opacity);
            Window.SetBusy(Status.IsBusy);
        }

        public void Update(DriveData data)
        {
            var newDiskItems = data.Disks.Select(disk => new DiskItem(disk)).ToList();

            DiskItems.Remove(oldItem => !newDiskItems.Any(newItem => newItem.DeviceID == oldItem.DeviceID));
            DiskItems.Add(newDiskItems.Where(newItem => !DiskItems.Any(oldItem => newItem.DeviceID == oldItem.DeviceID)));
        }

        public async void EjectVolumeItems(IEnumerable<VolumeItem> items)
        {
            if (!items.Any())
                return;

            if (Status.IsBusy)
                return;

            Status.SetAndNotify(S.Processing);

            DriveManager.Inst.Stop();

            foreach (var v in items)
                try
                {
                    await Task.Run(() => DriveEjector.Eject(v.Path, 3, 500, true));

                    v.Status.SetAndNotify(StorageItem.S.Processed);
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentOutOfRangeException)
                    {
                        v.Message = string.Format(R.T(L.Premium_Title), Constants.LIMIT_TEXT);
                        v.Status.SetAndNotify(StorageItem.S.ProcessStopped);
                    }
                    else
                    {
                        v.Message = ex.Message;
                        v.Status.SetAndNotify(StorageItem.S.ProcessFailed);
                    }
                }

            DriveManager.Inst.Start(true);

            Status.SetAndNotify(S.Processed);
        }

        [RelayCommand]
        void Eject(string mode)
        {
            var volumes = new List<VolumeItem>();

            if (mode == "One")
            {
                if (SelectedDiskItem is not null)
                    volumes.AddRange(SelectedDiskItem.VolumeItems);
            }
            else if (mode == "All")
            {
                foreach (var disk in DiskItems)
                    volumes.AddRange(disk.VolumeItems);
            }

            EjectVolumeItems(volumes);
        }

        [RelayCommand]
        void ShowCleanUSBDialog()
        {
            if (SelectedDiskItem is null)
                return;

            var volumes = SelectedDiskItem.VolumeItems;

            if (volumes.IsEmpty)
                return;

            _ = Window.Dialog.Open(new CleanVolumeDialog(Window, null, volumes));
        }

        [RelayCommand]
        void ShowCopyFromPcToVolume()
        {
            if (SelectedDiskItem is null)
                return;

            var volumes = SelectedDiskItem.VolumeItems;

            if (volumes.IsEmpty)
                return;

            _ = Window.Dialog.Open(new CopyFromPcToVolumeDialog(Window, null, volumes));
        }

        [RelayCommand]
        void ShowCopyFromVolumeToPc()
        {
            if (SelectedDiskItem is null)
                return;

            var volumes = SelectedDiskItem.VolumeItems;

            if (volumes.IsEmpty)
                return;

            _ = Window.Dialog.Open(new CopyFromVolumeToPcDialog(Window, null, volumes));
        }

        [RelayCommand]
        void ShowSecureVolume()
        {
            if (SelectedDiskItem is null)
                return;

            var volumes = SelectedDiskItem.VolumeItems;

            if (volumes.IsEmpty)
                return;

            _ = Window.Dialog.Open(new SecureVolumeDialog(Window, null, volumes));
        }

        [RelayCommand]
        void ShowSettingsVolume(VolumeItem item)
        {
            if (item is null)
                return;

            _ = Window.Dialog.Open(new SettingUSBDialog(Window, item));
        }

        [RelayCommand]
        void ShowSyncVolumes(VolumeItem item)
        {
            if (SelectedDiskItem is null || item is null)
                return;

            var volumes = SelectedDiskItem.VolumeItems.Where(i => i != item);

            if (volumes.Any())
                return;

            _ = Window.Dialog.Open(new SyncVolumeDialog(Window, item, volumes));
        }

        [RelayCommand]
        void RevealInFileExplorerVolume(VolumeItem item) => SystemUtils.RevealInFileExplorer(item.Path);
    }
}