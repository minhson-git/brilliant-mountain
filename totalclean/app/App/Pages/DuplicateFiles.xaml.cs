using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Windows.System;
using static IOApp.Features.Share;

namespace IOApp.Pages
{
    internal partial class DuplicateFiles : MainWindowPage
    {
        public IOStatus<DuplicateFiles, S> Status { get; }

        public BaseItemCheckWrapper<FolderItem> FolderItemWrapper { get; } = new([]);

        public ObservableCollectionEx<DuplicatedGroupItem> DuplicatedGroupItems { get; } = [];

        string _currentScanningText = "";
        public string CurrentScanningText { get => _currentScanningText; set => SetAndNotify(ref _currentScanningText, value); }

        CancellationTokenSource _cancellationTokenSource = new();

        public int DuplicatedItemsCount => DuplicatedGroupItems.SelectMany(i => i.FileItems.Where(i => i.Status.IsNotProcessed && i.IsSelected)).Count();
        public long DuplicatedItemsSize => DuplicatedGroupItems.SelectMany(i => i.FileItems.Where(i => i.Status.IsNotProcessed && i.IsSelected)).Sum(i => i.InputInfo.FileSize);

        int _processedItemsCount = 0;
        public string TrialLimitText => string.Format(R.T(L.TrialListLimit), Constants.TRIAL_FILES_REMOVE_LIMIT);

        public DuplicateFiles()
        {
            InitializeComponent();
            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) => Window.SetBusy(Status.IsBusy);
        }

        protected override void OnNavigatedToEx(Frame sender, object? parameter, NavigationEventArgs e)
        {
            if (NavigateCount == 1)
            {
                FolderItemWrapper.CollectionChanged += (_, _) =>
                {
                    if (FolderItemWrapper.Items.IsEmpty)
                        DuplicatedGroupItems.Clear();

                    DuplicatedGroupItemsCollectionChanged();
                    Notify(nameof(FolderItemWrapper));
                };

                DuplicatedGroupItems.CollectionChanged += (_, _) => DuplicatedGroupItemsCollectionChanged();

                DuplicatedGroupItemsCollectionChanged();
                Notify(nameof(FolderItemWrapper));
            }

            Window.SetBusy(Status.IsBusy);
        }

        void DragDropGrid_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Panel>(panel =>
            {
                panel.Loaded -= DragDropGrid_Loaded;
                DragDrop.Register(panel, storageItems =>
                {
                    AddFolders(storageItems.Select(i => i.Path));
                    Notify(nameof(FolderItemWrapper));
                });
            });

        [RelayCommand]
        void AddFolder() => _ = Window.Picker.OpenSingleFolder(null, storageFolder => AddFolders([storageFolder.Path]));

