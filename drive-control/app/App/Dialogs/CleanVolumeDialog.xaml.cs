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
using static IOApp.Configs.AppTypes;
using static IOApp.Features.RoboCommandHelper;
using static IOApp.Features.Share;

namespace IOApp.Dialogs
{
    [BindingProxy]
    internal sealed partial class CleanVolumeDialog : VolumeFeatureDialog
    {
        public CleanVolumeDialog(WindowEx window, VolumeItem? item, IEnumerable<VolumeItem> items) : base(window)
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
        void CleanEmptyFolder() => Clean(Window, CleanMode.DeleteEmptyFolders);

        [RelayCommand]
        void CleanAll() => Clean(Window, CleanMode.Empty);

        //

        public void Clean(WindowEx window, CleanMode cleanMode)
        {
            var title = cleanMode is CleanMode.Empty ? L.Features_EmptyDriveTitle : L.Features_DeleteEmptyFoldersTitle;
            var desc = cleanMode is CleanMode.Empty ? L.Features_EmptyDriveDesc : L.Features_DeleteEmptyFoldersDesc;

            window.Tip.Confirm(null,
                string.Format(R.T(title), string.Join(", ", VolumeItems.CheckedItems.Select(i => i.Path))),
                string.Format(R.T(desc), string.Join(", ", VolumeItems.CheckedItems.Select(i => i.Path))),
                async () =>
                {
                    if (Status.IsBusy)
                        return;

                    foreach (var v in VolumeItems.CheckedItems)
                        v.Status.SetAndNotify(StorageItem.S.ProcessInQueue);

                    Status.SetAndNotify(S.Processing);

                    foreach (var v in VolumeItems.CheckedItems)
                    {
                        try
                        {
                            if (v.Status.IsNotProcessInQueue)
                                continue;

                            if (Window.License.Status.IsTrial)
                                ArgumentOutOfRangeException.ThrowIfGreaterThan(v.UsedSize, Constants.LIMIT);

                            v.Status.SetAndNotify(StorageItem.S.Processing);

                            if (cleanMode is CleanMode.Empty)
                            {
                                await FastDeleteDirectory(v.Path, true);
                                v.SecureType = SecureType.Unsecured;
                            }
                            else if (cleanMode is CleanMode.DeleteEmptyFolders)
                                await FastMoveDirectory(v.Path, v.Path, true);

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

                    Window.Tip.Message(null, R.T(L.Status_Processed));

                    Status.SetAndNotify(S.Processed);
                });
        }
    }
}