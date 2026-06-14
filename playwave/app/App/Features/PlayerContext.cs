using IOApp.Dialogs;
using IOApp.Gens;
using IOApp.Pages;
using IOApp.Windows;
using IOCore;
using IOCore.Annotation;
using IOCore.Base;
using IOCore.Collections;
using IOCore.Colour;
using IOCore.Dialogs;
using IOCore.Files;
using IOCore.Helpers;
using IOCore.Modules.Data;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using XMedia.Media;
using XMedia.Media.PlayerBase;
using static IOApp.Configs.AppTypes;
using static IOCore.Files.MediaFamily;

namespace IOApp.Features;

public class Data
{
    readonly ConcurrentList<PlayerItem> _DATA_ITEMS = [];
    public IReadOnlyList<PlayerItem> DATA_ITEMS => _DATA_ITEMS;

    readonly ConcurrentList<PlaylistItem> _LIST_ITEMS = [];
    public IReadOnlyList<PlaylistItem> LIST_ITEMS => _LIST_ITEMS;

    readonly ConcurrentList<FolderItem> _FOLDER_ITEMS = [];
    public IReadOnlyList<FolderItem> FOLDER_ITEMS => _FOLDER_ITEMS;

    readonly ConcurrentList<PlayerItem> _LIST_DATA_ITEMS = [];
    public IReadOnlyList<PlayerItem> LIST_DATA_ITEMS => _LIST_DATA_ITEMS;

    readonly ConcurrentList<PlayerItem> _FOLDER_DATA_ITEMS = [];
    public IReadOnlyList<PlayerItem> FOLDER_DATA_ITEMS => _FOLDER_DATA_ITEMS;

    readonly ConcurrentList<PlayerItem> _RECENT_DATA_ITEMS = [];
    public IReadOnlyList<PlayerItem> RECENT_DATA_ITEMS => _RECENT_DATA_ITEMS;

    public event TypedEventHandler<ConcurrentList<PlaylistItem>, IEnumerable<PlaylistItem>>? ListItemsAdded
    {
        add => _LIST_ITEMS.ItemsAdded += value;
        remove => _LIST_ITEMS.ItemsAdded -= value;
    }

    public event TypedEventHandler<ConcurrentList<PlaylistItem>, IEnumerable<PlaylistItem>>? ListItemsRemoved
    {
        add => _LIST_ITEMS.ItemsRemoved += value;
        remove => _LIST_ITEMS.ItemsRemoved -= value;
    }

    public event TypedEventHandler<ConcurrentList<FolderItem>, IEnumerable<FolderItem>>? FolderItemsAdded
    {
        add => _FOLDER_ITEMS.ItemsAdded += value;
        remove => _FOLDER_ITEMS.ItemsAdded -= value;
    }

    public event TypedEventHandler<ConcurrentList<FolderItem>, IEnumerable<FolderItem>>? FolderItemsRemoved
    {
        add => _FOLDER_ITEMS.ItemsRemoved += value;
        remove => _FOLDER_ITEMS.ItemsRemoved -= value;
    }

    public event TypedEventHandler<ConcurrentList<PlayerItem>, IEnumerable<PlayerItem>>? ListDataItemsAdded
    {
        add => _LIST_DATA_ITEMS.ItemsAdded += value;
        remove => _LIST_DATA_ITEMS.ItemsAdded -= value;
    }

    public event TypedEventHandler<ConcurrentList<PlayerItem>, IEnumerable<PlayerItem>>? ListDataItemsRemoved
    {
        add => _LIST_DATA_ITEMS.ItemsRemoved += value;
        remove => _LIST_DATA_ITEMS.ItemsRemoved -= value;
    }

    public event TypedEventHandler<ConcurrentList<PlayerItem>, IEnumerable<PlayerItem>>? FolderDataItemsAdded
    {
        add => _FOLDER_DATA_ITEMS.ItemsAdded += value;
        remove => _FOLDER_DATA_ITEMS.ItemsAdded -= value;
    }

    public event TypedEventHandler<ConcurrentList<PlayerItem>, IEnumerable<PlayerItem>>? FolderDataItemsRemoved
    {
        add => _FOLDER_DATA_ITEMS.ItemsRemoved += value;
        remove => _FOLDER_DATA_ITEMS.ItemsRemoved -= value;
    }

    public event TypedEventHandler<ConcurrentList<PlayerItem>, IEnumerable<PlayerItem>>? RecentDataItemsAdded
    {
        add => _RECENT_DATA_ITEMS.ItemsAdded += value;
        remove => _RECENT_DATA_ITEMS.ItemsAdded -= value;
    }

    public event TypedEventHandler<ConcurrentList<PlayerItem>, IEnumerable<PlayerItem>>? RecentDataItemsRemoved
    {
        add => _RECENT_DATA_ITEMS.ItemsRemoved += value;
        remove => _RECENT_DATA_ITEMS.ItemsRemoved -= value;
    }

    public event Action<ConcurrentList<PlayerItem>>? RecentDataItemsMoved
    {
        add => _RECENT_DATA_ITEMS.ItemsMoved += value;
        remove => _RECENT_DATA_ITEMS.ItemsMoved -= value;
    }

    public void RemoveDataItems(IEnumerable<PlayerItem> items)
    {
        _LIST_DATA_ITEMS.RemoveRange(items);
        _FOLDER_DATA_ITEMS.RemoveRange(items);
        _RECENT_DATA_ITEMS.RemoveRange(items);
        _DATA_ITEMS.RemoveRange(items);
    }

    public void AddDataItemsToRecent(IEnumerable<PlayerItem> items)
    {
        _RECENT_DATA_ITEMS.AddRangeIfNotExisted(items);
        _DATA_ITEMS.AddRangeIfNotExisted(items);
    }

    public void InsertDataItemToRecent(int index, PlayerItem item)
    {
        _RECENT_DATA_ITEMS.InsertIfNotExisted(index, item);
        _DATA_ITEMS.AddIfNotExisted(item);
    }