        public void AddFolders(IEnumerable<string> paths)
        {
            var existedItems = FolderItemWrapper.Items.Where(i => PathUtils.IsIn(i.Target.InputInfo.FullName, paths));
            existedItems.ForEach(i => i.IsChecked = true);

            var duplicatedPaths = existedItems.Select(i => i.Target.InputInfo.FullName);

            var folderItems = new List<FolderItem>();

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    if (PathUtils.IsIn(path, duplicatedPaths))
                        FolderItemWrapper.Items.FirstOrDefault(i => PathUtils.Is(i.Target.InputInfo.FullName, path)).Let(_ => _.IsChecked = true);
                    else
                        folderItems.Add(new(path) { IsSelected = true });
                }
            }

            FolderItemWrapper.Add(folderItems, _ => _.IsSelected);
        }

        public Task ProcessAll(Action? startAction = null, Action<IEnumerable<FileSystemItem>>? packageAction = null, Action<S, bool>? endAction = null)
        {
            if (Status.IsProcessing)
                return Task.CompletedTask;

            startAction?.Invoke();

            var locker = new object();
            var coreCount = Environment.ProcessorCount;

            var duplicatedFileItems = DuplicatedGroupItems.Aggregate(new List<FileSystemItem>(), (accumulator, item) =>
            {
                accumulator.AddRange(item.FileItems);
                return accumulator;
            });

            return Task.Run(() =>
            {
                try
                {
                    var phases = duplicatedFileItems.Phases(null);

                    foreach (var phase in phases)
                    {
                        foreach (var package in phase)
                        {
                            var items = new ConcurrentQueue<FileSystemItem>();

                            foreach (var itemsPerProcess in package.Chunk(coreCount))
                            {
                                if (_cancellationTokenSource.IsCancellationRequested)
                                    throw new OperationCanceledException();

                                foreach (var fileItem in itemsPerProcess)
                                {
                                    try
                                    {
                                        if (fileItem.IsSelected)
                                        {
                                            fileItem.Delete(false);
                                            fileItem.Status.Set(FileItem.S.Processed);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        DispatcherQueue.TryEnqueue(() => fileItem.Status.Set(FileItem.S.ProcessFailed));
                                    }
                                }
                            }

                            DispatcherQueue.TryEnqueue(() => packageAction?.Invoke(items));
                        }
                    }

                    DispatcherQueue.TryEnqueue(() => endAction?.Invoke(S.Processed, false));
                }
                catch
                {
                    DispatcherQueue.TryEnqueue(() => endAction?.Invoke(S.ProcessFailed, false));
                }
            });
        }

        [RelayCommand]
        void Scan()
        {
            var folderPaths = FolderItemWrapper.CheckedItems.Select(i => i.InputInfo.FullName).ToList();
            if (folderPaths.IsNullOrEmpty())
                return;

            Scan(folderPaths);
        }

        async void Scan(List<string> folderPaths)
        {
            if (Status.IsBusy)
                return;

            Status.SetAndNotify(S.Loading);

            _cancellationTokenSource = new();

            var hasCorrupted = false;

            var locker = new object();
            var coreCount = Environment.ProcessorCount;

            DuplicatedGroupItems.Clear();
            FolderItemWrapper.Items.ForEach(i => i.Target.FileItems.Clear());

            var filePaths = new List<string>();

            filePaths = await Task.Run(() => GetScanningFilePathsFromFolders(folderPaths), _cancellationTokenSource.Token);

            try
            {
                var phases = filePaths.Phases(null);
                foreach (var phase in phases)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    foreach (var package in phase)
                    {
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        var folderFilePathsMap = new ConcurrentDictionary<string, List<FileSystemItem>>();
                        FolderItemWrapper.Items.ForEach(i => folderFilePathsMap.TryAdd(i.Target.InputInfo.FullName, []));

                        foreach (var itemsPerProcess in package.Chunk(coreCount))
                        {
                            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                            await Parallel.ForEachAsync(itemsPerProcess, _cancellationTokenSource.Token, (filePath, ct) =>
                            {
                                try
                                {
                                    ct.ThrowIfCancellationRequested();

                                    var item = FileSystemItem.Create(filePath, true);

                                    folderFilePathsMap.ForEach(i =>
                                    {
                                        if (PathUtils.IsSubPath(i.Key, filePath))
                                            i.Value.Add(item);
                                    });
                                }
                                catch (Exception)
                                {
                                    hasCorrupted = true;
                                }

                                return ValueTask.CompletedTask;
                            });
                        }

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            folderFilePathsMap.Where(i => i.Value.IsNotNullAndNotEmpty()).ForEach(i =>
                            {
                                var existedItem = FolderItemWrapper.Items.FirstOrDefault(folderItem => PathUtils.Is(folderItem.Target.InputInfo.FullName, i.Key));
                                if (existedItem != null)
                                    existedItem.Target.FileItems.Add(i.Value.ExceptBy(existedItem.Target.FileItems.Select(i => i.InputInfo.FullName), i => i.InputInfo.FullName));
                                else
                                {
                                    var folderItem = new FolderItem(i.Key);
                                    folderItem.FileItems.Add(i.Value);
                                    FolderItemWrapper.Add(folderItem);
                                }
                            });

                            BuildDuplicateGroups([.. FolderItemWrapper.CheckedItems]).ForEach(item =>
                            {
                                var existingDuplicatedGroupItem = DuplicatedGroupItems.FirstOrDefault(i => i.Key == item.Key);
                                if (existingDuplicatedGroupItem != null)
                                {
                                    var mergedItems = existingDuplicatedGroupItem.FileItems.Concat(item.FileItems).DistinctBy(i => i.InputInfo.FullName);
                                    existingDuplicatedGroupItem.FileItems.Replace(mergedItems);

                                    Notify(nameof(DuplicatedGroupItems));
                                }
                                else
                                    DuplicatedGroupItems.Add(item);
                            });
                        });
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (DuplicatedGroupItems.Count == 0)
                        Window.Tip.Message(null, R.T(L.Features_NoDuplicateFilesFound));

                    Status.SetAndNotify(S.Loaded);
                });
            }
            catch
            {
                DispatcherQueue.TryEnqueue(() => Status.SetAndNotify(S.LoadFailed));
            }
        }

        [RelayCommand]
        void RemoveFolder(FolderItem item)
        {
            if (Status.IsNotBusy)
                FolderItemWrapper.Remove(item);
        }

        void ListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue)
                if (args?.Item is DuplicatedGroupItem item)
                    item.ThumbnailEnqueued(_lzQueue);
        }

        [RelayCommand]
        void SelectFiles(string parameter)
        {
            if (parameter == "SelectDuplicate")
                DuplicatedGroupItems.ForEach(item => item.FileItems.ForEach((i, index) => i.IsSelected = index != 0));
            else if (parameter == "DeselectAll")
                DuplicatedGroupItems.ForEach(item => item.FileItems.ForEach(i => i.IsSelected = false));

            DuplicatedGroupItemsCollectionChanged();
        }

        [RelayCommand]
        void SelectFile(FileSystemItem item)
        {
            item.IsSelected = !item.IsSelected;
            DuplicatedGroupItemsCollectionChanged();
        }

        [RelayCommand]
        void Cancel()
        {
            if (Status.Not(S.Loading)) return;

            Status.SetAndNotify(S.ProcessStopping);
            _cancellationTokenSource.Cancel();

            FolderItemWrapper.Items.ForEach(i => i.Target.Status.SetAndNotify(FileItem.S.Ready));
            Status.SetAndNotify(S.ProcessStopped);
        }

        [RelayCommand]
        void Open(object sender) =>
            sender.GetDataContext<DuplicatedGroupItem>().Let(item =>
            {
                if (item.FirstFileItem == null)
                    return;

                if (item.FileType == ZFile.FileType.Image)
                    Window.AppBridge.OpenBy(AppBridge.Phototype.PackageId, sender,
                        app => _ = Launcher.LaunchUriAsync(new(string.Format(app.ProtocolUrl, HttpUtility.UrlEncode(item.FirstFileItem.InputInfo.FullName)))),
                        string.Format(R.T(L.InstallPhototypeText), item.FirstFileItem.InputInfo.Extension));
                else if (item.FileType == ZFile.FileType.Video || item.FileType == ZFile.FileType.Audio)
                    Window.AppBridge.OpenBy(AppBridge.Playlist.PackageId, sender,
                        app => _ = Launcher.LaunchUriAsync(new(string.Format(app.ProtocolUrl, HttpUtility.UrlEncode(item.FirstFileItem.InputInfo.FullName)))),
                        string.Format(R.T(L.InstallPlaylistText), R.T(item.FileType == ZFile.FileType.Video, L.Video, L.Audio), item.FirstFileItem.InputInfo.Extension));
            });

        void DuplicatedGroupItemsCollectionChanged() => Notify(nameof(DuplicatedGroupItems), nameof(DuplicatedItemsCount), nameof(DuplicatedItemsSize));

        [RelayCommand]
        void Clean(object sender)
        {
            if (Status.IsProcessing)
                return;

            Window.Tip.Confirm(null, R.T(L.Notification_RemoveDuplicate), R.T(L.Notification_RemoveDuplicateSub), () =>
            {
                if (Status.Any(S.Loading)) return;

                var folderPaths = FolderItemWrapper.Items.Where(i => i.IsChecked).Select(i => i.Target.InputInfo.FullName).ToList();
                if (folderPaths.IsNullOrEmpty()) return;

                Status.SetAndNotify(S.Processing);

                var duplicatedItemsCount = DuplicatedItemsCount;
                var duplicatedItemsSize = DuplicatedItemsSize;

                var locker = new object();
                var coreCount = Environment.ProcessorCount;

                var duplicatedFileItems = DuplicatedGroupItems.Aggregate(new List<FileSystemItem>(), (accumulator, item) =>
                {
                    accumulator.AddRange(item.FileItems);
                    return accumulator;
                });

                _ = Task.Run(() =>
                {
                    try
                    {
                        var phases = duplicatedFileItems.Phases(null);

                        foreach (var phase in phases)
                        {
                            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                            foreach (var package in phase)
                            {
                                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                                var items = new ConcurrentQueue<FileSystemItem>();

                                foreach (var itemsPerProcess in package.Chunk(coreCount))
                                {
                                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                                    foreach (var fileItem in itemsPerProcess)
                                    {
                                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                                        if (Window.LicenseStatus.IsTrial)
                                            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(_processedItemsCount, Constants.TRIAL_FILES_REMOVE_LIMIT);

                                        try
                                        {
                                            if (fileItem.IsSelected)
                                            {
                                                File.SetAttributes(fileItem.InputInfo.FullName, FileAttributes.Normal);
                                                File.Delete(fileItem.InputInfo.FullName);

                                                _processedItemsCount++;

                                                DispatcherQueue.TryEnqueue(() =>
                                                {
                                                    fileItem.IsDeleted = true;
                                                    fileItem.Status.SetAndNotify(FileItem.S.Processed);
                                                });
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            DispatcherQueue.TryEnqueue(() => fileItem.Status.Set(FileItem.S.ProcessFailed));
                                        }
                                    }
                                }

                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    var hashGroupedProcessedItems = items.GroupBy(i => i.Key);
                                    items.GroupBy(i => i.Key).ForEach(groupedItem =>
                                    {
                                        groupedItem.ForEach(i => i.Notify());

                                        var duplicateSetItem = DuplicatedGroupItems.FirstOrDefault(i => i.Key == groupedItem.Key);
                                        if (duplicateSetItem != null)
                                            groupedItem.ForEach(item =>
                                            {
                                                var targetFileItemIndex = duplicateSetItem.FileItems.IndexOf(i => PathUtils.Is(i.InputInfo.FullName, item.InputInfo.FullName));
                                                if (targetFileItemIndex != -1)
                                                    duplicateSetItem.FileItems[targetFileItemIndex] = item;
                                            });
                                    });
                                });
                            }
                        }

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            Window.Tip.Message(null,
                                string.Format(R.T(L.Notification_CleanResultTitle), FileUtils.GetReadableByteSizeText(duplicatedItemsSize)),
                                string.Format(R.T(L.Notification_CleanResultSubtitle), $"{duplicatedItemsCount:n0}"));

                            _ = Ask.Inst.ToRate(Window, null, 0, true, TimeSpan.FromDays(2));

                            Status.SetAndNotify(S.Processed);
                        });
                    }
                    catch (Exception ex)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            Status.SetAndNotify(S.ProcessFailed);

                            if (ex is ArgumentOutOfRangeException)
                            {
                                Window.Tip.Message(null,
                                    R.T(L.TrialListLimit), string.Format(R.T(L.LimitReachedSubtitle), Constants.TRIAL_FILES_REMOVE_LIMIT),
                                    async () => await Window.Dialog.Open(PremiumDialog.Create(Window)));
                            }
                        });
                    }
                });
            });
        }
    }

    internal class DuplicateProxy : BindingProxy<DuplicateFiles> { }
}