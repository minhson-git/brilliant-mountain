using CommunityToolkit.Mvvm.Input;
using FlyleafLib.MediaPlayer;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.UI;
using IOCore.Utils;
using IOMedia.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Usb.Events;
using WinRT;
using static IOApp.Features.Share;
using static IOCore.Files.MediaFamily;

namespace IOApp.Pages
{
    internal partial class HomePage : MainWindowPage
    {
        public PlayerEx PlayerEx { get; } = EncapsulatedSingleton<PlayerEx>.ExposeInstance();
        public ObservableCollectionEx<PlayerItem> PlayerItems { get; } = [];
    }

    internal sealed partial class Home : MainWindowPage
    {
        public static Home? Inst { get; private set; }

        static PlayerConfig PlayerConfig { get; } = EncapsulatedSingleton<PlayerConfig>.ExposeInstance();
        public PlayerPackageStorage PlayerPackageStorage { get; } = EncapsulatedSingleton<PlayerPackageStorage>.ExposeInstance();
        public PlayerEx PlayerEx { get; } = EncapsulatedSingleton<PlayerEx>.ExposeInstance();

        public IOStatus<Home, S> Status { get; }

        public List<PrivateFileItem> SOURCE_FILE_ITEMS { get; } = [];
        public ObservableCollectionEx<PrivateFileItem> FileItems { get; } = [];

        bool _firstLoadRecent = true;
        public ObservableCollectionEx<PlayerItem> RecentPlayerItems { get; } = [];

        readonly UsbEventWatcher _usbEventWatcher = new();
        public ObservableCollectionEx<DriveItem> DriveItems { get; private set; } = [];

        public string InputTypes => FormatUtils.LayoutGridString(PlayerConfig.InputMediaExtensions);

        readonly Debounce _debounce = new();

        public MenuFlyout FilterMenuFlyout { get; } = new();
        public MediaType Filter => FilterMenuFlyout.GetCheckedRadioMenuFlyoutItemTagValue(MediaType.Media);

        string _searchTerm = "";
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetAndNotify(ref _searchTerm, value))
                    _debounce.Run(() => DispatcherQueue.TryEnqueue(() => FilterSearchSort()), 700);
            }
        }

        public MenuFlyout SortMenuFlyout { get; } = new();
        public AppTypes.Sort Sort => SortMenuFlyout.GetCheckedRadioMenuFlyoutItemTagValue(AppTypes.Sort.A2Z);

        public string SortText => R.T(AppTypes.SORTS[Sort]);

        public Home()
        {
            InitializeComponent();
            DataContext = Inst = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) => Window.SetBusy(Status.IsBusy);
        }

        protected override void OnFirstLoaded()
        {
            Window.Var<MainWindow>(_ =>
            {
                //_.HomeNavigation.Init(_frame);
                //_.HomeNavigation.Navigate(typeof(Playlist));
            });
        }

        protected override void OnNavigatedToEx(Frame sender, object? parameter, NavigationEventArgs e)
        {
            if (NavigateCount == 1)
            {
                #region Init Controls

                AppTypes.FILTERS.ForEach(i =>
                {
                    var item = new RadioMenuFlyoutItem()
                    {
                        GroupName = nameof(FilterMenuFlyout),
                        Tag = i.Key,
                        Text = R.T(i.Value),
                        IsChecked = i.Key == AppTypes.FileType.All
                    };

                    item.Click += (sender, _) =>
                    {
                        var filter = sender.As<RadioMenuFlyoutItem>().Tag.As<AppTypes.FileType>();
                        FilterTextBlock.Text = R.T(AppTypes.FILTERS[filter]);
                        FilterSearchSort();
                    };

                    FilterMenuFlyout.Items.Add(item);

                    if (item.IsChecked)
                        FilterTextBlock.Text = R.T(AppTypes.FILTERS[i.Key]);
                });

                AppTypes.SORTS.ForEach(i =>
                {
                    var item = new RadioMenuFlyoutItem()
                    {
                        GroupName = nameof(SortMenuFlyout),
                        Tag = i.Key,
                        Text = R.T(i.Value),
                        IsChecked = i.Key == AppTypes.Sort.Newest
                    };

                    item.Click += (sender, _) =>
                    {
                        Notify(nameof(SortText));
                        FilterSearchSort();
                    };

                    SortMenuFlyout.Items.Add(item);

                    if (item.IsChecked)
                        Notify(nameof(SortText));
                });

                FilterSearchSort();

                #endregion

                PlayerEx.Player.Audio.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName.Anys(nameof(Player.Audio.Mute), nameof(Player.Audio.Volume)))
                    {
                        PlayerPackageStorage.Mute = PlayerEx.Player.Audio.Mute;
                        Notify(nameof(PlayerPackageStorage));
                    }
                };
            }

            PlayerEx.Player.PropertyChanged += Player_PropertyChanged;

            _lzQueue.Resume();

            Window.SetBusy(Status.IsBusy);
        }

        protected override void OnNavigatingFromEx(NavigatingCancelEventArgs e)
        {
            _lzQueue.Stop(true);

            PlayerEx.Player.PropertyChanged -= Player_PropertyChanged;
        }

        void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Player.Status))
            {
                if (PlayerEx.Player.Status == FlyleafLib.MediaPlayer.Status.Ended)
                {
                    if (PlayerEx.PlayEndedOption == PlayerEx.PlayEndedKind.Random)
                    {
                        PlayerEx.Random();
                        PlayerEx.Play();
                    }
                    else if (PlayerEx.Next(false))
                        PlayerEx.Play();

                    _ = Ask.Inst.ToRate(Window, "MediaPlayerPlayEnded", 1, true, TimeSpan.FromHours(2), true, 10);
                }
            }
        }

        public void FilterSearchSort() =>
            Window.Var<MainWindow>(_ =>
            {
                //_.HomeNavigation.LetPage<Playlist>(_ => _.FilterSearchSort(Filter, _searchTerm, Sort));
                //_.HomeNavigation.LetPage<Folder>(_ => _.FilterSearchSort(Filter, _searchTerm, Sort));
                //_.HomeNavigation.LetPage<Recent>(_ => _.FilterSearchSort(Filter, _searchTerm, Sort));
            });

        void PlayerItemControl_OnHandle(object sender, PlayerItemEventArgs e)
        {
            if (e.Kind == PlayerItemEventKind.Preview)
                PlayerEx.Open(e.Item, [.. RecentPlayerItems]);
            else if (e.Kind == PlayerItemEventKind.Open)
                PlayerPage.Open(Window, e.Item, [.. RecentPlayerItems], typeof(MediaPlayerLite));
        }

        [RelayCommand]
        void NavigateTo(Type type) => Window.Var<MainWindow>(_ => _.HomeNavigation.Navigate(type));
    }

    internal class HomeProxy : BindingProxy<Home> { }
}