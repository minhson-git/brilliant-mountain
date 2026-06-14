using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOCore;
using IOCore.Collections;
using IOCore.Dialogs;
using IOCore.UI;
using Microsoft.UI.Xaml.Input;
using System.Collections.Generic;
using System.Linq;

namespace IOApp.Dialogs;

internal partial class AddToPlaylistDialog : DialogEx
{
    readonly ListEx<string> _paths = [];

    public ObservableCollectionEx<PlaylistItem> PlaylistItems { get; } = [];

    public AddToPlaylistDialog(WindowEx windowEx, List<string> paths) : base(windowEx)
    {
        InitializeComponent();

        _paths.ReplaceRange(paths);

        var playlistItems = PlayerContext.I.Data.LIST_ITEMS.ToList();
        playlistItems.ForEach(playlistItem => playlistItem.IsActivated = playlistItem.Paths.Intersect(_paths).Any());

        PlaylistItems.ReplaceRange(playlistItems);
    }

    void CollectionItemButton_Tapped(object sender, TappedRoutedEventArgs e) => sender.LetDataContext<PlaylistItem>(_ => _.IsActivated = !_.IsActivated);

    [RelayCommand]
    void Save()
    {
        var addingPlaylistItems = PlaylistItems.Where(i => i.IsActivated);
        var removingPlaylistItems = PlaylistItems.Where(i => !i.IsActivated);

        var items = PlayerContext.I.Data.DATA_ITEMS.Where(i => _paths.Contains(i.InputInfo.FullName)).ToList();

        foreach (var playlistItem in removingPlaylistItems)
            PlayerContext.I.RemoveMediasFromPlaylists([playlistItem], items, null);

        foreach (var playlistItem in addingPlaylistItems)
            PlayerContext.I.RemoveMediasFromPlaylists([playlistItem], items, null);

        CloseCommand.Execute(null);
    }
}