using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Dialogs;
using IOApp.Features;
using IOCore;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.ConcurrentCollections;
using IOCore.Core;
using IOCore.Exs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using IOData;
using IOImage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static IOApp.Features.Share;

namespace IOApp.Pages
{
    internal sealed partial class Home : MainWindowPage
    {
        public AppLocalStorage AppLocalStorage { get; } = EncapsulatedSingleton<AppLocalStorage>.ExposeInstance();

        public SecureContext SecureContext { get; } = SecureContext.Inst;

        public IOStatus<Home, S> Status { get; }

        public ObservableCollectionEx<SecureFileItem> FileItems { get; } = [];

        readonly List<SecureFileItem> _pendingLockedFileItems = [];

        readonly Debounce _debounce = new();

        public bool IsNoSecureItems => SecureContext.Inst.SECURE_ITEMS.Count == 0;
        public string TrialLimitStickyText => string.Format(R.T(L.Features_TrialLimitSticky), $"{SecureContext.Inst.SECURE_ITEMS.Count}/{Constants.TRIAL_LIMIT}");

        public Home()
        {
            InitializeComponent();
            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                Window.SetBusy(Status.IsBusy);
                Window.Cover.ShowLoading(Status.IsBusy);

                if (Status.Any(S.Processing))
                {
                    foreach (var i in FileItems)
                        i.IsEnabled = false;
                }
                else if (Status.Any(S.Processed))
                {
                    foreach (var i in FileItems)
                        i.IsEnabled = true;
                }
                else if (Status.Any(S.ProcessStopped))
                {
                    foreach (var i in FileItems)
                        i.IsEnabled = true;
                }
                else if (Status.Any(S.ProcessFailed))
                {
                    foreach (var i in FileItems)
                        i.IsEnabled = true;
                }
            };
        }

        public ObservableCollectionEx<OptionItem<AppTypes.FileType>> FileTypes { get; } = [];
        int _fileTypeIndex = -1;
        public int FileTypeIndex
        {
            get => _fileTypeIndex;
            set
            {
                if (_fileTypeIndex != value)
                {
                    SetAndNotify(ref _fileTypeIndex, value);
                    ApplyFilter();
                }
            }
        }
        public AppTypes.FileType CurrentFileType => FileTypes.ElementAtOrDefault(_fileTypeIndex)?.Key ?? AppTypes.FileType.All;

        public ObservableCollectionEx<OptionItem<AppTypes.SortType>> SortTypes { get; } = [];
        int _sortTypeIndex = -1;
        public int SortTypeIndex
        {
            get => _sortTypeIndex;
            set
            {
                if (_sortTypeIndex != value)
                {
                    SetAndNotify(ref _sortTypeIndex, value);
                    ApplyFilter();
                }
            }
        }
        public AppTypes.SortType CurrentSortType => SortTypes.ElementAtOrDefault(_sortTypeIndex)?.Key ?? AppTypes.SortType.Newest;

        protected override void OnFirstLoaded()
        {
            FileTypes.Replace(AppTypes.FILTERS.Select(i => new OptionItem<AppTypes.FileType>(i.Key, R.T(i.Value), true)));
            FileTypeIndex = 0;

            SortTypes.Replace(AppTypes.SORTS.Select(i => new OptionItem<AppTypes.SortType>(i.Key, R.T(i.Value), true)));
            SortTypeIndex = 0;

            void fileItemsCollectionChangedAction()
            {
                Notify(nameof(FileItems), nameof(TrialLimitStickyText), nameof(IsNoSecureItems));
            }
            ;

            FileItems.CollectionChanged += (sender, e) => fileItemsCollectionChangedAction();
            fileItemsCollectionChangedAction();

            void sourceFileItemsCollectionChangedAction(ConcurrentListExCollectionChangedEventArgs<SecureFileItem> e)
            {
                if (e.Action == ConcurrentListExChangedAction.Add)
                    FileItems.AddIfNotExisted(e.Items ?? []);
                else if (e.Action == ConcurrentListExChangedAction.Clear)
                    FileItems.Clear();
                else if (e.Action == ConcurrentListExChangedAction.Move)
                {
                    if (e.OldIndex >= 0 && e.NewIndex >= 0 && e.OldIndex < FileItems.Count && e.NewIndex < FileItems.Count)
                    {
                        var item = FileItems[e.OldIndex];
                        FileItems.RemoveAt(e.OldIndex);
                        FileItems.Insert(e.NewIndex, item);
                    }
                }
                else if (e.Action == ConcurrentListExChangedAction.Remove)
                    FileItems.RemoveEx(e.Items ?? []);
            }
            SecureContext.SECURE_ITEMS.CollectionChanged += (sender, e) => sourceFileItemsCollectionChangedAction(e);

            DBManager.Inst.Execute<AppDbContext>(context => LoadFiles([.. context.Files]));
        }

