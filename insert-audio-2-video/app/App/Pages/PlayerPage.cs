using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Files;
using IOCore.UI;
using IOCore.Utils;
using IOMedia.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Data;
using System.Linq;
using Windows.System;
using static IOApp.Features.Share;
using static IOCore.Files.MediaFamily;

namespace IOApp.Pages
{
    internal partial class PlayerPage : MainWindowPage, IDisposable
    {
        public PlayerConfig PlayerConfig { get; } = EncapsulatedSingleton<PlayerConfig>.ExposeInstance();

        public PlayerEx PlayerVideo { get; } = new PlayerEx();
        public PlayerEx PlayerAudio { get; } = new PlayerEx();

        public IOStatus<PlayerPage, S> Status { get; }

        public string InputVideoTypes => FormatUtils.LayoutGridString(PlayerConfig.InputVideoExtensions);
        public string InputAudioTypes => FormatUtils.LayoutGridString(PlayerConfig.InputAudioExtensions);

        public PlayerPage()
        {
            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                Window.Cover.ShowLoading(Status.IsLoading, Cover.CanvasType.None);
                Window.SetBusy(Status.IsBusy);
            };
        }

        protected override void OnNavigatedToEx(Frame sender, object? parameter, NavigationEventArgs e)
        {
            if (NavigateCount == 1)
            {
                KeyboardUtils.AddInputs(Content,
                [
                    new(VirtualKeyModifiers.None, VirtualKey.Space, (_, _) =>
                    {
                        PlayerVideo.TogglePlayPause();
                        PlayerAudio.TogglePlayPause();
                    }),
                ]);
            }

            //PlayerVideo.Player.PropertyChanged += Player_PropertyChanged;

            _lzQueue.Resume();

            Notify(nameof(PlayerVideo));
            Notify(nameof(PlayerAudio));

            Window.SetBusy(Status.IsBusy);
        }

        protected override void OnNavigatingFromEx(NavigatingCancelEventArgs e)
        {
            _lzQueue.Stop(true);

            //PlayerVideo.Player.PropertyChanged -= Player_PropertyChanged;
        }

        //protected void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        //{
        //    if (e.PropertyName == nameof(Player.Status))
        //    {
        //        if (PlayerVideo.Player.Status == FlyleafLib.MediaPlayer.Status.Ended)
        //        {
        //            if (PlayerVideo.PlayEndedOption == PlayerEx.PlayEndedKind.Replay)
        //                PlayerVideo.Play();
        //        }

        //        if (!PlayerVideo.Player.IsPlaying)
        //            _ = Ask.Inst.ToRate(Window, null, 0, true, TimeSpan.FromDays(2));
        //    }
        //}

        protected void PresenterVideo_Loaded(object sender, RoutedEventArgs e) => PlayerVideo.PresentAt(sender as ContentPresenter);
        protected void PresenterAudio_Loaded(object sender, RoutedEventArgs e) => PlayerAudio.PresentAt(sender as ContentPresenter);

        protected void DragDropVideoGrid_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Panel>(panel =>
            {
                panel.Loaded -= DragDropVideoGrid_Loaded;
                DragDrop.Register(panel, storageItems =>
                    _ = PlayerContext.AddVideoFile(Window, storageItems.Where(i => FileUtils.IsFile(i.Path)).Select(i => i.Path).FirstOrDefault()));
            });

        protected void DragDropAudioGrid_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Panel>(panel =>
            {
                panel.Loaded -= DragDropAudioGrid_Loaded;
                DragDrop.Register(panel, storageItems =>
                    _ = PlayerContext.AddAudioFile(Window, storageItems.Where(i => FileUtils.IsFile(i.Path)).Select(i => i.Path).FirstOrDefault()));
            });

        public void AddItem(object? item, MediaType? type, bool scrollToView) =>
            item.Var<PlayerItem>(_ =>
            {
                if (type == null)
                    return;

                var player = type == MediaType.Video ? PlayerVideo : PlayerAudio;

                player.Current = _;
                player.Current.ThumbnailEnqueued(_lzQueue);

                player.Play();
                player.TogglePlayPause();

                Notify(nameof(PlayerVideo), nameof(PlayerAudio));

                //if (Window.LicenseStatus.IsTrial)
                //{
                //    var slug = PlayerConfig.GetExtra(PlayerVideo.Current.InputInfo.Extension, PlayerVideo.Current.InputInfo.HasVideo);
                //    Promotion.Inst.Offer(items =>
                //    {
                //        PromotionAppItems.Replace(items);
                //        Notify(nameof(PromotionAppItems));
                //    }, 1, null, slug == null ? null : [slug]);
                //}
            });

        bool _seekWhilePlaying;

        protected void TimeSlider_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Slider>(slider =>
            {
                slider.Loaded -= TimeSlider_Loaded;

                slider.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) =>
                {
                    if (PlayerVideo.Player.IsPlaying)
                    {
                        _seekWhilePlaying = true;
                        PlayerVideo.Player.Pause();
                        PlayerAudio.Player.Pause();
                    }
                    else
                        _seekWhilePlaying = false;

                    PlayerVideo.Player.Config.Player.SeekAccurate = true;

                    PlayerAudio.Player.Config.Player.SeekAccurate = true;
                    PlayerAudio.Player.CurTime = PlayerVideo.Player.CurTime;
                }), true);

                slider.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) =>
                {
                    if (_seekWhilePlaying)
                    {
                        _seekWhilePlaying = false;

                        PlayerVideo.Player.Config.Player.SeekAccurate = false;
                        PlayerVideo.Resume();

                        PlayerAudio.Player.Config.Player.SeekAccurate = false;
                        PlayerAudio.Resume();

                        PlayerAudio.Player.CurTime = PlayerVideo.Player.CurTime;
                    }
                }), true);

                slider.ValueChanged += (_, _) =>
                {
                    if (PlayerVideo.Player.CurTime >= PlayerVideo.Player.Duration)
                    {
                        PlayerAudio.Player.Pause();
                        PlayerAudio.Player.Config.Player.SeekAccurate = true;
                        PlayerAudio.Player.CurTime = 0;
                    }    
                };
            });

        #region Commands

        [RelayCommand]
        void AddFile(string type)
        {
            if (type == "Video")
                _ = PlayerContext.AddVideoFile(Window, null);
            else if (type == "Audio")
                _ = PlayerContext.AddAudioFile(Window, null);
        }

        [RelayCommand]
        void Mute(string type)
        {
            if (type == "Video")
                PlayerVideo.Player.Audio.Mute = !PlayerVideo.Player.Audio.Mute;
            else if (type == "Audio")
                PlayerAudio.Player.Audio.Mute = !PlayerAudio.Player.Audio.Mute;
        }

        [RelayCommand]
        void Play()
        {
            PlayerVideo.TogglePlayPause();
            PlayerAudio.TogglePlayPause();
        }

        #endregion

        public void Dispose()
        {
            PlayerVideo.Clear();
            PlayerAudio.Clear();
        }
    }
}