using CommunityToolkit.Mvvm.Input;
using IOCore;
using IOCore.Gens;
using IOCore.UI;
using IOCore.UI.Behaviors;
using Microsoft.UI.Windowing;
using static IOApp.App;
using static IOCore.Files.MediaFamily;

namespace IOApp.Windows;

internal partial class ConverterWindow : WindowEx
{
    public TitleBarEx TitleBar { get; }

    public ConverterWindow(Config config) : base(config)
    {
        InitializeComponent();

        TitleBar = ResolveTitleBar(_tb);
    }

    protected override void OnContentFirstLoaded()
    {
        if ((AppEx.I.NavArgs.Verb is nameof(VerbType.ConvertToMp3) or nameof(VerbType.ConvertToMp4)) && AppEx.I.NavArgs.Params.Count > 0)
            Load(AppEx.I.NavArgs.Params[0], AppEx.I.NavArgs.Verb is nameof(VerbType.ConvertToMp4) ? MediaType.Video : MediaType.Audio);

        Status.SetAndNotify(S.Ready);
    }

    protected override void OnReactivate()
    {
        base.OnReactivate();

        if ((AppEx.I.NavArgs.Verb is nameof(VerbType.ConvertToMp3) or nameof(VerbType.ConvertToMp4)) && AppEx.I.NavArgs.Params.Count > 0)
            Load(AppEx.I.NavArgs.Params[0], AppEx.I.NavArgs.Verb is nameof(VerbType.ConvertToMp4) ? MediaType.Video : MediaType.Audio);
    }

    public void Load(string path, MediaType type)
    {
        if (_mediaConverterControl.Item.Status.IsBusy)
            Tip.Message(this, null, T.ServiceWindow_OnlyOneInstantMessage);
        else
        {
            _mediaConverterControl.SetPath(path, type);
            _mediaConverterControl.ReloadAction();
        }
    }

    [RelayCommand]
    void ShowPremiumWindow() => AppEx.LoadWindow<PremiumWindow>(_ => _.Activate());

    protected override void OnClosing(AppWindowClosingEventArgs e)
    {
        AppWindow.Hide();
        e.Cancel = true;
    }
}