    public void AddItemToTopRecentOrMoveExistingItemToTopRecent(PlayerItem item)
    {
        var recentIndex = _RECENT_DATA_ITEMS.FindIndex(i => PathUtils.Is(i.InputInfo.FullName, item.InputInfo.FullName));
        if (recentIndex == -1)
            InsertDataItemToRecent(0, item);
        else if (recentIndex > 0)
            _RECENT_DATA_ITEMS.Move(recentIndex, 0);
    }

    public void AddDataItemsToList(IEnumerable<PlayerItem> items)
    {
        _LIST_DATA_ITEMS.AddRangeIfNotExisted(items);
        _DATA_ITEMS.AddRangeIfNotExisted(items);
    }

    public void AddDataItemToList(PlayerItem item)
    {
        _LIST_DATA_ITEMS.AddIfNotExisted(item);
        _DATA_ITEMS.AddIfNotExisted(item);
    }

    public void AddDataItemsToFolder(IEnumerable<PlayerItem> items)
    {
        _FOLDER_DATA_ITEMS.AddRangeIfNotExisted(items);
        _DATA_ITEMS.AddRangeIfNotExisted(items);
    }

    //

    public void RemoveDataItemFromFolder(PlayerItem item)
    {
        _FOLDER_DATA_ITEMS.Remove(item);

        if (!_LIST_DATA_ITEMS.Contains(item) && !_RECENT_DATA_ITEMS.Contains(item))
            _DATA_ITEMS.Remove(item);
    }

    public void RemoveDataItemFromList(PlayerItem item)
    {
        _LIST_DATA_ITEMS.Remove(item);

        if (!_FOLDER_DATA_ITEMS.Contains(item) && !_RECENT_DATA_ITEMS.Contains(item))
            _DATA_ITEMS.Remove(item);
    }

    public void RemoveDataItemFromRecent(PlayerItem item)
    {
        _RECENT_DATA_ITEMS.Remove(item);

        if (!_FOLDER_DATA_ITEMS.Contains(item) && !_LIST_DATA_ITEMS.Contains(item))
            _DATA_ITEMS.Remove(item);
    }

    //

    public void AddListItems(IEnumerable<PlaylistItem> items)
    {
        _LIST_ITEMS.AddRangeIfNotExisted(items);
    }

    public void AddListItem(PlaylistItem item)
    {
        _LIST_ITEMS.AddIfNotExisted(item);
    }

    public void RemoveListItem(PlaylistItem item)
    {
        _LIST_ITEMS.Remove(item);
    }

    public void AddFolderItems(IEnumerable<FolderItem> items)
    {
        _FOLDER_ITEMS.AddRangeIfNotExisted(items);
    }

    public void AddFolderItem(FolderItem item)
    {
        _FOLDER_ITEMS.AddIfNotExisted(item);
    }

    public void RemoveFolderItem(FolderItem item)
    {
        _FOLDER_ITEMS.Remove(item);
    }
}

public class PlayerContext : BasePlayerContext<PlayerContext>
{
    PlayerContext() { }

    public readonly Data Data = new();

    public void Init()
    {
        DBManager.I.Enqueue<AppDbContext>(async context =>
        {
            // Query all playlists to check missing built-in playlists

            var playlistEntities = context.Playlists.OrderBy(i => i.Id).Include(i => i.Files).ToList();

            var builtInPlaylistEntities = playlistEntities.Where(i => !string.IsNullOrWhiteSpace(i.Remark)).ToList();
            if (builtInPlaylistEntities.Count < BUILT_IN_PLAYLISTS.Count)
            {
                // Create missing built-in playlists

                var availableRemarks = builtInPlaylistEntities.Select(i => i.Remark).ToList();
                var addingPlaylistEntities = new ListEx<PlaylistEntity>();

                var missingPlaylists = BUILT_IN_PLAYLISTS.Where(i => i.Value.Item2 && !availableRemarks.Contains(i.Key.ToString()));
                foreach (var i in missingPlaylists)
                    addingPlaylistEntities.AddIfNotExisted(new PlaylistEntity(T.S(i.Value.Item1)) { Remark = i.Key.ToString() });

                context.Playlists.AddRange(addingPlaylistEntities);
                context.SaveChanges();

                playlistEntities.AddRange(addingPlaylistEntities);
                builtInPlaylistEntities.AddRange(addingPlaylistEntities);

                playlistEntities.Sort((a, b) => a.Id.CompareTo(b.Id));
            }

            // Get the translated built-in playlists names and assign them to the respective playlists

            foreach (var playlistEntity in builtInPlaylistEntities)
                BUILT_IN_PLAYLISTS
                    .FirstOrDefault(i => i.Value.Item2 && i.Key.ToString() == playlistEntity.Remark)
                    .Let(i => playlistEntity.Name = T.S(i.Value.Item1));

            // Load playlist items and recent media items

            Data.AddListItems(playlistEntities.Select(i => new PlaylistItem(i)));
            if (Data.LIST_ITEMS.Count > 0)
                Data.LIST_ITEMS[0].IsSelected = true;

            await LoadPlaylistItems(Data.LIST_ITEMS, items => Data.AddDataItemsToList(items), null);

            await LoadRecentItems([.. context.Recent.OrderByDescending(i => (long)i.LastOpenedAt)],
                loadedItems =>
                {
                    var loadedListItems = loadedItems.ToList();

                    for (var i = 0; i < loadedListItems.Count; i++)
                    {
                        var samePathPlaylistItemInstance = Data.LIST_DATA_ITEMS.FirstOrDefault(item => PathUtils.Is(item.InputInfo.FullName, loadedListItems[i].InputInfo.FullName));
                        if (samePathPlaylistItemInstance is not null)
                            loadedListItems[i] = samePathPlaylistItemInstance;
                    }

                    Data.AddDataItemsToRecent(loadedListItems);
                },
                null);
        });
    }

    #region PLAYLIST

