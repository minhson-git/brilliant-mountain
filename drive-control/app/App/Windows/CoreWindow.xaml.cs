using CommunityToolkit.Mvvm.Input;
using IOCore;
using IOCore.Core;
using IOCore.Gens;
using IOCore.Premium;
using IOWMI;
using Microsoft.UI.Xaml;
using System.Linq;
using ZAnnotation.IOCore;

namespace IOApp
{
    [BindingProxy]
    internal partial class CoreWindow : WindowEx
    {
        public string VolumeText { get; private set => SetAndNotify(ref field, value); } = R.T(L.Volumes);
        public string DiskText { get; private set => SetAndNotify(ref field, value); } = R.T(L.Disks);

        public string EjectAllText => License.Status.IsTrial ? $"{R.T(L.SafelyEjectAllUsbs)} • {PremiumCore.ConceptText}" : R.T(L.SafelyEjectAllUsbs);

        public CoreWindow(Config config) : base(config)
        {
            InitializeComponent();
        }

        protected override void OnContentFirstLoaded()
        {
            DriveManager.Inst.Updated += (_, data) => 
            {
                VolumeText = R.T(L.Volumes) + $" ({data.Volumes.Count})";
                DiskText = R.T(L.Disks) + $" ({data.Disks.Count})";
            };

            DriveManager.Inst.Start(true);

            Status.SetAndNotify(S.Ready);
        }

        protected override void OnLicenseStatusChanged()
        {
            Notify(nameof(EjectAllText));
        }

        [RelayCommand]
        void TaskbarIconAction(string parameter)
        {
            if (parameter is "ShowWindow")
                AppEx.LoadWindow<MainWindow>(_ => _.Activate());
            else if (parameter is "Premium")
                AppEx.LoadWindow<MainWindow>(_ =>
                {
                    _.Activate();
                    _.ShowPremiumDialogCommand.Execute(null);
                });
            else if (parameter is "ExitApp")
                AppEx.ForceExit();
        }

        protected override void OnClosing(WindowEventArgs e)
        {
            if (!AppEx.ForcedExit)
            {
                AppWindow.Hide();
                e.Handled = true;
            }
            else
                TaskbarIcon.Dispose();
        }
    }
}