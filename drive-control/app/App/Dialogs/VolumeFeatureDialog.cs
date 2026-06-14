using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Dialogs;
using IOCore.Exs;
using IOCore.Utils;
using IOWMI;
using System;
using System.Linq;
using static IOApp.Features.Share;

namespace IOApp.Dialogs
{
    internal partial class VolumeFeatureDialog : DialogEx
    {
        public IOStatus<VolumeFeatureDialog, S> Status { get; }

        public VolumeItem? VolumeItem { get; set => SetAndNotify(ref field, value); }

        public BaseItemCheckWrapper<VolumeItem> VolumeItems { get; } = new([]);

        public VolumeFeatureDialog(WindowEx window) : base(window)
        {
            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                if (Status.IsProcessing)
                    DriveManager.Inst.Stop();
                else if (Status.IsProcessed)
                {
                    DriveManager.Inst.Start(true);
                    _ = AskSaver.Inst.ToRate(Window, null, 0, true, TimeSpan.FromDays(2));
                }

                Window.SetBusy(Status.IsBusy);
            };

            VolumeItems.CheckChanged += (_, e) => Notify(nameof(VolumeItems));
        }

        public virtual void Update(DriveData data)
        {
            var newVolumeItems = data.Volumes.Select(volume => new VolumeItem(volume)).ToList();

            if (VolumeItem is not null)
                newVolumeItems.FirstOrDefault(newItem => VolumeItem.DeviceID == newItem.DeviceID && VolumeItem.Name == newItem.Name).Let(_ => VolumeItem = _);

            VolumeItems.Remove(oldItem => !newVolumeItems.Any(newItem => oldItem.DeviceID == newItem.DeviceID && oldItem.Name == newItem.Name));

            VolumeItems.TargetItems.Update(newVolumeItems,
                (oldItem, newItem) => oldItem.DeviceID == newItem.DeviceID && oldItem.Name == newItem.Name,
                (oldItem, newItem) => oldItem.Refresh(newItem.ManagementObject, newItem.DiskCaption, newItem.SerialNumber, newItem.DeviceID));
        }

        [RelayCommand]
        void RemoveVolume(VolumeItem item)
        {
            if (Status.IsBusy)
                return;

            VolumeItems.Remove(item);
            item.IsSelected = false;

            if (VolumeItems.Items.IsEmpty)
                Window.Dialog.Close();
        }
    }
}