using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOCore;
using IOCore.Premium;
using IOCore.UI;
using Microsoft.UI.Windowing;
using XMedia.Media.PlayerBase;

namespace IOApp.Windows;

internal partial class GrabberWindow : WindowEx
{
    public TitleBarEx TitleBar { get; }

    public GrabberWindow(Config config) : base(config)
    {
        InitializeComponent();

        TitleBar = ResolveTitleBar(_tb);
    }

    protected override void OnContentFirstLoaded()
    {
        Status.SetAndNotify(S.Ready);
    }

    protected override void OnReactivate() => base.OnReactivate();

    [RelayCommand]
    void ShowPremiumWindow() => AppEx.LoadWindow<PremiumWindow>(_ => _.Activate());

    [RelayCommand]
    void Play(string outputPath)
    {
        if (outputPath is not null)
        {
            var item = new PlayerItem(outputPath);
            item.InputInfo.Analyze();
            PlayerEx.I.Open(item, [item]);
            PlayerEx.I.NotifyAll();

            PlayerContext.I.AddToRecent(item);
        }
    }

    protected override void OnClosing(AppWindowClosingEventArgs e)
    {
        AppWindow.Hide();
        e.Cancel = true;
    }
}

internal abstract partial class PremiumWindowPremiumPage : PremiumPage<PremiumWindow> { }