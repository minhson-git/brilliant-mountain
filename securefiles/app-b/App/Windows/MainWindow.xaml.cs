using CommunityToolkit.Mvvm.Input;
using FlyleafLib.MediaPlayer;
using H.Hooks;
using IOApp.Features;
using IOApp.Pages;
using IOCore;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Core;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using IOData;
using IOImage.Presenter;
using IOMedia.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.System;
using XGraphics;
using static IOCore.Files.MediaFamily;

namespace IOApp
{
    internal partial class MainWindow : WindowEx
    {
        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();
        public PlayerPackageStorage PlayerPackageStorage { get; } = EncapsulatedSingleton<PlayerPackageStorage>.ExposeInstance();

        public PlayerConfig PlayerConfig { get; } = EncapsulatedSingleton<PlayerConfig>.ExposeInstance();
        public PlayerEx PlayerEx { get; } = EncapsulatedSingleton<PlayerEx>.ExposeInstance();

        public readonly XG XG;
        public readonly Presenter<ViewerItem> Presenter;

        readonly LowLevelKeyboardHook _keyboardHook = new()
        {
            IsLeftRightGranularity = false,
            HandleModifierKeys = true,
            IsExtendedMode = true,
            Handling = true
        };

        public CustomNavigation HomeNavigation => _homeNavigation;

        public MainWindow(Config config) : base(config)
        {
            InitializeComponent();

            Status.PropertyChanged += (_, _) => SystemUtils.SetThreadExecutionState(Status.IsBusy, false);

            TitleBar = _titleBar;
            NavigationView = _navigationView.Init(_frame, () => Navigate<Home>(null));

            _navigationView.Navigated += (_, e) =>
            {
                if (_navigationView.AnyTypes(typeof(Installer), typeof(ImageViewer), typeof(MediaPlayerLite)))
                {
                    _titleBar.IsLeftHeaderVisible = _titleBar.IsContentVisible = _titleBar.IsRightHeaderVisible = false;
                    _titleBar.Visibility = Visibility.Collapsed;
                    //_toolbar.Visibility = Visibility.Collapsed;

                    Grid.SetRow(_navigationView, 0);
                    Grid.SetRowSpan(_navigationView, 3);
                }
                else
                {
                    _titleBar.IsLeftHeaderVisible = _titleBar.IsContentVisible = _titleBar.IsRightHeaderVisible = true;
                    _titleBar.Visibility = Visibility.Visible;

                    _homeNavigation.Visibility = _navigationView.AnyTypes(typeof(Premium)) ? Visibility.Collapsed : Visibility.Visible;

                    Grid.SetRow(_navigationView, 2);
                    Grid.SetRowSpan(_navigationView, 1);
                }
            };

            MenuContext.Inst.Init(this);
        }

        protected override void OnContentFirstLoaded()
        {
            _ = DBManager.Inst.Init(new AppDbContext(AppDir.PGet(AppDir.Type.BaseFolder)));

            //PlayerEx.Player.PropertyChanged += (_, e) =>
            //{
            //    if (e.PropertyName == nameof(Player.Audio.Mute))
            //        PlayerPackageStorage.Mute = PlayerEx.Player.Audio.Mute;
            //    else if (e.PropertyName == nameof(Player.Status))
            //    {
            //        if (PlayerEx.Current is not null && PlayerEx.Player.IsPlaying)
            //            PlayerContext.Inst.AddToRecent(PlayerEx.Current as PlayerItem);
            //    }
            //};

            //var menuFlyoutItem = new MenuFlyoutItem
            //{
            //    Text = R.T(L.OpenVideosOrMusic),
            //    Icon = new FontIcon() { Glyph = "\ue8E5" },
            //    KeyboardAccelerators = { new() { Modifiers = VirtualKeyModifiers.Control, Key = VirtualKey.O } }
            //};

            //menuFlyoutItem.Click += (_, _) => _ = PlayerContext.AddFiles(this, null);
            //Menu.Insert(0, menuFlyoutItem);

            _keyboardHook.Up += (_, e) =>
            {
                if (e.Keys.Are(Key.Menu, Key.S))
                {
                    DispatcherQueue.TryEnqueue(() => PlayerEx.Player.Audio.Mute = !PlayerEx.Player.Audio.Mute);
                    e.IsHandled = true;
                }
            };

            _keyboardHook.Start();

            var isPromotionFlyoutVisible = AppEx.Inst.PageType != typeof(MediaPlayerLite);

            if (_licenseStatus.IsTrial && Ask.Inst.ShouldDo(null, 1, true, TimeSpan.FromDays(2)))
            {
                _ = Dialog.Open(PremiumDialog.Create(this));
                isPromotionFlyoutVisible = false;
            }

            Promotion.Inst.Offer(items =>
            {
                PromotionAppItems.Replace(items);
                Notify(nameof(PromotionAppItems));

                if (PromotionAppItems.IsNotEmpty && MathUtils.Chance(Promotion.Inst.OfferRate) && isPromotionFlyoutVisible)
                    Menu.ShowPromotionCommand.Execute(PromotionAppItemButton);
            }, 1);

            Navigate<Installer>(null);

            Status.SetAndNotify(S.Ready);
        }

        public void GoToMain()
        {
            Status.SetAndNotify(S.Init);

            DBManager.Inst.Init(new AppDbContext(AppDir.Get(AppDir.Type.LocalFolder)));
            DBManager.Inst.Execute<AppDbContext>(context =>
            {
                var queueFolderPath = Path.Combine(AppDir.Get(AppDir.Type.LocalFolder), "Queue");
                var encryptedFolderPath = Path.Combine(AppDir.Get(AppDir.Type.LocalFolder), "Encrypted");

                var queueFilePaths = Directory.Exists(queueFolderPath) ? Directory.GetFiles(queueFolderPath) : [];

                if (queueFilePaths.Length > 0)
                {
                    foreach (var queueFilePath in queueFilePaths)
                    {
                        var encryptedFilePath = Path.Combine(encryptedFolderPath, Path.GetFileName(queueFilePath));
                        File.Move(queueFilePath, encryptedFilePath);

                        var item = CypherFileItem.Create<PrivateFileItem>(encryptedFilePath);

                        context.Files.Add(new(item.EncryptedInfo.FullName)
                        {
                            FileType = item.FileType,
                            CensorType = item.Censor,
                        });
                    }

                    context.SaveChanges();
                }
            });

            Notify(nameof(AppPackageStorage));

            Navigate<Home>(null);

            Status.SetAndNotify(S.Ready);
        }

        [RelayCommand]
        void AddFiles()
        {

        }
    }

    internal abstract partial class MainWindowPage : TypedWindowPageEx<MainWindow> { }
    internal partial class MainWindowProxy : BindingProxy<MainWindow> { }

    internal abstract partial class MainWindowPremiumPage : PremiumPage<MainWindow> { }
}