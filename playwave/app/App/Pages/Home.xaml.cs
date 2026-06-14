using CommunityToolkit.Mvvm.Input;
using FlyleafLib.MediaPlayer;
using IOApp.Configs;
using IOApp.Dialogs;
using IOApp.Features;
using IOApp.Gens;
using IOApp.Windows;
using IOCore;
using IOCore.Annotation;
using IOCore.Base;
using IOCore.Collections;
using IOCore.Dialogs;
using IOCore.Helpers;
using IOCore.Modules.WMI;
using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using XMedia.Media;
using XMedia.Media.PlayerBase;
using static IOCore.Files.MediaFamily;

namespace IOApp.Pages;

internal partial class HomePage : MainWindowPage
{
    public IOStatus<HomePage, PlayerEx.S> Status { get; }

    public RefinedCollection<PlayerItem> RefinedPlayerItems { get; }

    public HomePage()
    {
        Status = new(this);
        Status.PropertyChanged += (_, _) => Window.SetBusy(Status.IsBusy);

        RefinedPlayerItems = new()
        {
            Filter = (_, i) =>
            {
                if (Window.NavigationView.FindPageInCache<Home>() is not Home home)
                    return false;

                var filterKey = home.FilterCheckableMenuFlyout.CheckedKeyOrDefault;

                if (filterKey is MediaType.Media)
                    return true;

                if (i.InputInfo is not MediaInfo mediaInfo)
                    return false;

                if (filterKey is MediaType.Video)
                    return mediaInfo.HasVideo;

                if (filterKey is MediaType.Audio)
                    return !mediaInfo.HasVideo;

                return false;
            },
            SearchBy = i => i.InputInfo.Name,
            Sort = Comparer<PlayerItem>.Create((a, b) =>
            {
                if (Window.NavigationView.FindPageInCache<Home>() is not Home home)
                    return 0;

                return home.SortCheckableMenuFlyout.CheckedKeyOrDefault switch
                {
                    AppTypes.Sort.Newest => b.InputInfo.CreationTime.CompareTo(a.InputInfo.CreationTime),
                    AppTypes.Sort.Oldest => a.InputInfo.CreationTime.CompareTo(b.InputInfo.CreationTime),
                    AppTypes.Sort.A2Z => a.InputInfo.Name.CompareTo(b.InputInfo.Name),
                    AppTypes.Sort.Z2A => b.InputInfo.Name.CompareTo(a.InputInfo.Name),
                    _ => 0
                };
            }),
        };

        RefinedPlayerItems.Refining += () => _lzQueue.Stop(true);
        RefinedPlayerItems.Refined += _lzQueue.Resume;
    }

    protected void PlayerItemsListViewBase_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue)
            if (args.Item is PlayerItem item)
                item.ThumbnailEnqueued(_lzQueue);
    }
}

[BindingProxy]
internal partial class Home : MainWindowPage
{
    public IOStatus<Home, PlayerEx.S> Status { get; }
    public NavigationViewEx HomeNavigation { get; }

    public ObservableCollectionEx<PlayerItem> RecentPlayerItems { get; } = [];

    public ObservableCollectionEx<DriveItem> DriveItems { get; private set; } = [];

    public CheckableMenuFlyout<MediaType> FilterCheckableMenuFlyout { get; }

    public string SearchTerm
    {
        get;
        set
        {
            if (SetAndNotify(ref field, value))
            {
                if (HomeNavigation.CurrentPage is HomePage homePage)
                    homePage.RefinedPlayerItems.SearchQuery = field;
            }
        }
    } = string.Empty;

    public CheckableMenuFlyout<AppTypes.Sort> SortCheckableMenuFlyout { get; }

