using CommunityToolkit.Mvvm.Input;
using IOApp.Pages;
using IOCore;
using IOCore.Base;
using IOMedia.Media;
using Microsoft.UI.Xaml;

namespace IOApp
{
    internal partial class CoreWindow : WindowEx
    {
        public PlayerEx PlayerEx { get; } = EncapsulatedSingleton<PlayerEx>.ExposeInstance();

        public CoreWindow(Config config) : base(config)
        {
            InitializeComponent();
        }

        protected override void OnContentFirstLoaded()
        {
            Status.SetAndNotify(S.Ready);
        }

        [RelayCommand]
        void TaskbarIconAction(string parameter)
        {
            if (parameter == "ShowWindow")
                AppEx.LoadWindow<MainWindow>(_ => _.Activate());
            else if (parameter == "Premium")
                AppEx.LoadWindow<MainWindow>(_ =>
                {
                    _.Activate();
                    _.Navigate<Premium>(null);
                });
            else if (parameter == "ExitApp")
            {
                TaskbarIcon.Dispose();
                AppEx.Exit(true);
            }
        }

        protected override void OnClosing(WindowEventArgs e)
        {
            if (TaskbarIcon.IsDisposed is false)
            {
                AppWindow.Hide();
                e.Handled = true;
            }
        }
    }

    internal partial class CoreWindowProxy : BindingProxy<CoreWindow> { }
}