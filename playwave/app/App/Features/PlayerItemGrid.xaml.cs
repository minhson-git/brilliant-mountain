using CommunityToolkit.Mvvm.Input;
using IOCore.Annotation;
using IOCore.Gens;
using IOCore.UI;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using XMedia.Media.PlayerBase;
using static IOApp.Configs.AppTypes;

namespace IOApp.Features;

public enum PlayerItemEventKind
{
    None,
    Preview,
    Open,
    PrivateChanged,
    FavoriteChanged,
    Remove,
    Delete,
}

internal class PlayerItemEventArgs(PlayerItemEventKind kind, PlayerItem item) : EventArgs
{
    public readonly PlayerItemEventKind Kind = kind;
    public readonly PlayerItem Item = item;
}

[NotifyPropertyChanged]
internal partial class PlayerItemGrid : Grid
{
    static readonly Type _type = typeof(PlayerItemGrid);

    public WindowLoader Window { get; }

    public delegate void EventHandler(object sender, PlayerItemEventArgs e);
    public event EventHandler? OnHandle;

    public bool UsePlayFeature { get => (bool)GetValue(UsePlayFeatureProperty); set => SetValue(UsePlayFeatureProperty, value); }
    public static readonly DependencyProperty UsePlayFeatureProperty = DependencyProperty.Register(nameof(UsePlayFeature), typeof(bool), _type, new PropertyMetadata(true,
        (d, e) => d.Var<PlayerItemGrid>(_ => _.Notify(nameof(UsePlayFeature)))));

    public bool UseMoreButton { get => (bool)GetValue(UseMoreButtonProperty); set => SetValue(UseMoreButtonProperty, value); }
    public static readonly DependencyProperty UseMoreButtonProperty = DependencyProperty.Register(nameof(UseMoreButton), typeof(bool), _type, new PropertyMetadata(true,
        (d, e) => d.Var<PlayerItemGrid>(_ => _.Notify(nameof(UseMoreButton)))));

    //

    public bool UseAddToPlaylistsItem { get => (bool)GetValue(UseAddToPlaylistsItemProperty); set => SetValue(UseAddToPlaylistsItemProperty, value); }
    public static readonly DependencyProperty UseAddToPlaylistsItemProperty = DependencyProperty.Register(nameof(UseAddToPlaylistsItem), typeof(bool), _type, new PropertyMetadata(true,
        (d, e) => d.Var<PlayerItemGrid>(_ => _.Notify(nameof(UseAddToPlaylistsItem)))));

    public bool UseRemoveItem { get => (bool)GetValue(UseRemoveItemProperty); set => SetValue(UseRemoveItemProperty, value); }
    public static readonly DependencyProperty UseRemoveItemProperty = DependencyProperty.Register(nameof(UseRemoveItem), typeof(bool), _type, new PropertyMetadata(true,
        (d, e) => d.Var<PlayerItemGrid>(_ => _.Notify(nameof(UseRemoveItem)))));

    //

    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(nameof(Item), typeof(PlayerItem), _type, new PropertyMetadata(null,
        (d, e) => d.Var<StandardPlayerItemControl>(_ => _.Notify(nameof(Item)))));
    public PlayerItem Item { get => (PlayerItem)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }

    public PlayerItemGrid()
    {
        Window = new(this);
    }

    [RelayCommand]
    void LTap()
    {
        if (UsePlayFeature)
            PreviewCommand.Execute(null);
    }

    [RelayCommand]
    void LDoubleTap()
    {
        if (UsePlayFeature)
            OnHandle?.Invoke(this, new(PlayerItemEventKind.Open, Item));
    }

    [RelayCommand]
    void Preview(object sender) => OnHandle?.Invoke(this, new(PlayerItemEventKind.Preview, Item));

    [RelayCommand]
    void Favorite()
    {
        var playlistItem = PlayerContext.I.Data.LIST_ITEMS.FirstOrDefault(i => i.Remark is nameof(BuiltInPlaylist.Favorite));
        if (playlistItem is null)
            return;

        if (playlistItem.Paths.Contains(Item.InputInfo.FullName))
            PlayerContext.I.RemoveMediasFromPlaylists([playlistItem], [Item], null);
        else
            PlayerContext.I.AddMediasToPlaylists([playlistItem], [Item.InputInfo.FullName], null);

        Item.Notify(nameof(PlayerItem.IsFavorite));

        OnHandle?.Invoke(this, new(PlayerItemEventKind.FavoriteChanged, Item));
    }

    [RelayCommand]
    void ShowMenu(object sender) =>
        MenuContext.I.Update(Item, UsePlayFeature, UseAddToPlaylistsItem, false, true, UseRemoveItem,
            i => OnHandle?.Invoke(sender, new(PlayerItemEventKind.Open, Item)),
            i =>
            {
                var playlistItem = PlayerContext.I.Data.LIST_ITEMS.FirstOrDefault(i => i.Remark is nameof(BuiltInPlaylist.Favorite));
                if (playlistItem is null)
                    return;

                if (playlistItem.Paths.Contains(Item.InputInfo.FullName))
                    PlayerContext.I.RemoveMediasFromPlaylists([playlistItem], [Item], null);
                else
                    PlayerContext.I.AddMediasToPlaylists([playlistItem], [Item.InputInfo.FullName], null);

                OnHandle?.Invoke(sender, new(PlayerItemEventKind.FavoriteChanged, Item));
            },
            i =>
            {
                PlayerContext.I.RemoveAll([i], false);
                OnHandle?.Invoke(sender, new(PlayerItemEventKind.Remove, i));
            },
            i =>
            {
                Tip.Confirm(Window.Value, T.RemovePermanently, T.CannotBeUndone, () =>
                {
                    PlayerEx.I.Player.Stop();
                    PlayerContext.I.RemoveAll([i], true);
                });
                OnHandle?.Invoke(sender, new(PlayerItemEventKind.Delete, i));
            })
        .ShowAt(sender as FrameworkElement);
}