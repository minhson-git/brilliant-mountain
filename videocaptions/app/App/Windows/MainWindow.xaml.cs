using IOApp.Pages;
using IOCore;
using IOCore.AppManager;
using IOCore.Core;
using IOCore.Maths;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using IOMedia.Tube;
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

            AttachTitleBarAndNavigationView(TitleBar, NavigationView.Init(_frame));
        }

        protected override void OnContentFirstLoaded()
        {
            TubeManager.Inst.Initialize(new());
            TubeManager.Inst.Switched += (sender, _) => TitleBar.Title = sender.Signature;

            var isPromotionFlyoutVisible = true;

            if (License.Status.IsTrial && AskSaver.Inst.ShouldDo(null, 1, true, TimeSpan.FromHours(1)))
            {
                _ = Dialog.Open(PremiumDialog.Create(this));
                isPromotionFlyoutVisible = false;
            }

            NavigationView.Navigate(typeof(Home), null);

            Status.SetAndNotify(S.Ready);
        }
    }

    internal abstract partial class MainWindowPage : TypedWindowPageEx<MainWindow> { }
}
