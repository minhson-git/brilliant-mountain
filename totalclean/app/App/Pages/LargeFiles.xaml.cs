using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.AppManager;
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
using WinRT;
using static IOApp.Features.Share;
using static IOCore.Dialogs.AboutDialog;

namespace IOApp.Pages
{
    internal partial class LargeFiles :  MainWindowPage
    {
        public IOStatus<LargeFiles, S> Status { get; }

        public BaseItemCheckWrapper<FolderItem> FolderItemWrapper { get; } = new([]);

        public ListEx<FileSystemItem> SourceLargeFileItems { get; } = [];
        public ObservableCollectionEx<FileSystemItem> LargeFileItems { get; } = [];

        public ObservableCollectionEx<OptionItem<AppTypes.LargeFileSizeType>> SizeFilterItems { get; } = [];
        protected int _sizeFilterItemIndex = -1;
        public int SizeFilterItemIndex
        {
            get => _sizeFilterItemIndex;
            set
            {
                if (_sizeFilterItemIndex != value)
                    SetAndNotify(ref _sizeFilterItemIndex, value);
            }
        }

        public MenuFlyout FileTypeFilterMenuFlyout { get; } = new();
        public AppTypes.FileType FileTypeFilter => FileTypeFilterMenuFlyout.GetCheckedRadioMenuFlyoutItemTagValue(AppTypes.FileType.All);

        string _currentScanningText = "";
        public string CurrentScanningText { get => _currentScanningText; set => SetAndNotify(ref _currentScanningText, value); }

        CancellationTokenSource _cancellationTokenSource = new();

        public int LargeItemsCount => LargeFileItems.Where(i => i.Status.IsNotProcessed && i.IsSelected).Count();
        public long LargeItemsSize => LargeFileItems.Where(i => i.Status.IsNotProcessed && i.IsSelected).Sum(i => i.InputInfo.FileSize);

        int _processedItemsCount = 0;
        public string TrialLimitText => string.Format(R.T(L.TrialListLimit), Constants.TRIAL_FILES_REMOVE_LIMIT);

        public LargeFiles()
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
                foreach (var i in AppTypes.FILE_TYPE_FILTERS)
                {
                    var item = new RadioMenuFlyoutItem()
                    {
                        GroupName = nameof(FileTypeFilterMenuFlyout),
                        Tag = i.Key,
                        Text = R.T(i.Value),
                        IsChecked = i.Key == AppTypes.FileType.All,
                    };

                    item.Click += (sender, _) =>
                    {
                        var filter = sender.As<RadioMenuFlyoutItem>().Tag.As<AppTypes.FileType>();
                        FilterTextBlock.Text = R.T(AppTypes.FILE_TYPE_FILTERS[filter]);
                        ApplyFilter(false);
                    };

                    FileTypeFilterMenuFlyout.Items.Add(item);

                    if (item.IsChecked)
                        FilterTextBlock.Text = R.T(AppTypes.FILE_TYPE_FILTERS[i.Key]);
                }

                SizeFilterItems.Replace(AppTypes.LARGE_FILE_SIZES.Select(i =>
                {
                    var optionItem = new OptionItem<AppTypes.LargeFileSizeType>(i.Key, FileUtils.GetReadableByteSizeText(i.Value));
                    return optionItem;
                }));
                SizeFilterItemIndex = 0;

                FolderItemWrapper.CollectionChanged += (_, _) =>
                {
                    if (FolderItemWrapper.Items.IsEmpty)
                        LargeFileItems.Clear();

                    LargeFileItemsCollectionChanged();
                    Notify(nameof(FolderItemWrapper));
                };

                SourceLargeFileItems.Changed += (_, _) => LargeFileItemsCollectionChanged();
                LargeFileItems.CollectionChanged += (_, _) => LargeFileItemsCollectionChanged();

                LargeFileItemsCollectionChanged();
                Notify(nameof(FolderItemWrapper));
            }

