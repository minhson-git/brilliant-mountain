using CommunityToolkit.Mvvm.Input;
using IOApp.Dialogs;
using IOApp.Features;
using IOApp.Pages;
using IOCore;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Core;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using IOData;
using IOImage.Presenter;
using IOMedia.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using XGraphics;

namespace IOApp
{
    internal partial class MainWindow : WindowEx
    {
        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();
        public SharedStorage SharedStorage { get; } = EncapsulatedSingleton<SharedStorage>.ExposeInstance();

        public readonly XG XG;
        public readonly Presenter<ViewerItem> Presenter;

        public MainWindow(Config config) : base(config)
        {
            InitializeComponent();

            Status.PropertyChanged += (_, _) => SystemUtils.SetThreadExecutionState(Status.IsBusy, false);

            TitleBar = _titleBar;
            NavigationView = _navigationView.Init(_frame, () => Navigate(typeof(Home), null));

            _navigationView.Navigated += (_, e) =>
            {
                if (_navigationView.AnyTypes(typeof(Installer), typeof(ImageViewer), typeof(MediaPlayerLite)))
                {
                    _titleBar.IsLeftHeaderVisible = _titleBar.IsContentVisible = _titleBar.IsRightHeaderVisible = false;
                    _titleBar.Visibility = Visibility.Collapsed;

                    Grid.SetRow(_navigationView, 0);
                    Grid.SetRowSpan(_navigationView, 3);

                    SystemUtils.SetThreadExecutionState(_navigationView.AnyTypes(typeof(MediaPlayerLite)), _navigationView.AnyTypes(typeof(MediaPlayerLite)));
                }
                else
                {
                    _titleBar.IsLeftHeaderVisible = _titleBar.IsContentVisible = _titleBar.IsRightHeaderVisible = true;
                    _titleBar.Visibility = Visibility.Visible;

                    Grid.SetRow(_navigationView, 2);
                    Grid.SetRowSpan(_navigationView, 1);
                }
            };

            XG = new(this, true);
            Presenter = new(XG);
        }

        protected override void OnContentFirstLoaded()
        {
            var isPromotionFlyoutVisible = true;

            if (License.Status.IsTrial && Ask.Inst.ShouldDo(null, 1, true, TimeSpan.FromDays(2)))
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

            Navigate(typeof(Installer), null);

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

                        var item = new SecureFileItem(encryptedFilePath);

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

            Navigate(typeof(Home), null);

            Status.SetAndNotify(S.Ready);
        }

        [RelayCommand]
        void ShowPrivacySettings() => _ = Dialog.Open(new PreferencesDialog(this));

        protected override void OnClosing(WindowEventArgs e)
        {
            SharedStorage.Sync(true);
        }

        [RelayCommand]
        void Play(SecureFileItem item)
        {
            if (File.Exists(item.RecoveredFileOrFolderPath))
            {
                var playerItem = MediaItem.Create<PlayerItem>(item.RecoveredFileOrFolderPath);
                playerItem.InputInfo.Analyze();
                PlayerPage.Open(this, playerItem, [playerItem], typeof(MediaPlayerLite));
            }
        }
    }

    internal abstract partial class MainWindowPage : TypedWindowPageEx<MainWindow> { }
    internal partial class MainWindowProxy : BindingProxy<MainWindow> { }
}
