using IOApp.Pages;
using IOCore;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Core;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
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
            NavigationView = _navigationView.Init(_frame, () => Navigate<JunkFiles>(null));

            _navigationView.BackRequested += (_, _) => TryGoBackCommand.Execute(null);
        }

        protected override void OnContentFirstLoaded()
        {
            var isPromotionFlyoutVisible = true;

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

            Navigate<JunkFiles>(null);

            Status.SetAndNotify(S.Ready);
        }
    }

    internal abstract partial class MainWindowPage : TypedWindowPageEx<MainWindow> { }
    internal partial class MainWindowProxy : BindingProxy<MainWindow> { }
}