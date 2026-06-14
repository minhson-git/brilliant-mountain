using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Core;
using IOCore.Gens;
using System;
using System.Collections.Generic;
using System.Linq;
using ZAnnotation.IOCore;
using static IOApp.Features.RoboCommandHelper;
using static IOApp.Features.Share;

namespace IOApp.Dialogs
{
    [BindingProxy]
    internal sealed partial class SyncVolumeDialog : VolumeFeatureDialog
    {
        public SyncVolumeDialog(WindowEx window, VolumeItem? item, IEnumerable<VolumeItem> items) : base(window)
        {
            InitializeComponent();
            DataContext = this;

            VolumeItem = item;
            VolumeItems.Replace(items);

            foreach (var v in VolumeItems.Items)
            {
                if (item == null)
                    v.IsChecked = true;
                else
                {
                    if (v.Target.DeviceID == item.DeviceID && v.Target.VolumeName == item.VolumeName)
                        v.IsChecked = true;
                }

                v.Target.Status.SetAndNotify(StorageItem.S.Ready);
            }
        }

        [RelayCommand]
        void Sync(object sender)
        {
            if (VolumeItem is null)
                return;

            Window.Tip.Confirm(sender,
                string.Format(R.T(L.Features_SyncTitle), VolumeItem.Path, string.Join(", ", VolumeItems.CheckedItems.Select(i => i.Path))), string.Format(R.T(L.Features_SyncDesc), VolumeItem.Path),
                async () =>
                {
                    if (Status.IsBusy)
                        return;

                    if (VolumeItem is null)
                        return;

                    foreach (var v in VolumeItems.CheckedItems)
                        v.Status.SetAndNotify(StorageItem.S.ProcessInQueue);

                    Status.SetAndNotify(S.Processing);

                    foreach (var v in VolumeItems.CheckedItems)
                    {
                        if (v.Status.IsNotProcessInQueue)
                            continue;

                        try
                        {
                            v.Status.SetAndNotify(StorageItem.S.Processing);

                            await FastCopyDirectory(VolumeItem.Path, v.Path, true, false, name => DispatcherQueue.TryEnqueue(() => v.Info = name));

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
                    }

                    Status.SetAndNotify(S.Processed);

                    Window.Tip.Message(null, R.T(L.Status_Processed));
                });
        }
    }
}