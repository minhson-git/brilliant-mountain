using IOApp.Pages;
using IOCore;
using IOCore.AppManager;
using IOCore.Core;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using IOMedia.Tube;
using System;

namespace IOApp
{
    internal partial class MainWindow : WindowEx
    {
        public MainWindow(Config config) : base(config)
        {
            InitializeComponent();

            Status.PropertyChanged += (_, _) => SystemUtils.SetThreadExecutionState(Status.IsBusy, false);

            TitleBar = _titleBar;
            NavigationView = _navigationView.Init(_frame);

            _navigationView.Navigated += (_, _) => Notify(nameof(NavigationView));
        }

        protected override void OnContentFirstLoaded()
        {
            TubeManager.Inst.Init(null);
            TubeManager.Inst.Switched += (_, e) => _titleBar.Title = TubeManager.Inst.Signature;

            var isPromotionFlyoutVisible = true;

            if (_licenseStatus.IsTrial && Ask.Inst.ShouldDo(null, 1, true, TimeSpan.FromHours(1)))
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

            Navigate(typeof(Home), null);

            Status.SetAndNotify(S.Ready);
        }
    }

    internal abstract class MainWindowPage : TypedWindowPageEx<MainWindow> { }
}
