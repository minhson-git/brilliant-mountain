using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOApp.Gens;
using IOCore.Annotation;
using IOCore.AppManager;
using IOCore.Collections;
using IOCore.Helpers;
using IOCore.License;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;
using XMedia.Media.PlayerBase;

namespace IOApp.Pages;

[BindingProxy]
internal partial class Playlist : HomePage
{
    public ObservableCollectionEx<PlaylistItem> PlaylistItems { get; } = [];

    public PlaylistItem? SelectedPlaylistItem
    {
        get;
        set
        {
            if (SetAndNotify(ref field, value ?? PlaylistItems.FirstOrDefault(), onChanging: (oldValue, newValue) => oldValue?.IsSelected = false))
            {
                if (field is not null)
                {
                    field.IsSelected = true;
                    RefinedPlayerItems.ReplaceRange(PlayerContext.I.Data.LIST_DATA_ITEMS.Where(i => i.PlaylistIds.Any(id => field.Id == id)));
                }
                else
                    RefinedPlayerItems.Clear();
            }
        }
    }

    public Playlist()
    {
        InitializeComponent();

        PlayerContext.I.Data.ListItemsAdded += (_, items) => DispatcherQueue.UI(() =>
        {
            PlaylistItems.AddRange(items);
            SelectedPlaylistItem ??= PlaylistItems.ElementAtOrDefault(0);
        });

        PlayerContext.I.Data.ListItemsRemoved += (_, items) => DispatcherQueue.UI(() =>
        {
            var nextSelectedItem = PlaylistItems.NextSelected(SelectedPlaylistItem, items);
            PlaylistItems.RemoveRange(items);

            if (nextSelectedItem is not null)
                SelectedPlaylistItem = nextSelectedItem;
            else if (!PlaylistItems.Any(i => i.IsSelected))
                SelectedPlaylistItem = PlaylistItems.FirstOrDefault();
        });

        PlayerContext.I.Data.ListDataItemsAdded += (_, items) =>
        {
            if (SelectedPlaylistItem is null)
                return;

            DispatcherQueue.UI(() =>
            {
                var oldSortStatus = RefinedPlayerItems.IsSortPaused;
                RefinedPlayerItems.IsSortPaused = true;
                RefinedPlayerItems.AddRangeIfNotExisted(items.Where(i => i.PlaylistItems.Contains(SelectedPlaylistItem)));
                RefinedPlayerItems.IsSortPaused = oldSortStatus;
            });
        };

        PlayerContext.I.Data.ListDataItemsRemoved += (_, items) =>
        {
            if (SelectedPlaylistItem is null)
                return;

            DispatcherQueue.UI(() => RefinedPlayerItems.RemoveRange(items));
        };

        PlaylistItems.AddRange(PlayerContext.I.Data.LIST_ITEMS);
        SelectedPlaylistItem = PlaylistItems.FirstOrDefault();
    }

    protected override void OnNavigatedToEx(NavigationEventArgs e)
    {
        if (NavigatedToCount == 1)
            OnLicenseStatusChanged();

        _lzQueue.Resume();
        Status.NotifyAll();
    }

    protected override void OnNavigatingFromEx(NavigatingCancelEventArgs e)
    {
        _lzQueue.Stop(true);
    }

    protected override void OnLicenseStatusChanged()
    {
        if (IOLicense.I.Status.IsTrial)
            _ = AppSession.I.Offer(Menu.PromotionAppItems.ReplaceRange, 1);
    }

    void PlayerItemControl_OnHandle(object sender, PlayerItemEventArgs e)
    {
        if (e.Kind is PlayerItemEventKind.Preview)
            PlayerEx.I.Open(e.Item, [.. RefinedPlayerItems]);
        else if (e.Kind is PlayerItemEventKind.Open)
            MediaPlayer.Open<MediaPlayer>(Window, e.Item, [.. RefinedPlayerItems]);
    }

    void PlaylistItemButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (Status.IsLoading)
            return;

        if (sender is FrameworkElement fe && fe.Tag is PlaylistItem playlistItem)
            SelectedPlaylistItem = playlistItem;
    }

    public async void OpenInputFilesPicker()
    {
        if (SelectedPlaylistItem is not null)
            (await Picker.OpenMultipleFiles(Window, picker => { foreach (var i in PlayerConfig.I.InputMediaExtensions) picker.FileTypeFilter.Add(i); }))
            .Let(_ => PlayerContext.I.AddMediasToPlaylists([SelectedPlaylistItem], [.. _.Select(i => i.Path)], null));
    }

    [RelayCommand]
    void PlayAll() => RefinedPlayerItems.FirstOrDefault(i => i.InputInfo is not null && !i.InputInfo.IsCorrupted).Var<PlayerItem>(item =>
    {
        PlayerEx.I.Open(item, [.. RefinedPlayerItems]);
        PlayerContext.I.AddToRecent(item);
    });

    [RelayCommand]
    void AddFiles() => OpenInputFilesPicker();

    [RelayCommand]
    void RemoveSelectedFiles() => SelectedPlaylistItem.Let(item =>
        PlayerContext.I.RemoveMediasFromPlaylists([item], [.. PlayerItemsListViewBase.SelectedItems.Cast<PlayerItem>()], null)
    );

    [RelayCommand]
    void RemovePlaylist(PlaylistItem item) => Tip.Confirm(Window, T.RemoveSelected, null, () =>
    {
        PlayerContext.I.RemoveMediasFromPlaylists([item], [.. PlayerContext.I.Data.LIST_DATA_ITEMS.Where(i => item.Paths.Contains(i.InputInfo.FullName))], null);
        PlayerContext.I.RemovePlaylist(item, null);
    });

    public string PlaylistName { get; set => SetAndNotify(ref field, value); } = string.Empty;

    void CreatePlaylistTextBoxComp_Loaded(object sender, RoutedEventArgs e) => PlaylistName = string.Empty;

    [RelayCommand]
    void CreatePlaylist()
    {
        if (string.IsNullOrWhiteSpace(PlaylistName))
            PlaylistName = "Untitled";

        PlayerContext.I.CreatePlaylist(PlaylistName.Trim(), null);
    }

    [RelayCommand]
    void EditingMode(PlaylistItem item)
    {
        item.Text = item.Name;
        item.IsEditing = true;
    }

    void RenamePlaylistTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PlaylistItem playlistItem)
        {
            if (string.IsNullOrWhiteSpace(playlistItem.Name))
            {
                playlistItem.Name = playlistItem.Text ?? string.Empty;
                playlistItem.IsEditing = false;
                return;
            }

            PlayerContext.I.UpdatePlaylist(playlistItem, playlistItem => playlistItem.IsEditing = false);
            playlistItem.IsEditing = false;
        }
    }
}