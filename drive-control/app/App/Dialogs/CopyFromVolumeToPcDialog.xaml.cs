using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Core;
using IOCore.Gens;
using IOCore.Utils;
using IOWMI;
using System;
using System.Collections.Generic;
using System.IO;
using ZAnnotation.IOCore;
using static IOApp.Features.RoboCommandHelper;
using static IOApp.Features.Share;

namespace IOApp.Dialogs
{
    [BindingProxy]
    internal sealed partial class CopyFromVolumeToPcDialog : VolumeFeatureDialog
    {
        public AppData AppData => AppSaver.Inst.Saver.Data;

        public CopyFromVolumeToPcDialog(WindowEx window, VolumeItem? item, IEnumerable<VolumeItem> items) : base(window)
        {
            InitializeComponent();
            DataContext = this;

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
        void UpdateOutputPath() => Dispatch.Async(async () => await Window.Picker.OpenSingleFolder(null, pickFolderResult => AppData.OutputPath = pickFolderResult.Path));

        [RelayCommand]
        void Copy() => Dispatch.Async(async () =>
        {
            if (Status.IsBusy)
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
                    if (Window.License.Status.IsTrial)
                        ArgumentOutOfRangeException.ThrowIfGreaterThan(v.UsedSize, Constants.LIMIT);

                    v.Status.SetAndNotify(StorageItem.S.Processing);

                    var dir = Path.Combine(AppData.OutputPath, v.Letter);
                    await FastCopyDirectory(v.Path, dir, false, false, name => DispatcherQueue.TryEnqueue(() => v.Info = name));

                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.Exists)
                    {
                        dirInfo.Attributes &= ~FileAttributes.Hidden;
                        dirInfo.Attributes &= ~FileAttributes.System;
                    }

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