using CommunityToolkit.Mvvm.Input;
using IOApp.Pages;
using IOApp.Windows;
using IOCore;
using IOCore.Annotation;
using IOCore.Converters;
using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using XMedia.Media.PlayerBase;
using static IOApp.Configs.AppTypes;

namespace IOApp.Features;

[NotifyPropertyChanged]
public partial class MediaPlayerControl : Grid
{
    public PlayerItem? PlayerItem => PlayerEx.I.Current as PlayerItem;

    public bool IsHudVisible { get; private set => SetAndNotify(ref field, value); }

    public MediaPlayerControl()
    {
        InitializeComponent();

        PlayerEx.I.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerEx.Current))
            {
                Notify(nameof(PlayerItem));
                PlayerItem?.NotifyAll();
            }
        };

        PlayerItem?.Changed = (_, e) =>
        {
            if (e.PropertyName is nameof(PlayerItem.IsFavorite))
            {
                Notify(nameof(PlayerItem));
                PlayerItem.NotifyAll();
            }
        };
    }

    void Presenter_Loaded(object sender, RoutedEventArgs e)
    {
        PlayerEx.I.PresentAt(sender as ContentPresenter);
    }

    public Action<FrameworkElement> OnSleeping => _ =>
    {
        UI.SetCursor(_, null);
        IsHudVisible = false;
    };

    public Action<FrameworkElement> OnWaking => _ =>
    {
        UI.SetCursor(_, InputSystemCursor.Create(InputSystemCursorShape.Arrow));
        IsHudVisible = true;
    };

    [RelayCommand]
    void GoToMediaPlayer() => PlayerEx.I.Current.Let(item =>
    {
        if (!item.InputInfo.IsCorrupted)
            AppEx.FindWindow<MainWindow>().Let(_ => _.NavigationView.Navigate(typeof(MediaPlayer), null));
    });

    protected void TimeSlider_FirstLoaded(object sender, RoutedEventArgs e) =>
        sender.Var<Slider>(slider =>
        {
            slider.Loaded -= TimeSlider_FirstLoaded;

            slider.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
            {
                PlayerEx.I.Suspend();
                PlayerEx.I.Player.CurTime = TimeConverter.UnitsToTicks(slider.Value, TickUnit.Second);
                slider.ValueChanged += TimeSlider_ValueChanged;
            }), true);

            slider.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) =>
            {
                slider.ValueChanged -= TimeSlider_ValueChanged;
                PlayerEx.I.Restore();
            }), true);
        });

    void TimeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => PlayerEx.I.Player.CurTime = TimeConverter.UnitsToTicks(e.NewValue, TickUnit.Second);


    [RelayCommand]
    void Shuffle() => PlayerEx.I.PlayEndedAction = PlayerEx.I.PlayEndedAction is PlayEndedAction.Random ? PlayEndedAction.AutoPlay : PlayEndedAction.Random;

    [RelayCommand]
    void Favorite()
    {
        var playlistItem = PlayerContext.I.Data.LIST_ITEMS.FirstOrDefault(i => i.Remark is nameof(BuiltInPlaylist.Favorite));
        if (playlistItem is null || PlayerEx.I.Current is not PlayerItem playerItem)
            return;

        if (playlistItem.Paths.Contains(playerItem.InputInfo.FullName))
            PlayerContext.I.RemoveMediasFromPlaylists([playlistItem], [playerItem], () => PlayerItem?.Notify(nameof(Features.PlayerItem.IsFavorite)));
        else
            PlayerContext.I.AddMediasToPlaylists([playlistItem], [playerItem.InputInfo.FullName], () => PlayerItem?.Notify(nameof(Features.PlayerItem.IsFavorite)));
    }
}