    public Home()
    {
        InitializeComponent();

        Status = new(this);
        Status.PropertyChanged += (_, _) => Window.SetBusy(Status.IsBusy);

        HomeNavigation = _hnv;
        HomeNavigation.Init(_f, _ => _.Navigate(typeof(Playlist), null));

        //

        FilterCheckableMenuFlyout = new(MediaType.Media, _ =>
            HomeNavigation.CurrentPage.Var<HomePage>(page => page.RefinedPlayerItems.Apply()));

        foreach (var i in AppTypes.MEDIAS)
            FilterCheckableMenuFlyout.Add(i.Key, null, T.S(i.Value), i.Key is MediaType.Media);

        //

        SortCheckableMenuFlyout = new(AppTypes.Sort.Newest, _ =>
            HomeNavigation.CurrentPage.Var<HomePage>(page => page.RefinedPlayerItems.Apply(true, true)));

        foreach (var i in AppTypes.SORTS)
            SortCheckableMenuFlyout.Add(i.Key, null, T.S(i.Value), i.Key is AppTypes.Sort.Newest);

        //

        PlayerEx.I.Player.Audio.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Audio.Mute) or nameof(Audio.Volume))
                PlayerData.I.Mute = PlayerEx.I.Player.Audio.Mute;
        };

        PlayerContext.I.Data.RecentDataItemsMoved += sender => DispatcherQueue.UI(() => RecentPlayerItems.ReplaceRange(sender.Take(3)));

        PlayerContext.I.Data.RecentDataItemsAdded += (sender, items) => DispatcherQueue.UI(() =>
        {
            var recentItems = sender.Take(3);
            RecentPlayerItems.ReplaceRange(recentItems);

            foreach (var i in recentItems)
                i.ThumbnailEnqueued(_lzQueue);

            if (PlayerEx.I.Current is null)
            {
                var item = recentItems.FirstOrDefault();
                if (item is not null && !item.InputInfo.IsCorrupted)
                    PlayerEx.I.Open(item, [.. RecentPlayerItems], false);
            }
        });

        PlayerContext.I.Data.RecentDataItemsRemoved += (sender, _) => DispatcherQueue.UI(() => RecentPlayerItems.ReplaceRange(sender.Take(3)));

        DriveManager.I.VolumesArrival += (_, volumes) => DispatcherQueue.UI(() => AddOrUpdateVolumes(volumes));
        DriveManager.I.VolumesRemoval += (_, volumes) => DispatcherQueue.UI(() =>
        {
            foreach (var vol in volumes)
                DriveItems.RemoveAll(i => i.Name == vol.GetPath().TrimEnd(Path.DirectorySeparatorChar));
        });

        DriveManager.I.Updated += OnFirstUpdated;
        DriveManager.I.Start(true);

        PlayerContext.I.Init();
    }

    protected override void OnNavigatedToEx(NavigationEventArgs e)
    {
        PlayerEx.I.Player.PropertyChanged += Player_PropertyChanged;

        _lzQueue.Resume();
        Status.NotifyAll();
    }

    protected override void OnNavigatingFromEx(NavigatingCancelEventArgs e)
    {
        _lzQueue.Stop(true);

        PlayerEx.I.Player.PropertyChanged -= Player_PropertyChanged;
    }

    protected override void OnFirstLoaded()
    {
        HomeNavigation.Navigate(typeof(Playlist), null);
    }

    void AddOrUpdateVolumes(IEnumerable<Volume> volumes)
    {
        foreach (var vol in volumes)
        {
            var path = vol.GetPath().TrimEnd(Path.DirectorySeparatorChar);
            var driveItem = DriveItems.FirstOrDefault(i => i.Name == path);
            var volumeLabel = vol.ManagementObject.GetStringOrDefault("VolumeName");

            if (driveItem is not null)
                driveItem.Update(volumeLabel, vol.Kind);
            else
                DriveItems.Add(new DriveItem(path, volumeLabel, vol.Kind));
        }
    }

    void OnFirstUpdated(DeviceManager<DriveManager, DriveData> sender, DriveData data)
    {
        DriveManager.I.Updated -= OnFirstUpdated;
        AddOrUpdateVolumes(DriveManager.I.Volumes);
    }

    void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Player.Status))
        {
            if (PlayerEx.I.Player.Status is FlyleafLib.MediaPlayer.Status.Ended)
            {
                if (PlayerEx.I.PlayEndedAction is PlayEndedAction.Random)
                {
                    PlayerEx.I.Random();
                    PlayerEx.I.Play();
                }
                else if (PlayerEx.I.Next(false))
                    PlayerEx.I.Play();

                _ = AskSaver.I.ToRate(Window, "MediaPlayerPlayEnded", 1, true, TimeSpan.FromHours(2), true, 10);
            }
        }
    }

    void PlayerItemControl_OnHandle(object sender, PlayerItemEventArgs e)
    {
        if (e.Kind is PlayerItemEventKind.Preview)
            PlayerEx.I.Open(e.Item, [.. RecentPlayerItems]);
        else if (e.Kind is PlayerItemEventKind.Open)
            MediaPlayer.Open<MediaPlayer>(Window, e.Item, [.. RecentPlayerItems]);
    }

    async void LoadDriveAsync(DriveItem item)
    {
        Window.Cover.ShowLoading(true, T.Status_Loading, Cover.CanvasType.Acrylic);
        await item.ScanPaths();

        item.LoadItems(null, async (_, _) =>
        {
            Window.Cover.ShowLoading(false);

            var dialogService = DialogService.From(this);
            if (dialogService is not null)
                await dialogService.Open(_ => new DriveDialog(_, item));
        });
    }

    [RelayCommand]
    void LoadDrive(DriveItem item) => LoadDriveAsync(item);
}