            Window.SetBusy(Status.IsBusy);
        }

        protected override void OnLicenseStatusChanged()
        {
            if (Window.LicenseStatus.IsPremium)
            {
                SizeFilterItems.ForEach(i =>
                {
                    i.Value = i.OriginalValue;
                    i.IsEnabled = true;
                });

                foreach (var i in SourceLargeFileItems)
                    i.IsEnabled = true;
            }

            if (Window.LicenseStatus.IsTrial)
                Promotion.Inst.Offer(items =>
                {
                    PromotionAppItems.Replace(items);
                    Notify(nameof(PromotionAppItems));
                }, 1);
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

        public List<FileSystemItem> FilterLargeFileItems(IEnumerable<FileSystemItem> fileItems)
        {
            var filteredItems = fileItems.ToList();

            AppTypes.LARGE_FILE_SIZES.TryGetValue(SizeFilterItems.GetAtOrDefault(SizeFilterItemIndex, AppTypes.LargeFileSizeType._50MB), out var miniumSize);
            filteredItems.RemoveAll(i => i.InputInfo.FileSize < miniumSize);

            if (FileTypeFilter != AppTypes.FileType.All)
            {
                Predicate<FileSystemItem> predicate = FileTypeFilter switch
                {
                    AppTypes.FileType.Video => (i) => i.FileType != ZFile.FileType.Video,
                    AppTypes.FileType.Audio => (i) => i.FileType != ZFile.FileType.Audio,
                    AppTypes.FileType.Image => (i) => i.FileType != ZFile.FileType.Image,
                    AppTypes.FileType.Document => (i) => i.FileType != ZFile.FileType.Document,
                    AppTypes.FileType.Others => (i) => i.FileType.Anys(ZFile.FileType.Video, ZFile.FileType.Audio, ZFile.FileType.Image, ZFile.FileType.Document),
                    AppTypes.FileType.All => _ => false,
                    _ => _ => false,
                };

                filteredItems.RemoveAll(predicate);
            }

            return filteredItems;
        }

        public void ApplyFilter(bool hardFilter)
        {
            var filteredItems = FilterLargeFileItems(SourceLargeFileItems);

            if (hardFilter)
                LargeFileItems.Replace(filteredItems);
            else
            {
                var removingItems = LargeFileItems.GetRemovingItems(filteredItems);
                var addingItems = LargeFileItems.GetAddingItems(filteredItems);

                LargeFileItems.RemoveEx(removingItems);
                LargeFileItems.Add(addingItems);
            }
        }

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

            _cancellationTokenSource = new();

            startAction?.Invoke();

            var locker = new object();
            var coreCount = Environment.ProcessorCount;

            var largeFileItems = LargeFileItems.ToList();

            return Task.Run(() =>
            {
                try
                {
                    var phases = largeFileItems.Phases(null);

                    foreach (var phase in phases)
                    {
                        foreach (var package in phase)
                        {
                            var items = new ConcurrentQueue<FileSystemItem>();

                            foreach (var itemsPerProcess in package.Chunk(coreCount))
                            {
                                if (_cancellationTokenSource.IsCancellationRequested)
                                    throw new OperationCanceledException();

                                Parallel.ForEach(itemsPerProcess, fileItem =>
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
                                        fileItem.Status.Set(FileItem.S.ProcessFailed);
                                    }

                                    items.Enqueue(fileItem);
                                });
                            }

                            DispatcherQueue.TryEnqueue(() => packageAction?.Invoke(items));
                        }
                    }

                    DispatcherQueue.TryEnqueue(() => endAction?.Invoke(S.Processed, false));
                }
                catch (Exception)
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
            {
                SourceLargeFileItems.Clear();
                LargeFileItems.Clear();
                return;
            }

            Scan(folderPaths);
        }

        async void Scan(List<string> folderPaths)
        {
            if (Status.IsBusy)
                return;

            Status.SetAndNotify(S.Loading);

            _cancellationTokenSource = new();

            var exceptions = new List<Exception>();
            var coreCount = Environment.ProcessorCount;

            try
            {
                var folderFilesDict = await Task.Run(() => GetPathMapFromFolders(folderPaths, _cancellationTokenSource.Token), _cancellationTokenSource.Token) ?? throw new Exception();
                
                var minFileSize = AppTypes.LARGE_FILE_SIZES.FirstOrDefault().Value;

                foreach (var folderFiles in folderFilesDict)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    var folderItem = FolderItemWrapper.Items.FirstOrDefault(i => PathUtils.Is(i.Target.InputInfo.FullName, folderFiles.Key))?.Target;
                    if (folderItem is null)
                        continue;

                    var notExistedFileItems = folderItem.FileItems.ExceptBy(folderFiles.Value, i => i.InputInfo.FullName).ToList();
                    folderItem.FileItems.RemoveEx(notExistedFileItems);

                    var locker = new object();
                    var phases = folderFiles.Value.Phases(null);

                    foreach (var phase in phases)
                    {
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        foreach (var package in phase)
                        {
                            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                            var chunk = package.Chunk(coreCount).ToList();
                            foreach (var itemsPerProcess in chunk)
                            {
                                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                                await Parallel.ForEachAsync(itemsPerProcess, _cancellationTokenSource.Token, (filePath, ct) =>
                                {
                                    try
                                    {
                                        ct.ThrowIfCancellationRequested();

                                        var fileInfo = new FileInfo(filePath);
                                        if (fileInfo.Length < minFileSize)
                                            return ValueTask.CompletedTask;

                                        if (folderItem.FileItems.FirstOrDefault(i => PathUtils.Is(i.InputInfo.FullName, filePath)) is FileSystemItem fileItem)
                                            fileItem.Refresh(filePath, false);
                                        else
                                        {
                                            var item = FileSystemItem.Create(filePath, false);
                                            if (item is not null)
                                            {
                                                lock (locker)
                                                {
                                                    folderItem.FileItems.AddIfNotExisted(item);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        exceptions.Add(ex);
                                    }

                                    return ValueTask.CompletedTask;
                                });
                            }

                            var checkedFolderItems = FolderItemWrapper.CheckedItems.ToList();
                            foreach (var checkedFolderItem in checkedFolderItems)
                            {
                                SourceLargeFileItems.Remove(SourceLargeFileItems.GetRemovingItems(checkedFolderItem.FileItems));
                                SourceLargeFileItems.AddIfNotExisted(SourceLargeFileItems.GetAddingItems(checkedFolderItem.FileItems));

                                var filteredItems = FilterLargeFileItems(checkedFolderItem.FileItems);
                          
                                LargeFileItems.RemoveEx(LargeFileItems.GetRemovingItems(filteredItems));
                                LargeFileItems.AddIfNotExisted(LargeFileItems.GetAddingItems(filteredItems));
                            }
                        }
                    }
                }

                if (LargeFileItems.Count == 0)
                    Window.Tip.Message(null, R.T(L.Features_NoLargeFilesFound));

                SourceLargeFileItems.Replace([.. SourceLargeFileItems.OrderByDescending(item => item.InputInfo.FileSize)]);
                LargeFileItems.Replace([.. LargeFileItems.OrderByDescending(item => item.InputInfo.FileSize)]);

                Status.SetAndNotify(S.Loaded);
            }
            catch (Exception)
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
            if (parameter == "SelectAll")
            {
                var enabledFileItems = LargeFileItems.Where(i => i.IsEnabled).ToList();
                foreach (var i in enabledFileItems)
                    i.IsSelected = true;
            }
            else if (parameter == "DeselectAll")
            {
                var enabledFileItems = LargeFileItems.Where(i => i.IsEnabled).ToList();
                foreach (var i in enabledFileItems)
                    i.IsSelected = false;
            }

            LargeFileItemsCollectionChanged();
        }

        [RelayCommand]
        void SelectFile(FileSystemItem item)
        {
            item.IsSelected = !item.IsSelected;
            LargeFileItemsCollectionChanged();
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

        void LargeFileItemsCollectionChanged()
        {
            Notify(nameof(SourceLargeFileItems), nameof(LargeFileItems), nameof(LargeItemsCount), nameof(LargeItemsSize));
        }

        [RelayCommand]
        void Clean(object sender)
        {
            if (Status.IsProcessing)
                return;

            Window.Tip.Confirm(null, R.T(L.Notification_RemoveLargeFiles), R.T(L.Notification_RemoveSub), () =>
            {
                if (Status.Any(S.Loading)) return;

                var folderItems = FolderItemWrapper.CheckedItems.ToList();
                if (folderItems.IsNullOrEmpty()) return;

                Status.SetAndNotify(S.Processing);

                var largeFileItems = folderItems.SelectMany(i => i.FileItems).Distinct().Where(i => i.IsSelected).ToList();

                var duplicatedItemsCount = LargeItemsCount;
                var duplicatedItemsSize = LargeItemsSize;

                _cancellationTokenSource = new();

                var locker = new object();
                var coreCount = Environment.ProcessorCount;

                _ = Task.Run(() =>
                {
                    try
                    {
                        var phases = largeFileItems.Phases(null);

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
                                            fileItem.Status.Set(FileItem.S.ProcessFailed);
                                        }
                                    }
                                }

                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    foreach (var item in items)
                                        item.Notify(null);
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

    internal class CleanLargeFilesProxy : BindingProxy<LargeFiles> { }
}
