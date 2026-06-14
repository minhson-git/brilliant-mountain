using CommunityToolkit.Mvvm.Input;
using FlyleafLib.MediaPlayer;
using IOApp.Features;
using IOCore;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Core;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.Services;
using IOCore.UI;
using IOCore.Utils;
using IOMedia.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using static IOApp.Features.Share;
using static IOCore.Files.MediaFamily;

namespace IOApp.Pages
{
    internal partial class PlayerPage : MainWindowPage, IDisposable
    {
        public PlayerConfig PlayerConfig { get; } = EncapsulatedSingleton<PlayerConfig>.ExposeInstance();
        public PlayerPackageStorage PlayerPackageStorage { get; } = EncapsulatedSingleton<PlayerPackageStorage>.ExposeInstance();

        public PlayerEx PlayerEx { get; } = EncapsulatedSingleton<PlayerEx>.ExposeInstance();

        public IOStatus<PlayerPage, S> Status { get; }

        public string InputTypes => FormatUtils.LayoutGridString(PlayerConfig.InputMediaExtensions);

        readonly Debounce _debounce = new();

        public CountdownTimer SleepTimer { get; }

        public MenuFlyout PlayEndedMenuFlyout { get; } = new();
        public MenuFlyout SleepMenuFlyout { get; } = new();
        public MenuFlyout SpeedMenuFlyout { get; } = new();

        bool _isHudVisible;
        public bool IsHudVisible { get => _isHudVisible; private set => SetAndNotify(ref _isHudVisible, value); }

        readonly MilestoneCounter _offerCounter = new();

        public PlayerPage()
        {
            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                Window.Cover.ShowLoading(Status.IsLoading, Cover.CanvasType.None);
                Window.SetBusy(Status.IsBusy);
            };

            SleepTimer = new(null, _ =>
            {
                if (!PlayerEx.SleepDelay.AnyEnums(PlayerEx.SleepDelayKind.Off, PlayerEx.SleepDelayKind.Ended))
                {
                    SystemUtils.Shutdown.Run(SystemUtils.Shutdown.Types.Shutdown);
                    Window.Cover.ShowCanceling(R.T(L.ShuttingDown), () => SystemUtils.Shutdown.Run(SystemUtils.Shutdown.Types.CancelShutdown));
                }
            }, null, _ => Notify(nameof(SleepTimer)), false);
        }

        protected override void OnNavigatedToEx(Frame sender, object? parameter, NavigationEventArgs e)
        {
            if (NavigateCount == 1)
            {
                KeyboardUtils.AddInputs(Content,
                [
                    new(VirtualKeyModifiers.None, VirtualKey.L, (_, _) => PlayerPackageStorage.IsListVisible = !PlayerPackageStorage.IsListVisible),
                    new(VirtualKeyModifiers.None, VirtualKey.Escape, (_, _) =>
                    {
                        if (Window.IsTiny)
                            Window.IsTiny = false;
                        else if (Window.IsFullScreen)
                            Window.IsFullScreen = false;
                        else
                            Window.TryGoBackCommand.Execute(null);
                    }),
                    new(VirtualKeyModifiers.None, VirtualKey.F11, (_, _) =>
                    {
                        if (Window.IsTiny)
                            Window.IsTiny = false;
                        else
                            Window.IsFullScreen = !Window.IsFullScreen;
                    }),
                    new(VirtualKeyModifiers.None, VirtualKey.Back, (_, _) => Window.TryGoBackCommand.Execute(null)),
                    new(VirtualKeyModifiers.None, VirtualKey.Space, (_, _) => PlayerEx.TogglePlayPause()),
                    new(VirtualKeyModifiers.None, VirtualKey.Left, (_, _) => PlayerEx.Player.SeekBackward()),
                    new(VirtualKeyModifiers.None, VirtualKey.Right, (_, _) => PlayerEx.Player.SeekForward()),
                    new(VirtualKeyModifiers.None, VirtualKey.Up, (_, _) => SelectItem(PlayerEx.Playlist.Previous(PlayerEx.Current), true)),
                    new(VirtualKeyModifiers.None, VirtualKey.Down, (_, _) => SelectItem(PlayerEx.Playlist.Next(PlayerEx.Current), true))
                ]);
            }

            _offerCounter.Reset();

            Window.PropertyChanged += Window_PropertyChanged;
            PlayerEx.Player.PropertyChanged += Player_PropertyChanged;

            PlayEndedMenuFlyout.Placement = SleepMenuFlyout.Placement = SpeedMenuFlyout.Placement = FlyoutPlacementMode.Top;

            _lzQueue.Resume();

            Notify(nameof(PlayerEx));

            Window.SetBusy(Status.IsBusy);
        }

