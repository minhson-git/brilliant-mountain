using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Core;
using IOCore.Exs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.UI;
using IOCore.Utils;
using IOWMI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZAnnotation.IOCore;
using static IOApp.Features.RoboCommandHelper;
using static IOApp.Features.Share;

namespace IOApp.Dialogs
{
    [BindingProxy]
    internal sealed partial class CopyFromPcToVolumeDialog : VolumeFeatureDialog
    {
        public bool Overwrite { get; set => SetAndNotify(ref field, value); }

        public ObservableCollectionEx<FileItem> FileItems { get; } = [];

        public CopyFromPcToVolumeDialog(WindowEx window, VolumeItem? item, IEnumerable<VolumeItem> items) : base(window)
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

        void DragDropGrid_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Panel>(panel =>
            {
                panel.Loaded -= DragDropGrid_Loaded;
                DragDrop.Register(panel, paths =>
                    FileItems.Add(
                        paths.Where(path => !FileItems.Any(f => f.InputInfo.FullName == path) && File.Exists(path)).Select(path => new FileItem(path))
                    ));
            });

        void FileItem_Tapped(object sender, TappedRoutedEventArgs e) => sender.LetDataContext<FileItem>(_ => SystemUtils.RevealInFileExplorer(_.InputInfo.FullName));

        [RelayCommand]
        void AddFiles() => Dispatch.Async(async () =>
            await Window.Picker.OpenMultipleFiles(
                picker => picker.FileTypeFilter.Add("*"),
                pickFileResults => FileItems.Add(pickFileResults.Where(s => !FileItems.Any(f => f.InputInfo.FullName == s.Path) && File.Exists(s.Path)).Select(s => new FileItem(s.Path)))
        ));

        [RelayCommand]
        void AddFolder() => Dispatch.Async(async () =>
            await Window.Picker.OpenSingleFolder(null, pickFolderResult =>
            {
                if (!FileItems.Any(f => f.InputInfo.FullName == pickFolderResult.Path && Directory.Exists(pickFolderResult.Path)))
                    FileItems.Add(new FileItem(pickFolderResult.Path));
            }
        ));

        [RelayCommand]
        void EmptyFiles() => Window.Tip.Confirm(null,R.T(L.RemoveAll), null, FileItems.Clear);

        [RelayCommand]
        void RemoveFile(FileItem item) => FileItems.Remove(item);

        [RelayCommand]
        void CopyFiles()
        {
            Dispatch.Async(async () =>
            {
                if (Status.IsBusy)
                    return;

                foreach (var v in VolumeItems.CheckedItems)
                    v.Status.SetAndNotify(StorageItem.S.ProcessInQueue);

                var fileItems = FileItems.Where(i => i.InputInfo.IsFile);
                var folderItems = FileItems.Where(i => i.InputInfo.IsFolder);

                Status.SetAndNotify(S.Processing);

                foreach (var v in VolumeItems.CheckedItems)
                {
                    if (v.Status.IsNotProcessInQueue)
                        continue;

                    try
                    {
                        v.Status.SetAndNotify(StorageItem.S.Processing);

                        foreach (var item in fileItems)
                        {
                            if (Window.License.Status.IsTrial)
                                ArgumentOutOfRangeException.ThrowIfGreaterThan(v.UsedSize, Constants.LIMIT);

                            v.Info = item.InputInfo.Name;

                            if (Overwrite)
                                await FastCopyFile(item.InputInfo.FullName, v.Path);
                            else
                            {
                                var outputPath = Path.Combine(v.Path, item.InputInfo.Name);

                                if (PathUtils.IsRootDirectory(outputPath))
                                    outputPath = Path.Combine(outputPath, v.Letter);

                                await FileUtils.CopyFileAsync(item.InputInfo.FullName, PathUtils.NextAvailablePath(outputPath), Overwrite, true);
                            }
                        }

                        foreach (var item in folderItems)
                        {
                            if (Window.License.Status.IsTrial)
                                ArgumentOutOfRangeException.ThrowIfGreaterThan(v.UsedSize, Constants.LIMIT);

                            v.Info = item.InputInfo.Name;

                            var outputPath = Path.Combine(v.Path, item.InputInfo.Name);

                            if (PathUtils.IsRootDirectory(outputPath))
                                outputPath = Path.Combine(outputPath, item.InputInfo.FullName[0].ToString());

                            if (!Overwrite)
                                outputPath = PathUtils.NextAvailablePath(outputPath);

                            await FastCopyDirectory(item.InputInfo.FullName, outputPath, false, false, name => DispatcherQueue.TryEnqueue(() => v.Info = name));
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
}