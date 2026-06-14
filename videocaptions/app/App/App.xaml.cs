using IOCore;
using IOCore.License;
using IOCore.Premium;
using IOMedia;
using IOMedia.Tube;
using Microsoft.UI.Xaml;
using System;

namespace IOApp
{
    public partial class App : AppEx
    {
        static App()
        {
            Meta.Setup("getvid");

            LicenseCore.Setup("9NJ2NG43SGZ2", []);

            PremiumCore.Init(ThemeKind.Cypher, ConceptKind.Pro, []);

            TubeConfig.ExposeInstance().Init(Mode.VideoDownloader, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

            MediaContext.Init();

            IOLicense.ProLicense = IOLicense.License.Premium;

#if DEBUG
            IOLicense.DevLicense = IOLicense.License.Default;
#endif
        }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            RegisterWindow(new(Role.Main, new(1000, 640)), config =>
            {
                var window = new MainWindow(config);
                window.Activate();
                return window;
            }, true);
        }

        [STAThread]
        static int Main() => EP(() => _ = new App());
    }
}