        protected override void OnNavigatingFromEx(NavigatingCancelEventArgs e)
        {
            PlayerEx.Player.Stop();
            _lzQueue.Stop(true);

            Window.IsFullScreen = Window.IsTiny = false;

            Window.PropertyChanged -= Window_PropertyChanged;
            PlayerEx.Player.PropertyChanged -= Player_PropertyChanged;
        }

        protected override void OnLicenseStatusChanged()
        {
            BuildMenuFlyouts();
        }

        void Window_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WindowEx.IsFullScreen))
                PlayerEx.Update();
            else if (e.PropertyName == nameof(WindowEx.IsTiny))
            {
                Window.IsMinimizable = Window.IsMaximizable = !Window.IsTiny;
                Window.IsAlwaysOnTop = Window.IsTiny;

                if (Window.IsTiny)
                    Window.Config.SetSizeOption(Config.Option.Tiny, Window.AppWindow);
                else
                    Window.Config.SetSizeOption(Config.Option.Normal, Window.AppWindow);

                ScreenUtils.CenterOnScreen(Window.Hwnd);

                PlayerEx.Update();

                _ = Ask.Inst.ToRate(Window, "IsTiny", 1, true, TimeSpan.FromDays(2), true, 10);
            }
        }

        protected void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Player.Audio.Mute))
                PlayerPackageStorage.Mute = PlayerEx.Player.Audio.Mute;
            if (e.PropertyName == nameof(Player.Status))
            {
                if (PlayerEx.Player.Status == FlyleafLib.MediaPlayer.Status.Ended)
                {
                    if (PlayerEx.SleepDelay == PlayerEx.SleepDelayKind.Ended)
                    {
                        SystemUtils.Shutdown.Run(SystemUtils.Shutdown.Types.Shutdown);
                        Window.Cover.ShowCanceling(R.T(L.ShuttingDown), () => SystemUtils.Shutdown.Run(SystemUtils.Shutdown.Types.CancelShutdown));
                        return;
                    }

                    if (PlayerEx.PlayEndedOption == PlayerEx.PlayEndedKind.AutoPlay)
                        PlayerEx.Playlist.Next(PlayerEx.Current, true).Let(_ => SelectItem(_, true));
                    else if (PlayerEx.PlayEndedOption == PlayerEx.PlayEndedKind.Replay)
                        PlayerEx.Play();
                    else if (PlayerEx.PlayEndedOption == PlayerEx.PlayEndedKind.Random)
                        PlayerEx.Playlist.RandomOne([PlayerEx.Current]).Let(_ => SelectItem(_, true));
                }

                if (!PlayerEx.Player.IsPlaying)
                    _ = Ask.Inst.ToRate(Window, null, 0, true, TimeSpan.FromDays(2));
            }
        }

        protected void BuildMenuFlyouts()
        {
            PlayEndedMenuFlyout.Items.Clear();
            PlayerEx.PLAY_ENDEDS.ForEach(i =>
            {
                if (i.Value.I1)
                {
                    var item = new RadioMenuFlyoutItem()
                    {
                        GroupName = nameof(PlayEndedMenuFlyout),
                        Tag = i.Key,
                        Icon = new FontIcon() { Glyph = i.Value.I3 },
                        Text = R.T(i.Value.I4),
                        IsChecked = i.Key == PlayerEx.PlayEndedOption
                    };

                    item.Click += (_, _) =>
                        PlayerEx.PlayEndedOption = UIHelper.GetCheckedRadioMenuFlyoutItemTagValue(PlayEndedMenuFlyout, PlayerEx.PlayEndedKind.AutoPlay);

                    PlayEndedMenuFlyout.Items.Add(item);
                }
            });

            SpeedMenuFlyout.Items.Clear();
            PlayerEx.SPEEDS.ForEach(i =>
            {
                if (!i.Value.I1) return;

                var isEnabled = Window.LicenseStatus.IsPremium || i.Key.AnyEnums(PlayerEx.SpeedKind._0_5, PlayerEx.SpeedKind._0_75, PlayerEx.SpeedKind._1, PlayerEx.SpeedKind._1_25, PlayerEx.SpeedKind._1_5);

                var item = new RadioMenuFlyoutItem()
                {
                    GroupName = nameof(SpeedMenuFlyout),
                    Tag = i.Key,
                    Text = $"{(isEnabled ? "" : "🌟 ")}{i.Value.I3}",
                    IsChecked = i.Key == PlayerEx.SpeedOption,
                    IsEnabled = isEnabled
                };

                item.Click += (sender, _) => PlayerEx.SpeedOption = sender.GetTagOrDefault<PlayerEx.SpeedKind>();

                SpeedMenuFlyout.Items.Add(item);
            });
        }

        protected void Presenter_Loaded(object sender, RoutedEventArgs e) => PlayerEx.PresentAt(sender as ContentPresenter);

        public static void Open(WindowEx window, PlayerItem item, IEnumerable<PlayerItem> items, Type type)
        {
            if (item is null || item.InputInfo.IsCorrupted)
                return;

            items = items.Where(i => !i.InputInfo.IsCorrupted);

            if (items.Any(i => i == item))
                window.Navigate<PlayerPage>(null).Let(page =>
                {
                    page.PlayerEx.Playlist.Replace(items);
                    page.SelectItem(item, true);
                });
        }

        public void RemoveAll(IEnumerable<PlayerItem> removingItems, bool doDelete)
        {
            var nextSelectedItem = PlayerEx.Playlist.NextSelected(PlayerEx.Current, removingItems);

            var items = PlayerEx.Playlist.Where(i => removingItems.Any(item => item.InputInfo.FullName == i.InputInfo.FullName)).ToList();
            foreach (var item in items)
                PlayerEx.Playlist.RemoveEx(item);

            _lzQueue.Clear();

            SelectItem(nextSelectedItem, true);
        }

        public void SelectItem(object? item, bool scrollToView) =>
            item.Var<PlayerItem>(_ =>
            {
                PlayerEx.Current = _;
                PlayerEx.Current.ThumbnailEnqueued(_lzQueue);

                PlayerEx.Play();
                Notify(nameof(PlayerEx));

                if (Window.LicenseStatus.IsTrial)
                {
                    var slug = PlayerConfig.GetExtra(PlayerEx.Current.InputInfo.Extension, PlayerEx.Current.InputInfo.HasVideo);
                    Promotion.Inst.Offer(items =>
                    {
                        PromotionAppItems.Replace(items);
                        Notify(nameof(PromotionAppItems));
                    }, 1, null, slug is null ? null : [slug]);
                }
            });

        bool _seekWhilePlaying;

        protected void TimeSlider_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Slider>(slider =>
            {
                slider.Loaded -= TimeSlider_Loaded;

                slider.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) =>
                {
                    _debounce.Pause();

                    if (PlayerEx.Player.IsPlaying)
                    {
                        _seekWhilePlaying = true;
                        PlayerEx.Player.Pause();
                    }
                    else
                        _seekWhilePlaying = false;

                    PlayerEx.Player.Config.Player.SeekAccurate = true;

                }), true);

                slider.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) =>
                {
                    _debounce.Resume();

                    if (_seekWhilePlaying)
                    {
                        _seekWhilePlaying = false;

                        PlayerEx.Player.Config.Player.SeekAccurate = false;
                        PlayerEx.Resume();
                    }
                }), true);
            });

        protected void Container_PointerMoved(object sender, PointerRoutedEventArgs e) =>
            sender.Var<GridEx>(panel =>
            {
                if (!_isHudVisible)
                {
                    IsHudVisible = true;
                    panel.TryVisibleCursor(true);
                }

                _debounce.Run(() => DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isHudVisible)
                    {
                        IsHudVisible = false;
                        panel.TryVisibleCursor(false);
                    }
                }), 3000);
            });

        protected void Container_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
        }

        readonly TapGesture _tapGesture = new();

        protected async void Presenter_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (await _tapGesture.IsDoubleTap())
                return;

            PlayerEx.TogglePlayPause();
        }

        protected void Presenter_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            _tapGesture.SetDoubleTap();

            if (Window.IsTiny)
                Window.IsTiny = false;
            else
                Window.IsFullScreen = !Window.IsFullScreen;
        }

        protected void ListViewEx_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (PlayerEx.Current != e.ClickedItem)
                SelectItem(e.ClickedItem, false);
            else
                PlayerEx.TogglePlayPause();
        }

        protected void ListViewEx_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue)
                if (args.Item is MediaItem item)
                    item.ThumbnailEnqueued(_lzQueue);
        }

        #region Commands

        [RelayCommand]
        void Mute() => PlayerEx.Player.Audio.Mute = !PlayerEx.Player.Audio.Mute;

        [RelayCommand]
        void Play() => PlayerEx.TogglePlayPause();

        [RelayCommand]
        void Backward() => PlayerEx.Player.SeekBackward();

        [RelayCommand]
        void Forward() => PlayerEx.Player.SeekForward();

        [RelayCommand]
        void Previous() => SelectItem(PlayerEx.Playlist.Previous(PlayerEx.Current), true);

        [RelayCommand]
        void Next() => SelectItem(PlayerEx.Playlist.Next(PlayerEx.Current), true);

        [RelayCommand]
        void RotateRight()
        {
            PlayerEx.Player.RotateRight();
            PlayerEx.Update();
        }

        [RelayCommand]
        void DisplayList() => PlayerPackageStorage.IsListVisible = !PlayerPackageStorage.IsListVisible;

        #endregion

        async Task AddFiles(IEnumerable<string> paths, Action<S> startAction, Action<List<PlayerItem>, int, int> packageAction, Action<S, Exception?, bool> endAction)
        {
            var exceptions = new ConcurrentBag<Exception>();

            startAction?.Invoke(S.Loading);

            try
            {
                var phases = paths.Phases(null);

                foreach (var (phase, phaseIndex) in phases.Select((value, i) => (value, i)))
                {
                    foreach (var (package, packageIndex) in phase.Select((value, i) => (value, i)))
                    {
                        var items = new ConcurrentBag<PlayerItem>();

                        foreach (var itemsPerProcess in package.Chunk(Environment.ProcessorCount))
                        {
                            await Parallel.ForEachAsync(itemsPerProcess, (path, ct) =>
                            {
                                try
                                {
                                    if (!PlayerEx.Playlist.Any(i => i.InputInfo.FullName == path))
                                    {
                                        if (!PlayerConfig.IsInputAccepted(path, MediaType.Media))
                                            throw new UnacceptedInputException();

                                        var item = MediaItem.Create<PlayerItem>(path);
                                        item.InputInfo.Analyze();

                                        if (!item.InputInfo.IsCorrupted)
                                            items.Add(item);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    exceptions.Add(ex);
                                }

                                return ValueTask.CompletedTask;
                            });
                        }

                        if (!items.IsEmpty)
                            packageAction?.Invoke([.. items.OrderBy(i => Array.IndexOf(package, i.InputInfo.FullName))], phaseIndex, packageIndex);

                        await Task.Delay(100);
                    }
                }

                endAction?.Invoke(S.Loaded, exceptions.IsEmpty ? null : new AggregateException(exceptions), false);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                endAction?.Invoke(S.LoadFailed, new AggregateException(exceptions), false);
            }

            _openFilesLzQueue.Clear();
            await Task.Delay(100);
        }

        public Task OpenNewFiles(IEnumerable<string> filePaths)
        {
            var firstPath = filePaths.FirstOrDefault();

            var fileItemPaths = PlayerEx.Playlist.Select(i => i.InputInfo.FullName);
            filePaths = [.. filePaths.Where(fp => fp == firstPath || !fileItemPaths.Any(fip => fip == fp))];

            var firstItem = PlayerEx.Playlist.FirstOrDefault(i => i.InputInfo.FullName == firstPath);
            firstItem.Let(i => DispatcherQueue.TryEnqueue(() => SelectItem(i, true)));

            return AddFiles(filePaths,
                s => DispatcherQueue.TryEnqueue(() => Status.SetAndNotify(s)),
                (items, _, _) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PlayerEx.Playlist.Add(items);

                        if (firstItem is null)
                        {
                            firstItem = PlayerEx.Playlist.FirstOrDefault(i => i.InputInfo.FullName == firstPath);
                            firstItem.Let(i => DispatcherQueue.TryEnqueue(() => SelectItem(i, true)));
                        }
                    });
                },
                (s, _, _) => DispatcherQueue.TryEnqueue(() => Status.SetAndNotify(s)));
        }

        readonly LzQueue _openFilesLzQueue = LzQueueService.Inst.Spawn();

        public void OpenNewFilesEnqueued(List<string> filePaths) =>
            _openFilesLzQueue.Enqueue(
                new LzTask<List<string>>(filePaths, OpenNewFiles
            ));

        public void Dispose()
        {
            PlayerEx.Clear();
            _debounce.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    internal class PlayerPageProxy : BindingProxy<PlayerPage> { }

    public partial class PlayerPackageStorage : PackageStorage<PlayerPackageStorage>, INotifyPropertyChanged
    {
        public PlayerPackageStorage() { }

        public bool Mute
        {
            get => GetValueOrDefault(Name(), false);
            set => Set(Name(), value);
        }

        public bool IsListVisible
        {
            get => GetValueOrDefault(Name(), false);
            set => Set(Name(), value);
        }

        public bool IsInfoVisible
        {
            get => GetValueOrDefault(Name(), false);
            set => Set(Name(), value);
        }
    }
}