    static async Task LoadPlaylistItems(
        IEnumerable<PlaylistItem> playlistItems,
        Action<IEnumerable<PlayerItem>>? packageAction = null,
        Action<PlayerEx.S, Exception?, bool>? endAction = null)
    {
        if (!playlistItems.Any())
            return;

        var exceptions = new ConcurrentQueue<Exception>();

        try
        {
            foreach (var playlistItem in playlistItems)
            {
                var phases = playlistItem.Paths.Phases(null);

                foreach (var (phase, i) in phases.Select((value, i) => (value, i)))
                {
                    foreach (var package in phase)
                    {
                        var items = new ConcurrentQueue<PlayerItem>();

                        foreach (var itemsPerProcess in package.Chunk(Environment.ProcessorCount))
                        {
                            await Parallel.ForEachAsync(itemsPerProcess, (path, ct) =>
                            {
                                try
                                {
                                    var samePathInstanceInContext = I.Data.DATA_ITEMS.FirstOrDefault(i => PathUtils.Is(path, i.InputInfo.FullName));

                                    if (samePathInstanceInContext is not null)
                                        items.Enqueue(samePathInstanceInContext);
                                    else
                                    {
                                        var item = new PlayerItem(path);
                                        item.InputInfo.Analyze();

                                        item.PlaylistItems.AddIfNotExisted(playlistItem);

                                        if (!item.InputInfo.IsCorrupted)
                                            items.Enqueue(item);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    exceptions.Enqueue(ex);
                                }

                                return ValueTask.CompletedTask;
                            });
                        }

                        if (!items.IsEmpty)
                            packageAction?.Invoke(items.OrderBy(i => Array.IndexOf(package, i.InputInfo.FullName)));

                        await Task.Delay(100);
                    }
                }
            }

            endAction?.Invoke(PlayerEx.S.Loaded, exceptions.IsEmpty ? null : new AggregateException(exceptions), false);
        }
        catch (Exception ex)
        {
            exceptions.Enqueue(ex);
            endAction?.Invoke(PlayerEx.S.LoadFailed, new AggregateException(exceptions), false);
        }
    }

    public void CreatePlaylist(string playlistName, Action<PlaylistItem>? action)
    {
        DBManager.I.Enqueue<AppDbContext>(context =>
        {
            var playlistEntity = context.Playlists.Add(new(playlistName));
            context.SaveChanges();

            var playlistItem = new PlaylistItem(playlistEntity.Entity);

            Data.AddListItem(playlistItem);

            action?.Invoke(playlistItem);
        });
    }

    public void UpdatePlaylist(PlaylistItem item, Action<PlaylistItem>? action)
    {
        DBManager.I.Enqueue<AppDbContext>(context =>
        {
            var playlistEntity = context.Playlists.FirstOrDefault(i => i.Id == item.Id);
            if (playlistEntity is not null)
            {
                playlistEntity.Name = item.Name;
                context.SaveChanges();
            }

            action?.Invoke(item);
        });
    }

    public void RemovePlaylist(PlaylistItem item, Action? action)
    {
        DBManager.I.Enqueue<AppDbContext>(context =>
        {
            context.PlaylistFiles.RemoveRange(context.PlaylistFiles.Where(i => i.PlaylistId == item.Id));
            context.Playlists.FirstOrDefault(i => i.Id == item.Id).Let(albumEntity => context.Playlists.Remove(albumEntity));

            context.SaveChanges();

            Data.RemoveListItem(item);

            action?.Invoke();
        });
    }

    public void AddMediasToPlaylists(IReadOnlyList<PlaylistItem> dstPlaylistItems, IReadOnlyList<string> srcFilePaths, Action? endAction)
    {
        if (!dstPlaylistItems.Any() || !srcFilePaths.Any())
            return;

        DBManager.I.Enqueue<AppDbContext>(context =>
        {
            var dstPlaylistIds = dstPlaylistItems.Select(i => i.Id).ToList();
            var dstPlaylistFilesEntities = context.PlaylistFiles.Where(i => dstPlaylistIds.Contains(i.PlaylistId) && srcFilePaths.Contains(i.Path));

            // 1. Handle files that already presented in database

            var dstPlaylistFilesPaths = dstPlaylistFilesEntities.Select(i => i.Path).ToList();
            var alreadyPresentedFilePaths = srcFilePaths.Intersect(dstPlaylistFilesPaths).ToList();

            foreach (var filePath in alreadyPresentedFilePaths)
            {
                var item = Data.DATA_ITEMS.FirstOrDefault(i => PathUtils.Is(filePath, i.InputInfo.FullName));
                if (item is not null)
                {
                    foreach (var playlistItem in dstPlaylistItems)
                    {
                        playlistItem.Paths.AddIfNotExisted(filePath);
                        item.PlaylistItems.AddIfNotExisted(playlistItem);
                    }

                    Data.AddDataItemToList(item);
                }
            }

            // 2. Handle newly added files

            var newlyAddedFilePaths = srcFilePaths.Except(alreadyPresentedFilePaths).ToList();
            _ = AddFiles(newlyAddedFilePaths,
                items =>
                {
                    var addingPlaylistFileEntities = items
                        .Select(i => i.InputInfo.FullName)
                        .SelectMany(path => dstPlaylistIds.Select(playlistId => new PlaylistFileEntity(path, playlistId)))
                        .ToList();

                    if (addingPlaylistFileEntities.Count > 0)
                    {
                        context.PlaylistFiles.AddRange(addingPlaylistFileEntities);
                        context.SaveChanges();
                    }

                    foreach (var item in items)
                    {
                        foreach (var playlistItem in dstPlaylistItems)
                        {
                            playlistItem.Paths.AddIfNotExisted(item.InputInfo.FullName);
                            item.PlaylistItems.AddIfNotExisted(playlistItem);
                        }
                    }

                    Data.AddDataItemsToList(items);
                },
                (s, _, _) => AppEx.I.DispatcherQueue.UI(() =>
                {
                    foreach (var i in Data.LIST_ITEMS)
                        i.NotifyAll();
                }));

            var playerItems = Data.DATA_ITEMS.Where(i => srcFilePaths.Contains(i.InputInfo.FullName)).ToList();

            AppEx.I.DispatcherQueue.UI(async () =>
            {
                foreach (var i in dstPlaylistItems)
                    i.NotifyAll();

                foreach (var i in playerItems)
                    i.NotifyAll();

                /// @nguyenducphu: Temp fix for UI not updating issue
                await Task.Delay(100);
                endAction?.Invoke();
            });
        });
    }

    public void RemoveMediasFromPlaylists(IReadOnlyList<PlaylistItem> playlistItems, IReadOnlyList<PlayerItem> playerItems, Action? endAction)
    {
        if (!playlistItems.Any() || !playerItems.Any())
            return;

        var pathsHashSet = playerItems.Select(i => i.InputInfo.FullName).ToHashSet();

        DBManager.I.Enqueue<AppDbContext>(context =>
        {
            var srcPlaylistIds = playlistItems.Select(i => i.Id).ToHashSet();
            var existingPlaylistFileEntities = context.PlaylistFiles
                .Where(i => srcPlaylistIds.Contains(i.PlaylistId) && pathsHashSet.Contains(i.Path)).ToList();

            context.PlaylistFiles.RemoveRange(existingPlaylistFileEntities);

            context.SaveChanges();

            foreach (var playlistItem in playlistItems)
                playlistItem.Paths.RemoveAll(pathsHashSet.Contains);

            foreach (var item in playerItems)
            {
                var isItemInOtherLists = Data.LIST_ITEMS.Any(listItem => listItem.Paths.Contains(item.InputInfo.FullName));
                if (!isItemInOtherLists)
                {
                    item.PlaylistItems.RemoveAll(i => playlistItems.Contains(i));
                    Data.RemoveDataItemFromList(item);
                }
            }

            AppEx.I.DispatcherQueue.UI(async () =>
            {
                foreach (var i in playlistItems)
                    i.NotifyAll();

                foreach (var i in playerItems)
                    i.NotifyAll();

                /// @nguyenducphu: Temp fix for UI not updating issue
                await Task.Delay(100);
                endAction?.Invoke();
            });
        });
    }

    #endregion

    #region FOLDER

    public void PrepareFolderItems() => DBManager.I.Enqueue<AppDbContext>(context =>
    {
        var builtInPlaylistEntityIds = context.Playlists
            .Where(i => !string.IsNullOrWhiteSpace(i.Remark))
            .Select(i => i.Id)
            .ToHashSet();

        var builtInPlaylistItems = Data.LIST_ITEMS
            .Where(i => builtInPlaylistEntityIds.Contains(i.Id))
            .ToList();

        var builtInPlaylistFileEntities = context.PlaylistFiles
            .Where(i => builtInPlaylistEntityIds.Contains(i.PlaylistId))
            .ToList();

        var folderItems = context.Folders.ToList().Select(i => new FolderItem(i.Path) { IsSelected = i.IsSelected }).ToList();

        Data.AddFolderItems(folderItems);

        _ = AddFilesFromFolders(folderItems, [],
            items =>
            {
                var playlistFileEntityDict = builtInPlaylistFileEntities.Aggregate(new Dictionary<string, ListEx<int>>(), (acc, cur) =>
                {
                    if (acc.TryGetValue(cur.Path, out var playlistIds))
                        acc[cur.Path].AddIfNotExisted(cur.PlaylistId);
                    else
                        acc.TryAdd(cur.Path, [cur.PlaylistId]);

                    return acc;
                });

                foreach (var item in items)
                    if (playlistFileEntityDict.TryGetValue(item.InputInfo.FullName, out var playlistIds))
                    {
                        var playlistItem = builtInPlaylistItems.FirstOrDefault(i => playlistIds.Contains(i.Id));
                        if (playlistItem is not null)
                        {
                            item.PlaylistItems.AddIfNotExisted(playlistItem);
                            playlistItem.Paths.AddIfNotExisted(item.InputInfo.FullName);
                        }
                    }

                Data.AddDataItemsToFolder(items);
            },
            (_, _, _) =>
            {
                var selectedFolderPaths = Data.FOLDER_ITEMS
                    .Where(i => i.IsSelected)
                    .SelectMany(i => i.FilePaths)
                    .ToHashSet();

                var folderPlayerItems = Data.DATA_ITEMS
                    .Where(i => selectedFolderPaths.Contains(i.InputInfo.FullName))
                    .ToList();

                var addingItems = Data.FOLDER_DATA_ITEMS.GetAddingItems(folderPlayerItems);
                Data.AddDataItemsToFolder(addingItems);
            });
    });

    public void AddFolders(IEnumerable<string> paths) => DBManager.I.Enqueue<AppDbContext>(context =>
    {
        var duplicateFolderEntities = context.Folders.Where(i => paths.Contains(i.Path)).ToList();

        duplicateFolderEntities.ForEach(duplicateEntity => duplicateEntity.IsSelected = true);
        context.Folders.UpdateRange(duplicateFolderEntities);
        context.SaveChanges();

        var duplicateFolderPaths = duplicateFolderEntities.Select(i => i.Path).ToHashSet();

        var newFolderItems = paths
            .Where(path => !duplicateFolderPaths.Contains(path))
            .Select(path => new FolderItem(path))
            .ToList();

        newFolderItems.ForEach(folderItem => folderItem.IsSelected = true);

        Data.AddFolderItems(newFolderItems);

        context.Folders.AddRange(newFolderItems.Select(item => new FolderEntity(item.Path) { IsSelected = true }));
        context.SaveChanges();

        var builtInPlaylistEntities = context.Playlists.Where(i => !string.IsNullOrWhiteSpace(i.Remark)).ToList();
        var builtInPlaylistEntityIds = builtInPlaylistEntities.Select(i => i.Id).ToHashSet();
        var builtInPlaylistItems = Data.LIST_ITEMS.Where(i => builtInPlaylistEntityIds.Contains(i.Id)).ToList();

        var builtInPlaylistFileEntities = context.PlaylistFiles.Where(i => builtInPlaylistEntityIds.Contains(i.PlaylistId)).ToList();

        var exceptions = new ConcurrentBag<Exception>();
        var existingPathSet = new HashSet<string>(Data.DATA_ITEMS.Select(i => i.InputInfo.FullName));

        _ = AddFilesFromFolders(newFolderItems, Data.DATA_ITEMS.Select(i => i.InputInfo.FullName),
            items =>
            {
                var playlistFileEntityDict = builtInPlaylistFileEntities.Aggregate(new Dictionary<string, ListEx<int>>(), (acc, cur) =>
                {
                    if (acc.TryGetValue(cur.Path, out var playlistIds))
                        acc[cur.Path].AddIfNotExisted(cur.PlaylistId);
                    else
                        acc.TryAdd(cur.Path, [cur.PlaylistId]);

                    return acc;
                });

                foreach (var item in items)
                    if (playlistFileEntityDict.TryGetValue(item.InputInfo.FullName, out var playlistIds))
                    {
                        var playlistItem = builtInPlaylistItems.FirstOrDefault(i => playlistIds.Contains(i.Id));
                        if (playlistItem is not null)
                        {
                            item.PlaylistItems.AddIfNotExisted(playlistItem);
                            playlistItem.Paths.AddIfNotExisted(item.InputInfo.FullName);
                        }
                    }

                Data.AddDataItemsToFolder(items);
            },
            (_, _, _) =>
            {
                var selectedFolderPaths = Data.FOLDER_ITEMS
                    .Where(i => i.IsSelected)
                    .SelectMany(i => i.FilePaths)
                    .ToHashSet();

                var folderPlayerItems = Data.DATA_ITEMS
                    .Where(i => selectedFolderPaths.Contains(i.InputInfo.FullName))
                    .ToList();

                var addingItems = Data.FOLDER_DATA_ITEMS.GetAddingItems(folderPlayerItems);
                Data.AddDataItemsToFolder(addingItems);
            });
    });

    public static async Task AddFilesFromFolders(IEnumerable<FolderItem> folderItems, IEnumerable<string> existingFilePaths,
        Action<List<PlayerItem>>? packageAction = null, Action<PlayerEx.S, Exception?, bool>? endAction = null)
    {
        if (folderItems is null)
            return;

        var existingPathSet = new HashSet<string>(existingFilePaths);
        var specialSizesStack = new ConcurrentStack<int>([28, 3, 1]);

        var exceptions = new ConcurrentQueue<Exception>();

        try
        {
            var phases = folderItems.Phases(null);

            foreach (var phase in phases)
            {
                foreach (var chunk in phase)
                {
                    foreach (var itemsPerProcess in chunk.Chunk(Environment.ProcessorCount))
                    {
                        await Parallel.ForEachAsync(itemsPerProcess, (folderItem, ct) =>
                        {
                            var items = new ConcurrentQueue<PlayerItem>();

                            foreach (var filePath in Directory.EnumerateFiles(folderItem.Path))
                            {
                                if (PlayerConfig.I.IsInputAccepted(filePath, MediaType.Media) && !I.Data.FOLDER_DATA_ITEMS.Any(i => PathUtils.Is(i.InputInfo.FullName, filePath)))
                                {
                                    try
                                    {
                                        var item = new PlayerItem(filePath);
                                        item.InputInfo.Analyze();

                                        if (item.InputInfo.IsCorrupted)
                                            throw new Exception();

                                        item.BaseFolderPath = folderItem.Path;
                                        folderItem.FilePaths.AddIfNotExisted(filePath);

                                        items.Enqueue(item);

                                        int size = specialSizesStack.TryPeek(out size) ? size : 24;
                                        if (items.Count >= size)
                                        {
                                            var reportingItems = new List<PlayerItem>(items);
                                            AppEx.I.DispatcherQueue.UI(() => packageAction?.Invoke(reportingItems));
                                            items.Clear();

                                            specialSizesStack.TryPop(out _);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        exceptions.Enqueue(ex);
                                    }
                                }
                            }

                            if (!items.IsEmpty)
                                AppEx.I.DispatcherQueue.UI(() => packageAction?.Invoke([.. items]));

                            return ValueTask.CompletedTask;
                        });
                    }
                }
            }

            endAction?.Invoke(PlayerEx.S.Loaded, exceptions.IsEmpty ? null : new AggregateException(exceptions), false);
        }
        catch (Exception ex)
        {
            exceptions.Enqueue(ex);
            endAction?.Invoke(PlayerEx.S.LoadFailed, new AggregateException(exceptions), false);
        }
    }

    public static void ToggleFolder(FolderItem item) => DBManager.I.Enqueue<AppDbContext>(context =>
    {
        var folderEntity = context.Folders.FirstOrDefault(i => i.Path == item.Path);
        if (folderEntity is not null)
        {
            folderEntity.IsSelected = item.IsSelected;
            context.Folders.Update(folderEntity);
            context.SaveChanges();
        }
    });

    #endregion

    #region RECENT

    public static async Task LoadRecentItems(IEnumerable<RecentEntity> entities,
        Action<IEnumerable<PlayerItem>>? packageAction,
        Action<Exception?>? endAction)
    {
        var exceptions = new ConcurrentQueue<Exception>();

        try
        {
            var phases = entities.Phases(null);

            foreach (var phase in phases)
            {
                foreach (var package in phase)
                {
                    var items = new ConcurrentQueue<PlayerItem>();

                    foreach (var itemsPerProcess in package.Chunk(Environment.ProcessorCount))
                    {
                        await Parallel.ForEachAsync(itemsPerProcess, (entity, ct) =>
                        {
                            try
                            {
                                var samePathInstanceInContext = I.Data.DATA_ITEMS.FirstOrDefault(i => PathUtils.Is(entity.Path, i.InputInfo.FullName));

                                if (samePathInstanceInContext is not null)
                                    items.Enqueue(samePathInstanceInContext);
                                else
                                {
                                    var item = new PlayerItem(entity.Path, true, entity.LastOpenedAt);
                                    item.InputInfo.Analyze();

                                    if (item.InputInfo.IsCorrupted)
                                        throw new Exception();

                                    items.Enqueue(item);
                                }
                            }
                            catch (Exception ex)
                            {
                                exceptions.Enqueue(ex);
                            }

                            return ValueTask.CompletedTask;
                        });
                    }

                    if (!items.IsEmpty)
                    {
                        var sortedItems = items
                            .OrderByDescending(i => i.LastOpenedAt)
                            .ToList();

                        packageAction?.Invoke(sortedItems);
                    }

                    await Task.Delay(100);
                }
            }

            endAction?.Invoke(exceptions.IsEmpty ? null : new AggregateException(exceptions));
        }
        catch (Exception ex)
        {
            exceptions.Enqueue(ex);
            endAction?.Invoke(new AggregateException(exceptions));
        }
    }

    public void AddToRecent(PlayerItem? item)
    {
        if (item is null || !item.InputInfo.FixedDrive)
            return;

        DBManager.I.Enqueue<AppDbContext>(context =>
        {
            item.LastOpenedAt = TimeExt.GetCurrentUnixTimestamp();

            var recentItemEntity = context.Recent.FirstOrDefault(i => i.Path == item.InputInfo.FullName);
            if (recentItemEntity is null)
                context.Recent.Add(new(item.InputInfo.FullName));
            else
            {
                recentItemEntity.UpdateTime(CoreEntity.EntityTimeType.LastOpenedAt);
                context.Recent.Update(recentItemEntity);
            }

            context.SaveChanges();

            //

            Data.AddItemToTopRecentOrMoveExistingItemToTopRecent(item);
        });
    }

    #endregion

    #region MISC

    public static async Task<bool> AddFilesToPlayer(WindowEx? window, IEnumerable<string>? paths, bool autoPlayFirstPath = false, Action? beforeAdding = null)
    {
        if (paths is null)
            (await Picker.OpenMultipleFiles(window, picker => { foreach (var i in PlayerConfig.I.InputMediaExtensions) picker.FileTypeFilter.Add(i); }))
                .Let(_ => paths = [.. _.Select(i => i.Path)]);

        if (paths is null || !paths.Any())
            return false;

        beforeAdding?.Invoke();

        if (window is not MainWindow mainWindow)
            return false;

        if (mainWindow.NavigationView.Navigate(typeof(MediaPlayer), null) is not MediaPlayer mediaPlayer)
            return false;

        if (autoPlayFirstPath && paths.FirstOrDefault() is string firstPath)
        {
            MediaPlayer.Open<MediaPlayer>(window, firstPath, path => new PlayerItem(path));
            paths = paths.Skip(1);
        }

        mediaPlayer.Status.SetAndNotify(PlayerEx.S.Loading);

        await AddFiles(paths,
            items => PlayerEx.I.Playlist.AddRange(items),
            (s, _, _) => mediaPlayer.Status.SetAndNotify(s));

        return true;
    }

    public static async Task AddFiles(IEnumerable<string> paths, Action<List<PlayerItem>>? packageAction = null, Action<PlayerEx.S, Exception?, bool>? endAction = null)
    {
        if (!paths.Any())
        {
            endAction?.Invoke(PlayerEx.S.LoadFailed, null, false);
            return;
        }

        var exceptions = new ConcurrentQueue<Exception>();

        try
        {
            var phases = paths.Phases(null);

            foreach (var phase in phases)
            {
                foreach (var package in phase)
                {
                    var items = new ConcurrentQueue<PlayerItem>();

                    foreach (var itemsPerProcess in package.Chunk(Environment.ProcessorCount))
                    {
                        await Parallel.ForEachAsync(itemsPerProcess, (path, ct) =>
                        {
                            try
                            {
                                var samePathInstanceInContext = I.Data.DATA_ITEMS.FirstOrDefault(i => PathUtils.Is(path, i.InputInfo.FullName));
                                if (samePathInstanceInContext is not null)
                                    items.Enqueue(samePathInstanceInContext);
                                else
                                {
                                    if (!PlayerConfig.I.IsInputAccepted(path, MediaType.Media))
                                        throw new UnacceptedInputException();

                                    var item = new PlayerItem(path);
                                    item.InputInfo.Analyze();

                                    if (item.InputInfo.IsCorrupted)
                                        throw new Exception();

                                    items.Enqueue(item);
                                }
                            }
                            catch (Exception ex)
                            {
                                exceptions.Enqueue(ex);
                            }

                            return ValueTask.CompletedTask;
                        });
                    }

                    if (!items.IsEmpty)
                    {
                        var sortedItems = package.Join(items, p => p, i => i.InputInfo.FullName, (_, i) => i).ToList();
                        packageAction?.Invoke(sortedItems);
                        await Task.Delay(20);
                    }
                }
            }

            endAction?.Invoke(PlayerEx.S.Loaded, exceptions.IsEmpty ? null : new AggregateException(exceptions), false);
        }
        catch (Exception ex)
        {
            exceptions.Enqueue(ex);
            endAction?.Invoke(PlayerEx.S.LoadFailed, new AggregateException(exceptions), false);
        }
    }

    public void RemoveAll(IEnumerable<PlayerItem> items, bool doDelete)
    {
        if (!items.Any())
            return;

        DBManager.I.Enqueue<AppDbContext>(context =>
        {
            var paths = items.Select(i => i.InputInfo.FullName).ToHashSet();

            if (doDelete)
            {
                context.PlaylistFiles.RemoveRange(context.PlaylistFiles.Where(i => paths.Contains(i.Path)));
                context.Recent.RemoveRange(context.Recent.Where(i => paths.Contains(i.Path)));
                context.SaveChanges();

                Data.RemoveDataItems(items);

                foreach (var playlistItem in Data.LIST_ITEMS)
                {
                    playlistItem.Paths.RemoveAll(i => paths.Contains(i));
                    playlistItem.NotifyAll();
                }

                foreach (var folderItem in Data.FOLDER_ITEMS)
                    folderItem.FilePaths.RemoveAll(i => paths.Contains(i));

                foreach (var i in paths)
                    FileUtils.Delete(i);
            }
            else
                AppEx.LoadWindow<MainWindow>(window =>
                {
                    AppEx.I.DispatcherQueue.UI(() =>
                    {
                        var homeNavigation = window.HomeNavigation;

                        if (homeNavigation?.CurrentPage is Pages.Playlist playlistPage && playlistPage.SelectedPlaylistItem is not null)
                        {
                            context.PlaylistFiles.RemoveRange(context.PlaylistFiles.Where(i => i.PlaylistId == playlistPage.SelectedPlaylistItem.Id && paths.Contains(i.Path)));
                            context.SaveChanges();

                            foreach (var playlistItem in Data.LIST_ITEMS)
                                if (playlistItem.Id == playlistPage.SelectedPlaylistItem.Id)
                                {
                                    playlistItem.Paths.RemoveAll(i => paths.Contains(i));
                                    playlistItem.NotifyAll();
                                }

                            foreach (var item in items)
                            {
                                item.PlaylistItems.RemoveAll(i => i.Id == playlistPage.SelectedPlaylistItem.Id);

                                var itemInOtherLists = Data.LIST_ITEMS.Any(i => i.Paths.Contains(item.InputInfo.FullName));
                                if (!itemInOtherLists)
                                    Data.RemoveDataItemFromList(item);

                                playlistPage.RefinedPlayerItems.Remove(item);
                            }

                        }
                        else if (homeNavigation?.CurrentPage is Recent recentPage)
                        {
                            context.Recent.RemoveRange(context.Recent.Where(i => paths.Contains(i.Path)));
                            context.SaveChanges();

                            foreach (var item in items)
                            {
                                Data.RemoveDataItemFromRecent(item);
                                recentPage.RefinedPlayerItems.Remove(item);
                            }
                        }
                    });
                });
        });
    }

    public void RemoveFolder(FolderItem item) => DBManager.I.Enqueue<AppDbContext>(context =>
    {
        var folderEntity = context.Folders.FirstOrDefault(i => i.Path == item.Path);
        if (folderEntity is not null)
        {
            context.Folders.Remove(folderEntity);
            context.SaveChanges();
        }

        Data.RemoveFolderItem(item);
    });

    public static List<PlayerItem> FilterSearchSort(List<PlayerItem> items, MediaType filter, string searchTerm, Sort sort)
    {
        if (filter is MediaType.Video)
            items.RemoveAll(i => i.InputInfo is MediaInfo mediaInfo && !mediaInfo.HasVideo);
        else if (filter is MediaType.Audio)
            items.RemoveAll(i => i.InputInfo is MediaInfo mediaInfo && mediaInfo.HasVideo);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            items.RemoveAll(i => !i.InputInfo.Name.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase));

        items.Sort(sort switch
        {
            Sort.Newest => (a, b) => b.LastOpenedAt.CompareTo(a.LastOpenedAt),
            Sort.Oldest => (a, b) => a.LastOpenedAt.CompareTo(b.LastOpenedAt),
            Sort.A2Z => (a, b) => a.InputInfo.Name.CompareTo(b.InputInfo.Name),
            Sort.Z2A => (a, b) => b.InputInfo.Name.CompareTo(a.InputInfo.Name),
            _ => (a, b) => 1,
        });

        return items;
    }

    #endregion
}

public class MenuContext : Singleton<MenuContext>
{
    public MenuFlyout _menu = new() { Placement = FlyoutPlacementMode.Bottom };

