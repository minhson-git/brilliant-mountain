using IOApp.Pages;
using IOCore;
using IOCore.Base;
using IOCore.Dialogs;
using IOCore.License;
using IOCore.Premium;
using IOCore.UI;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using System;
using XMedia.Tube;

namespace IOApp
{
    internal partial class MainWindow : WindowEx
    {
        public TitleBarEx TitleBar { get; }
        public NavigationViewEx NavigationView {  get; }

        public MainWindow(Config config) : base(config)
        {
            InitializeComponent();

            Status.PropertyChanged += (_, _) => SystemUtils.SetThreadExecutionState(Status.IsBusy, false);

            TitleBar = ResolveTitleBar(_tb);
            NavigationView = ResolveNavigationView(_nv, _f, _ => _.Navigate(typeof(Home), null));

            NavigationView.Navigated += (sender, _) => Notify(nameof(NavigationView));
        }

        protected override void OnContentFirstLoaded()
        {
            TubeManager.I.Initialize(new());
            //TubeManager.I.Switched += (_, e) => _tb.Title = TubeManager.I.Signature;

            if (IOLicense.I.Status.IsTrial && AskSaver.I.ShouldDo("AskToBuy", 1, true, TimeSpan.FromHours(1)))
            {
                DialogService.From(Content)?.Open(PremiumDialog.Create);
                Menu.ShouldShowOnFirstLoaded = false;
            }

            NavigationView.Navigate(typeof(Home), null);

            Status.SetAndNotify(S.Ready);

            _ = Menu.TryShowPromotionAsync(PromotionAppItemButton);
        }
    }

    internal abstract class MainWindowPage : TypedWindowPageEx<MainWindow> { }
}
