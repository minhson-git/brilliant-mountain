using IOCore;
using IOCore.Core;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.License;
using IOCore.Premium;
using IOCore.Settings;
using Microsoft.UI.Xaml;
using System;

namespace IOApp
{
    public partial class App : AppEx
    {
        static App()
        {
            Meta.Setup("usb-drive-manager");

            LicenseCore.Setup("9N1BQGX8H8H6",
            [
                new("9NP86JNKQ4MC", R.T(L._1Month), R.T(L.Premium_MonthlyBilling)),
                new("9NP4VJ9FTN0J", R.T(L._6Months), R.T(L.Premium_SemiAnnuallyBilling)),
                new("9NGW29QZJH14", R.T(L.Premium_BuyOnceUseForever), null, true)
            ]);

            PremiumCore.Init(ThemeKind.Lay, ConceptKind.Premium,
            [
                new(null, null, R.T(L.Premium_LimitTitle), string.Format(R.T(L.Premium_LimitDesc), Configs.Constants.LIMIT_TEXT), null, '\uE734'),
                //new(null, null, R.T(L.Premium_NoAdsTitle), R.T(L.Premium_NoAdsSubtitle1), null, "\uE734")
            ]);

#if DEBUG
            IOLicense.DevLicense = IOLicense.License.Default;
#endif
        }

        public App()
        {
            InitializeComponent();

            _settingsData.StartupSetting.IsForced = true;
            _settingsData.Options = SettingsData.SettingOption.Theme | SettingsData.SettingOption.Language | SettingsData.SettingOption.Startup;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            RegisterWindow(new(Role.Main, new(1000, 640)), config =>
            {
                var window = new MainWindow(config);
                window.Activate();
                return window;
            }, !_settingsData.StartupSetting.Hide);

            RegisterWindow(new(Role.Core), config =>
            {
                var window = new CoreWindow(config);
                window.Activate();
                window.AppWindow.Hide();
                return window;
            }, true);
        }

        [STAThread]
        static int Main() => EP(() => _ = new App());
    }
}