    readonly MenuFlyoutItem _playItem;
    readonly MenuFlyoutItem _convertItem;
    readonly MenuFlyoutItem _addToPlaylistsItem;
    readonly MenuFlyoutItem _snapshotItem;

    readonly MenuFlyoutItem _addToFavorite;
    readonly MenuFlyoutItem _removeFromFavorite;

    readonly MenuFlyoutItem _removeItem;
    readonly MenuFlyoutItem _deleteItem;

    PlayerItem? _playerItem;

    public bool UsePlayFeature { get; private set; } = false;
    public bool UseAddToPlaylistsItem { get; private set; } = false;
    public bool UseSnapshotItem { get; private set; } = false;

    public bool UseAddToFavorite { get; private set; } = false;
    public bool UseRemoveFromFavorite { get; private set; } = false;

    public bool UseRemoveItem { get; private set; } = false;

    Action<PlayerItem>? _openAction;
    Action<PlayerItem>? _favoriteAction;
    Action<PlayerItem>? _removeAction;
    Action<PlayerItem>? _deleteAction;

    MenuContext()
    {
        var item = _playItem = new MenuFlyoutItem() { Text = T.Play, Icon = new FontIcon() { Glyph = "\ue768" } };
        item.Click += (_, _) =>
        {
            if (_playerItem is not null)
                _openAction?.Invoke(_playerItem);
        };
        _menu.Items.Add(item);

        item = _convertItem = new MenuFlyoutItem() { Text = T.Convert, Icon = new FontIcon() { Glyph = "\uea69" } };
        item.Click += (_, _) =>
        {
            if (_playerItem is not null)
                AppEx.LoadWindow<ConverterWindow>(window =>
                {
                    window.Activate();
                    window.Load(_playerItem.InputInfo.FullName ?? string.Empty, _playerItem.InputInfo.HasVideo ? MediaType.Video : MediaType.Audio);
                });
        };
        _menu.Items.Add(item);

        item = _addToPlaylistsItem = new MenuFlyoutItem() { Text = T.AddToPlaylist, Icon = new FontIcon() { Glyph = "\ue82d" } };
        item.Click += async (sender, _) =>
        {
            var window = AppEx.FindWindow(sender as UIElement);
            if (window is null)
                return;

            if (_playerItem is not null)
            {
                var dialogService = DialogService.From(window.Content);
                if (dialogService is not null)
                    await dialogService.Open(_ => new AddToPlaylistDialog(_, [_playerItem.InputInfo.FullName]));
            }
        };
        _menu.Items.Add(item);

        item = _addToFavorite = new MenuFlyoutItem() { Text = T.AddToFavorite, Icon = new FontIcon() { Glyph = "\uEB52" } };
        item.Click += (_, _) =>
        {
            if (_playerItem is not null)
                _favoriteAction?.Invoke(_playerItem);
        };
        _menu.Items.Add(item);

        item = _removeFromFavorite = new MenuFlyoutItem() { Text = T.RemoveFromFavorite, Icon = new FontIcon() { Glyph = "\uEB51" } };
        item.Click += (_, _) =>
        {
            if (_playerItem is not null)
                _favoriteAction?.Invoke(_playerItem);
        };
        _menu.Items.Add(item);

        item = new MenuFlyoutItem() { Text = T.RevealInFileExplorer, Icon = new FontIcon() { Glyph = "\ue8e5" } };
        item.Click += (_, _) => SystemUtils.RevealInFileExplorer(_playerItem?.InputInfo.FullName);
        _menu.Items.Add(item);

        item = _snapshotItem = new MenuFlyoutItem() { Text = T.TakeSnapshot, Icon = new FontIcon() { Glyph = "\uE722" } };
        item.Click += async (sender, _) =>
        {
            var window = AppEx.FindWindow(sender as UIElement);
            if (window is null)
                return;

            if (_playerItem is not null && PlayerEx.I.Player.CanPlay)
                (await Picker.SaveFile(window, picker =>
                {
                    picker.SuggestedFileName = _playerItem.InputInfo.NameWithoutExtension;
                    picker.FileTypeChoices.Add("PNG", [".png"]);
                }))
                .Let(_ =>
                {
                    var (width, height) = PlayerEx.I.GetCurrentVideoResolution();
                    PlayerEx.I.Player.TakeSnapshotToFile(_.Path, width, height);
                });
        };
        _menu.Items.Add(item);

        item = new MenuFlyoutItem() { Text = T.Properties, Icon = new FontIcon() { Glyph = "\ue946" } };
        item.Click += async (sender, _) =>
        {
            var window = AppEx.FindWindow(sender as UIElement);
            if (window is null)
                return;

            if (_playerItem is not null)
            {
                var dialogService = DialogService.From(window.Content);
                if (dialogService is not null)
                    await dialogService.Open(_ => new PropertiesDialog(_).Load(_playerItem.InputInfo.Info));
            }
        };
        _menu.Items.Add(item);

        item = _removeItem = new MenuFlyoutItem() { Text = T.Remove, Icon = new FontIcon() { Glyph = "\ue711" } };
        item.Click += (_, _) =>
        {
            if (_playerItem is not null)
                _removeAction?.Invoke(_playerItem);
        };
        _menu.Items.Add(item);

        item = _deleteItem = new MenuFlyoutItem() { Text = T.RemovePermanently, Icon = new FontIcon() { Glyph = "\ue711", Foreground = ColourUtils.ToSolidColorBrush("#ff0000") } };
        item.Click += (_, _) =>
        {
            if (_playerItem is not null)
                _deleteAction?.Invoke(_playerItem);
        };
        _menu.Items.Add(item);
    }

