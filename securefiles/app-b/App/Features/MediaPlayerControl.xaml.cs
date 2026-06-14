using CommunityToolkit.Mvvm.Input;
using FlyleafLib.MediaPlayer;
using IOApp.Pages;
using IOCore.Base;
using IOCore.Helpers;
using IOCore.UI;
using IOCore.Utils;
using IOMedia.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace IOApp.Features
{
    public partial class MediaPlayerControl : UserControlEx
    {
        public PlayerEx PlayerEx { get; } = EncapsulatedSingleton<PlayerEx>.ExposeInstance();
        public PlayerItem? PlayerItem => PlayerEx.Current as PlayerItem;

        readonly Debounce _debounce = new();

        public BitmapImage? Icon { get; } = SvgHelper.ToBitmapImage(PathUtils.ExternalPath("disk.svg"), 128, 128);

        bool _isHudVisible;
        public bool IsHudVisible { get => _isHudVisible; private set => SetAndNotify(ref _isHudVisible, value); }

        public MediaPlayerControl()
        {
            InitializeComponent();
            DataContext = this;

            PlayerEx.Player.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Player.Status))
                {
                    if (PlayerEx.Player.IsPlaying)
                        DiskStoryBoard.Resume();
                    else
                        DiskStoryBoard.Pause();
                }
            };
        }

        void DiskImage_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Image>(image =>
            {
                image.Loaded -= DiskImage_Loaded;
                image.Source = Icon;

                DiskStoryBoard.Begin();
                DiskStoryBoard.Pause();
            });

        void Presenter_Loaded(object sender, RoutedEventArgs e) => PlayerEx.PresentAt(sender as ContentPresenter);

        void Container_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isHudVisible)
                IsHudVisible = true;

            _debounce.Run(() => DispatcherQueue.TryEnqueue(() =>
            {
                if (_isHudVisible)
                    IsHudVisible = false;
            }), 3000);
        }

        readonly TapGesture _tapGesture = new();

        async void Container_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (await _tapGesture.IsDoubleTap())
                return;

            PlayerEx.TogglePlayPause();
        }

        void Container_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            _tapGesture.SetDoubleTap();
            SwitchMediaPlayerCommand.Execute(null);
        }

        #region Slider Seeking

        bool _seekingWhilePlaying;

        void TimeSlider_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Slider>(slider =>
            {
                slider.Loaded -= TimeSlider_Loaded;

                slider.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) =>
                {
                    if (PlayerEx.Player.IsPlaying)
                    {
                        PlayerEx.Player.Pause();
                        _seekingWhilePlaying = true;
                    }

                    PlayerEx.Player.Config.Player.SeekAccurate = true;
                }), true);

                slider.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) =>
                {
                    if (_seekingWhilePlaying)
                    {
                        PlayerEx.Resume();
                        _seekingWhilePlaying = false;
                    }
                }), true);
            });

        #endregion

        #region Commands

        [RelayCommand]
        void SwitchMediaPlayer() => PlayerEx.Current.Let(item =>
        {
            //if (!item.InputInfo.IsCorrupted)
            //    Window.Navigate<MediaPlayer>(null);
        });

        [RelayCommand]
        void Play() => PlayerEx.TogglePlayPause();

        [RelayCommand]
        void Previous()
        {
            if (PlayerEx.Previous(false))
                PlayerEx.Play();
        }

        [RelayCommand]
        void Next()
        {
            if (PlayerEx.Next(false))
                PlayerEx.Play();
        }

        [RelayCommand]
        void Mute() => PlayerEx.Player.Audio.Mute = !PlayerEx.Player.Audio.Mute;

        [RelayCommand]
        void Shuffle() => PlayerEx.PlayEndedOption = PlayerEx.PlayEndedOption == PlayerEx.PlayEndedKind.Random
            ? PlayerEx.PlayEndedKind.AutoPlay
            : PlayerEx.PlayEndedKind.Random;

        [RelayCommand]
        void Favorite() => PlayerItem.Let(item =>
        {
            //PlayerContext.Inst.AddOrRemovePlaylist(Configs.AppTypes.BuiltInPlaylist.Favorite, item, null);
            Notify(nameof(PlayerItem));
        });

        #endregion
    }
}