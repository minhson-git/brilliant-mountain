using IOApp.Pages;
using IOCore;
using IOCore.Premium;
using IOCore.UI;
using Microsoft.UI.Windowing;

namespace IOApp.Windows;

internal partial class PremiumWindow : WindowEx
{
    public TitleBarEx TitleBar { get; }
    public NavigationViewEx NavigationView { get; }

    public PremiumWindow(Config config) : base(config)
    {
        InitializeComponent();

        TitleBar = ResolveTitleBar(_tb);
        NavigationView = ResolveNavigationView(_nv, _f, _ => _.Navigate(typeof(Home), null));
    }

    protected override void OnContentFirstLoaded()
    {
        NavigationView.Navigate(typeof(Premium), null);
        Status.SetAndNotify(S.Ready);
    }

    protected override void OnClosing(AppWindowClosingEventArgs e)
    {
        AppWindow.Hide();
        e.Cancel = true;
    }
}

internal abstract partial class PremiumWindowPremiumPage : PremiumPage<PremiumWindow> { }