    public MenuFlyout Update(
        PlayerItem item, bool usePlayFeature, bool useAddToPlaylistsItem, bool useSnapshotItem, bool useFavoriteItem, bool useRemoveItem,
        Action<PlayerItem>? openAction, Action<PlayerItem>? favoriteAction, Action<PlayerItem>? removeAction, Action<PlayerItem>? deleteAction)
    {
        _playerItem = item;

        UsePlayFeature = usePlayFeature;
        UseAddToPlaylistsItem = useAddToPlaylistsItem;
        UseSnapshotItem = useSnapshotItem;

        UseAddToFavorite = useFavoriteItem;
        UseRemoveFromFavorite = useFavoriteItem;

        UseRemoveItem = useRemoveItem;

        _openAction = openAction;
        _favoriteAction = favoriteAction;
        _removeAction = removeAction;
        _deleteAction = deleteAction;

        _playItem.Visibility = usePlayFeature ? Visibility.Visible : Visibility.Collapsed;
        _addToPlaylistsItem.Visibility = useAddToPlaylistsItem ? Visibility.Visible : Visibility.Collapsed;
        _snapshotItem.Visibility = useSnapshotItem ? Visibility.Visible : Visibility.Collapsed;

        _addToFavorite.Visibility = useFavoriteItem && !_playerItem.IsFavorite ? Visibility.Visible : Visibility.Collapsed;
        _removeFromFavorite.Visibility = useFavoriteItem && _playerItem.IsFavorite ? Visibility.Visible : Visibility.Collapsed;

        _removeItem.Visibility = _deleteItem.Visibility = useRemoveItem ? Visibility.Visible : Visibility.Collapsed;

        _playItem.IsEnabled = _convertItem.IsEnabled = _addToPlaylistsItem.IsEnabled = _snapshotItem.IsEnabled = !_playerItem.InputInfo.IsCorrupted;

        return _menu;
    }
}

[EchoChanged]
public partial class PlayerItem : BasePlayerItem
{
    public readonly ListEx<PlaylistItem> PlaylistItems = [];
    public List<int> PlaylistIds => [.. PlaylistItems.Select(i => i.Id)];

