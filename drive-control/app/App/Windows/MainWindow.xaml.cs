using IOApp.Pages;
using IOCore;
using IOCore.AppManager;
using IOCore.Core;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using IOWMI;
using Microsoft.UI.Xaml;
using System;
using ZAnnotation.IOCore;

namespace IOApp
{
    [BindingProxy]
    internal partial class MainWindow : WindowEx
    {
        public MainWindow(Config config) : base(config)
        {
            InitializeComponent();

            AttachTitleBarAndNavigationView(TitleBar, NavigationView.Init(_frame, () => NavigationView.Navigate(typeof(Volumes), null)));

            NavigationView.Navigated += (sender, _) =>
            {
                //if (sender.CurrentPage is VolumeFeaturePage)
                //{
                //    foreach (var i in sender.MenuItems)
                //        i.Var<NavigationViewItemEx>(j => j.Visibility = Visibility.Collapsed);

                //    TitleBar.IsPaneToggleButtonVisible = false;
                //    TitleBar.IsBackButtonVisible = true;
                //}
                //else
                //{
                //    foreach (var i in sender.MenuItems)
                //        i.Var<NavigationViewItemEx>(j => j.Visibility = Visibility.Visible);

                //    TitleBar.IsPaneToggleButtonVisible = true;
                //    TitleBar.IsBackButtonVisible = false;

                //    if (sender.CurrentPage is Disks)
                //        DriveManager.Inst.Update();
                //}
            };

            TitleBar.PaneToggleRequested += (_, _) => NavigationView.IsPaneOpen = !NavigationView.IsPaneOpen;
            TitleBar.BackRequested += (_, _) => NavigationView.TryGoBack(null);
        }

        protected override void OnContentFirstLoaded()
        {
            var isPromotionFlyoutVisible = true;

            if (License.Status.IsTrial && AskSaver.Inst.ShouldDo(null, 1, true, TimeSpan.FromDays(2)))
            {
                _ = Dialog.Open(PremiumDialog.Create(this));
                isPromotionFlyoutVisible = false;
            }

            _ = Promotion.Inst.Offer(items =>
            {
                PromotionAppItems.Replace(items);

                if (PromotionAppItems.IsNotEmpty && MathUtils.Chance(Promotion.Inst.OfferRate) && isPromotionFlyoutVisible)
                    Menu.ShowPromotionCommand.Execute(PromotionAppItemButton);
            }, 1);

            DriveManager.Inst.Updated += (_, data) =>
            {
                NavigationView.ExtractPageInCache<Disks>(_ => _.Update(data));
                NavigationView.ExtractPageInCache<Volumes>(_ => _.Update(data));
                //NavigationView.ExtractPageInCache<VolumeFeaturePage>(_ => _.Update());
            };

            NavigationView.Navigate(typeof(Volumes), null);

            DriveManager.Inst.Start(true);

            Status.SetAndNotify(S.Ready);
        }

        protected override void OnClosing(WindowEventArgs e)
        {
            if (!AppEx.ForcedExit)
            {
                AppWindow.Hide();
                e.Handled = true;
            }
        }
    }

    internal abstract partial class MainWindowPage : TypedWindowPageEx<MainWindow> { }
}