        protected override void OnNavigatedToEx(NavigationEventArgs e)
        {
            _lzQueue.Resume();
            Window.SetBusy(Status.IsBusy);
        }

        protected override void OnNavigatingFromEx(NavigatingCancelEventArgs e)
        {
            _lzQueue.Stop(true);
        }

        protected override void OnLicenseStatusChanged()
        {
            if (Window.License.Status.IsTrial)
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
                DragDrop.Register(panel, storagePaths =>
                {
                    if (storagePaths.IsNotNullAndNotEmpty())
                    {
                        if (storagePaths.IsNotNullAndNotEmpty())
                            AddFiles([.. storagePaths], null, null);
                    }
                });
            });

        public void ApplyFilter()
        {
            try
            {
                Status.SetAndNotify(S.Loading);

                var filteredItems = new List<SecureFileItem>();

                filteredItems.AddRange(SecureContext.SECURE_ITEMS);

                if (CurrentFileType != AppTypes.FileType.All)
                {
                    filteredItems.RemoveAll(CurrentFileType switch
                    {
                        AppTypes.FileType.Video => i => i.FileType != ZFile.FileType.Video,
                        AppTypes.FileType.Audio => i => i.FileType != ZFile.FileType.Audio,
                        AppTypes.FileType.Image => i => i.FileType != ZFile.FileType.Image,
                        _ => _ => false,
                    });
                }

                filteredItems.Sort(CurrentSortType switch
                {
                    AppTypes.SortType.Newest => (a, b) => b.EncryptedInfo!.LastWriteTime.CompareTo(a.EncryptedInfo!.LastWriteTime),
                    AppTypes.SortType.Oldest => (a, b) => a.EncryptedInfo!.LastWriteTime.CompareTo(b.EncryptedInfo!.LastWriteTime),
                    AppTypes.SortType.A2Z => (a, b) => a.OriginalInfo!.Name.CompareTo(b.OriginalInfo!.Name),
                    AppTypes.SortType.Z2A => (a, b) => b.OriginalInfo!.Name.CompareTo(a.OriginalInfo!.Name),
                    _ => (a, b) => 1,
                });

                FileItems.Replace(filteredItems);

                Status.SetAndNotify(S.Loaded);
            }
            catch
            {
                Status.SetAndNotify(S.LoadFailed);
            }
        }

        public void AddFiles(IEnumerable<string> paths, Action<IEnumerable<SecureFileItem>>? packageAction, AppTypes.SortType? sortType)
        {
            if (paths.IsNullOrEmpty())
            {
                Status.SetAndNotify(S.Loaded);
                return;
            }

            if (Window.License.Status.IsTrial)
            {
                if (SecureContext.SECURE_ITEMS.Count >= Constants.TRIAL_LIMIT)
                {
                    Window.Tip.Confirm(null,
                        string.Format(R.T(L.Features_TrialLimitReachedTitle), SecureContext.SECURE_ITEMS.Count, Constants.TRIAL_LIMIT),
                        R.T(L.Features_TrialLimitReachedSubTitle),
                        async () => await Window.Dialog.Open(PremiumDialog.Create(Window)));

                    return;
                }
                else if (SecureContext.SECURE_ITEMS.Count + paths.Count() >= Constants.TRIAL_LIMIT)
                    paths = [.. paths.Take(Constants.TRIAL_LIMIT - SecureContext.SECURE_ITEMS.Count)];
            }

            Status.SetAndNotify(S.Loading);

            var hasCorrupted = false;

            var locker = new object();

            var coreCount = Environment.ProcessorCount;
            var pathCount = paths.Count();

            Window.Cover.ShowLoading(true, $"-/{pathCount}", Cover.CanvasType.Opacity);

            _pendingLockedFileItems.Clear();

            async void ui(S s)
            {
                if (_pendingLockedFileItems.Count > 0)
                    await Window.Dialog.Open(new UnlockDialog(Window, _pendingLockedFileItems));

                if (s == S.Loaded)
                {
                    if (hasCorrupted)
                        Window.Tip.Message(null, string.Empty, R.T(L.LoadCorruptedSomeFiles));

                    ApplyFilter();

                    if (Window.License.Status.IsTrial)
                    {
                        if (SecureContext.SECURE_ITEMS.Count <= Constants.TRIAL_LIMIT)
                            _ = Ask.Inst.ToRate(Window, null, 1, true, TimeSpan.FromDays(2));
                    }
                    else
                        _ = Ask.Inst.ToRate(Window, null, 1, true, TimeSpan.FromDays(7));
                }

                _pendingLockedFileItems.Clear();

                Status.SetAndNotify(s);
            }
            ;

            Task.Run(async () =>
            {
                try
                {
                    var phases = paths.Phases(null);

                    foreach (var (phase, i) in phases.Select((value, i) => (value, i)))
                    {
                        foreach (var package in phase)
                        {
                            var items = new List<SecureFileItem>();

                            foreach (var itemsPerProcess in package.Chunk(coreCount))
                            {
                                await Parallel.ForEachAsync(itemsPerProcess, async (path, ct) =>
                                {
                                    SecureFileItem? item = null;

                                    try
                                    {
                                        item = new SecureFileItem(path);
                                        if (item is null)
                                            throw new Exception($"Failed to create {path}.");

                                        if (item.Footer is null)
                                        {
                                            item.CorrectFileType(item.InputInfo.IsFile ? ZFile.GetTypeByExtension(item.InputInfo.Extension) : ZFile.FileType.Directory);

                                            var encryptionFolderPath = Path.Combine(AppDir.Get(AppDir.Type.LocalFolder), "Encrypted");
                                            FileUtils.CreateDirectoryIfNotExist(encryptionFolderPath);

                                            var outputFilePath = Path.Combine(encryptionFolderPath, Guid.NewGuid().ToString());

                                            await CypherFileItem.EncodeOne(item.InputInfo.FullName, outputFilePath, AppLocalStorage.Password, true);

                                            item.InputInfo.Refresh(outputFilePath);
                                            item.EncryptedInfo.Refresh(outputFilePath);

                                            if (item.TryLoadFooter(true))
                                                item.LoadThumbnailFromFooter(Constants.THUMBNAIL_ENCRYPT_PASSWORD);

                                            if (item.Footer is null || item.Footer.Metadata is null)
                                                throw new Exception($"Failed to encrypt {path}.");

                                            item.CorrectFileType(item.Footer.Metadata.FileType);
                                            item.OriginalInfo.Refresh(item.Footer.Metadata.OriginalName);

                                            FileUtils.Delete(item.OriginalInfo.FullName);
                                        }
                                        else
                                        {
                                            var inputPath = item.InputInfo.FullName;

                                            var originalFooter = FileFooter.Create(inputPath);
                                            if (originalFooter is null || originalFooter.Metadata is null || originalFooter.Extra is null)
                                                throw new Exception($"Failed to load footer from {path}.");

                                            if (originalFooter.Extra.IsThumbnailEncrypted)
                                                originalFooter.FooterThumbnailBuffer = CryptographyUtils.DecryptToBytes(originalFooter.FooterThumbnailBuffer, AppLocalStorage.Password, Constants.SALT, Constants.ITERATIONS, true) ?? [];

                                            var decryptedFilePath = AppDir.GetFilePath(AppDir.Type.TemporaryFolder, $"{Guid.NewGuid()}");
                                            if (originalFooter.Metadata.IsFile)
                                                decryptedFilePath = Path.ChangeExtension(decryptedFilePath, Path.GetExtension(originalFooter.Metadata.OriginalName));

                                            await CypherFileItem.DecodeOne(inputPath, decryptedFilePath, AppLocalStorage.Password, true);

                                            var encryptedFilePath = AppDir.GetFilePath(AppDir.Type.LocalFolder, "Encrypted", $"{Guid.NewGuid()}");
                                            await CypherFileItem.EncodeOne(decryptedFilePath, encryptedFilePath, AppLocalStorage.Password, true);

                                            var newFooter = FileFooter.Create(encryptedFilePath);
                                            if (newFooter is null || newFooter.Metadata is null || newFooter.Extra is null)
                                                throw new Exception($"Failed to load footer from {path}.");

                                            FileUtils.RemoveLastBytesFromFile(encryptedFilePath, newFooter.Size);
                                            FileFooter.AppendToFile(encryptedFilePath, originalFooter.Metadata, originalFooter.FooterThumbnailBuffer);

                                            item.InputInfo.Refresh(encryptedFilePath);
                                            item.EncryptedInfo.Refresh(encryptedFilePath);

                                            if (item.TryLoadFooter(true))
                                                item.LoadThumbnailFromFooter(Constants.THUMBNAIL_ENCRYPT_PASSWORD);

                                            if (item.Footer is null || item.Footer.Metadata is null || item.Footer.Extra is null)
                                                throw new Exception($"Failed to load footer from {path}.");

                                            item.CorrectFileType(item.Footer.Metadata.FileType);
                                            item.OriginalInfo.Refresh(item.Footer.Metadata.OriginalName);

                                            FileUtils.Delete(inputPath);
                                            FileUtils.Delete(decryptedFilePath);
                                        }

                                        item.Status.Set(FileItem.S.Ready);

                                        if (FileUtils.IsFile(item.EncryptedInfo.FullName))
                                        {
                                            lock (locker)
                                            {
                                                items.Add(item);
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        item?.Status.Set(FileItem.S.ProcessFailed);

                                        if (item is not null && (e is CryptographicException || e is ApplicationException || e is PasswordException))
                                        {
                                            lock (locker)
                                            {
                                                _pendingLockedFileItems.Add(item);
                                            }
                                        }
                                        else
                                            hasCorrupted = true;
                                    }
                                });
                            }

                            if (sortType is not null)
                                items.Sort(sortType switch
                                {
                                    AppTypes.SortType.Newest => (a, b) => b.EncryptedInfo!.CreationTime.CompareTo(a.EncryptedInfo!.CreationTime),
                                    AppTypes.SortType.Oldest => (a, b) => a.EncryptedInfo!.CreationTime.CompareTo(b.EncryptedInfo!.CreationTime),
                                    AppTypes.SortType.A2Z => (a, b) => a.InputInfo.Name.CompareTo(b.InputInfo.Name),
                                    AppTypes.SortType.Z2A => (a, b) => b.InputInfo.Name.CompareTo(a.InputInfo.Name),
                                    _ => (a, b) => 1,
                                });

                            DBManager.Inst.Execute<AppDbContext>(context =>
                            {
                                context.Files.AddRange(items.Select(i => new SecureFileEntity(i.EncryptedInfo!.FullName)
                                {
                                    FileType = i.FileType,
                                    CensorType = i.Censor,
                                }));
                                context.SaveChanges();
                            });

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                lock (locker)
                                {
                                    packageAction?.Invoke(items);

                                    SecureContext.SECURE_ITEMS.AddRange(items);
                                    ApplyFilter();

                                    Window.Cover.ShowLoading(true, $"{SecureContext.SECURE_ITEMS.Count}/{pathCount}", Cover.CanvasType.Opacity);
                                }
                            });
                        }
                    }

                    DispatcherQueue.TryEnqueue(() => ui(S.Loaded));
                }
                catch (Exception)
                {
                    DispatcherQueue.TryEnqueue(() => ui(S.LoadFailed));
                }
            });
        }

        public Task LoadFiles(IEnumerable<SecureFileEntity> entities)
        {
            if (entities.IsNullOrEmpty())
            {
                Status.SetAndNotify(S.Loaded);
                return Task.CompletedTask;
            }

            Status.SetAndNotify(S.Loading);

            var hasCorrupted = false;

            var coreCount = Environment.ProcessorCount;
            var pathCount = entities.Count();

            var locker = new object();

            Window.Cover.ShowLoading(true, $"-/{pathCount}");

            return Task.Run(() =>
            {
                try
                {
                    var phases = entities.Phases(null);

                    foreach (var (phase, i) in phases.Select((value, i) => (value, i)))
                    {
                        foreach (var package in phase)
                        {
                            var items = new List<SecureFileItem>();

                            foreach (var itemsPerProcess in package.Chunk(coreCount))
                            {
                                Parallel.ForEach(itemsPerProcess, entity =>
                                {
                                    try
                                    {
                                        var item = new SecureFileItem(entity, true);
                                        if (!item.IsCorrupted)
                                            item.LoadThumbnailFromFooter(Constants.THUMBNAIL_ENCRYPT_PASSWORD);

                                        item.Status.Set(FileItem.S.Ready);

                                        lock (locker)
                                        {
                                            items.Add(item);
                                        }
                                    }
                                    catch
                                    {
                                        hasCorrupted = true;
                                    }
                                });
                            }

                            items.Sort((a, b) => b.EncryptedInfo!.CreationTime.CompareTo(a.EncryptedInfo!.CreationTime));

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                lock (locker)
                                {
                                    SecureContext.SECURE_ITEMS.Add(items);
                                    Window.Cover.ShowLoading(true, $"{SecureContext.SECURE_ITEMS.Count}/{pathCount}");
                                }
                            });
                        }
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (hasCorrupted)
                            Window.Tip.Message(null, string.Empty, R.T(L.LoadCorruptedSomeFiles));

                        Status.SetAndNotify(S.Loaded);
                    });
                }
                catch (Exception)
                {
                    DispatcherQueue.TryEnqueue(() => Status.SetAndNotify(S.LoadFailed));
                }
            });
        }

        [RelayCommand]
        void AddFiles() => AddFilesOrFolder(true);

        [RelayCommand]
        void AddFolder() => AddFilesOrFolder(false);

        async void AddFilesOrFolder(bool addFiles)
        {
            var paths = new List<string>();

            if (addFiles)
            {
                var storageFiles = await Window.Picker.OpenMultipleFiles(picker =>
                {
                    foreach (var i in Profile.INPUT_EXTENSIONS)
                        picker.FileTypeFilter.Add(i);

                    picker.FileTypeFilter.Add("*");
                });

                if (storageFiles is null)
                    return;

                paths.AddRange(storageFiles.Select(i => i.Path));
            }
            else
            {
                var storageFolder = await Window.Picker.OpenSingleFolder();

                if (storageFolder is null)
                    return;

                paths.Add(storageFolder.Path);
            }

            if (paths.Count > 0)
                AddFiles(paths, null, null);
        }

        void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox control)
                return;

            var text = control.Text;

            _debounce.Run(() =>
            {
                DispatcherQueue.TryEnqueue(() => FileItems.Replace(
                    string.IsNullOrWhiteSpace(text)
                    ? SecureContext.SECURE_ITEMS
                    : SecureContext.SECURE_ITEMS.Where(i => i.OriginalInfo!.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase)))
                );
            }, 750);
        }

        void FileGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue)
            {
                if (args?.Item is SecureFileItem item)
                    _lzQueue.Enqueue(new LzAction<bool>(true, p => item.LoadThumbnailFromFooter(Constants.THUMBNAIL_ENCRYPT_PASSWORD)));
            }
        }

        void FileGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var addedItems = e.AddedItems.Cast<SecureFileItem>().ToList();
            foreach (var item in addedItems)
                item.IsSelected = true;

            var removedItems = e.RemovedItems.Cast<SecureFileItem>().ToList();
            foreach (var item in removedItems)
                item.IsSelected = false;
        }

        async void FileControl_OnPlay(object sender, EventArgs e)
        {
            if (sender.GetDataContext() is not SecureFileItem item) return;

            var hasException = false;

            if (!File.Exists(item.RecoveredFileOrFolderPath))
            {
                Status.SetAndNotify(S.Processing);

                await Task.Run(async () =>
                {
                    try
                    {
                        item.RecoveredFileOrFolderPath = AppDir.PGetFilePath(AppDir.Type.TemporaryFolder, $"{Guid.NewGuid()}{item.OriginalInfo!.Extension}");
                        await CypherFileItem.DecodeOne(item.InputInfo.FullName, item.RecoveredFileOrFolderPath, AppLocalStorage.Password, true);
                    }
                    catch (Exception)
                    {
                        hasException = true;
                    }
                });
            }

            Status.SetAndNotify(S.Processed);

            if (hasException)
                Window.Tip.Message(null, R.T(L.LoadCorruptedFile), null, null);
            else if (File.Exists(item.RecoveredFileOrFolderPath))
            {
                if (item.FileType == ZFile.FileType.Image)
                {
                    var imageItem = ImageItem.Create<ViewerItem>(item.RecoveredFileOrFolderPath);
                    imageItem.InputInfo.Analyze();
                    ViewerPage.Open(Window, imageItem, [imageItem]);
                }
                else if (item.FileType is ZFile.FileType.Video or ZFile.FileType.Audio)
                    Window.PlayCommand.Execute(item);
                else
                    SystemUtils.OpenFileWithDefaultApp(item.RecoveredFileOrFolderPath);
            }
        }

        void FileControl_OnBlur(object sender, EventArgs e)
        {
            if (sender.GetDataContext() is not SecureFileItem item) return;

            var censor = item.Censor == SecureFileItem.CensorType.None ? SecureFileItem.CensorType.Blur : SecureFileItem.CensorType.None;

            DBManager.Inst.Execute<AppDbContext>(context =>
            {
                var fileEntity = context.Files.FirstOrDefault(f => f.Path == item.EncryptedInfo!.FullName);
                if (fileEntity is not null)
                {
                    fileEntity.CensorType = censor;
                    context.SaveChanges();

                    DispatcherQueue.TryEnqueue(() => item.Censor = censor);
                }
            });
        }

        void FileControl_OnRemove(object sender, EventArgs e)
        {
            if (sender.GetDataContext() is not SecureFileItem item) return;

            DBManager.Inst.Execute<AppDbContext>(context =>
            {
                var fileEntity = context.Files.FirstOrDefault(i => i.Path == item.EncryptedInfo!.FullName);
                if (fileEntity is not null)
                {
                    context.Files.Remove(fileEntity);
                    context.SaveChanges();

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SecureContext.SECURE_ITEMS.Remove(item);
                        FileItems.Remove(item);
                    });
                }
            });
        }

        void FileControl_OnDelete(object sender, RoutedEventArgs e)
        {
            if (sender.GetDataContext() is not SecureFileItem item) return;

            Window.Tip.Confirm(null, R.T(L.Features_ConfirmPermanentlyDelete), null,
                () =>
                {
                    SecureContext.SECURE_ITEMS.Remove(item);
                    item.Removed();
                    ApplyFilter();
                });
        }

        [RelayCommand]
        void Export() => Window.Navigate(typeof(Exporter), FileItems.Where(i => i.IsSelected).ToList());

        [RelayCommand]
        void Blur() => Blur(true);

        [RelayCommand]
        void Unblur() => Blur(false);

        void Blur(bool blur)
        {
            var selectedItems = FileItems.Where(i => i.IsSelected).ToList();
            foreach (var item in selectedItems)
            {
                if (item.EncryptedInfo is null)
                    continue;

                DBManager.Inst.Execute<AppDbContext>(context =>
                {
                    var fileEntity = context.Files.FirstOrDefault(f => f.Path == item.EncryptedInfo.FullName);
                    if (fileEntity is not null)
                    {
                        fileEntity.CensorType = blur ? SecureFileItem.CensorType.Blur : SecureFileItem.CensorType.None;
                        context.SaveChanges();

                        DispatcherQueue.TryEnqueue(() => item.Censor = fileEntity.CensorType);
                    }
                });
            }
        }

        [RelayCommand]
        void RemoveCorrupted()
        {
            var corruptedItems = SecureContext.SECURE_ITEMS.Where(i => i.IsCorrupted).ToList();
            var corruptedPaths = corruptedItems.Select(i => i.EncryptedInfo?.FullName ?? "");

            DBManager.Inst.Execute<AppDbContext>(context =>
            {
                context.Files.RemoveRange(context.Files.Where(i => corruptedPaths.Contains(i.Path)));
                context.SaveChanges();

                DispatcherQueue.TryEnqueue(() =>
                {
                    foreach (var i in corruptedItems)
                        SecureContext.SECURE_ITEMS.Remove(i);

                    ApplyFilter();
                });
            });
        }

        [RelayCommand]
        void Delete()
        {
            Window.Tip.Confirm(null, R.T(L.PermanentlyDelete), R.T(L.CannotBeUndone), () =>
            {
                var selectedItems = FileItems.Where(i => i.IsSelected).ToList();
                foreach (var item in selectedItems)
                {
                    SecureContext.SECURE_ITEMS.Remove(item);
                    item.Removed();
                }

                ApplyFilter();
            });
        }

        public void Closed()
        {
            _debounce.Dispose();
        }
    }
}