    public bool IsFavorite => PlaylistItems.Any(i => i.IsBuiltIn && i.Remark is nameof(BuiltInPlaylist.Favorite));

    public bool IsPrivate => PlaylistItems.Any(i => i.IsBuiltIn && i.Remark is nameof(BuiltInPlaylist.Restrict));
    public bool IsBlur => IsPrivate && AppData.I.PrivateItemsRestricted;

    public bool NeedUnlock => AppData.I.PrivateItemsPasswordLocked && IsPrivate && !PasswordUnlockDialog.UnlockedInThisSession && !string.IsNullOrEmpty(AppData.I.PrivateItemsLockedPassword);

    public PlayerItem(MediaInfoBase inputInfo) : base(inputInfo)
    {
        InputInfo.Changed += (_, _) =>
        {
            Notify(nameof(IsFavorite),
                   nameof(IsPrivate),
                   nameof(IsBlur));

            Echo(nameof(IsFavorite));
        };
    }

    public PlayerItem(string inputPath) : this(MediaInfoBase.Create(inputPath))
    {
    }

    public PlayerItem(string inputPath, bool isRecent, ulong lastOpenedAt) : this(MediaInfoBase.Create(inputPath))
    {
        IsRecent = isRecent;
        LastOpenedAt = lastOpenedAt;
    }
}