using IOCore;
using IOCore.Core;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Premium;
using IOCore.Settings;
using System;

namespace IOApp
{
    public partial class App : AppEx
    {
        static App()
        {
            Meta.APP_SLUG = "totalclean";
            Meta.IO_APP_ID = "68d127d609d1291720253db8";

            LicenseCore.PRODUCT_STORE_ID = "9N1BQGX8H8H6";
            LicenseCore.ADD_ON_CONFIGS =
            [
                new("9PBHC3CHGTZX", R.T(L.Premium_1Month), R.T(L.Premium_MonthlyBilling)),
                new("9PMBN1HGB2P3", R.T(L.Premium_6Months), R.T(L.Premium_SemiAnnuallyBilling)),
                new("9PF0335KX8NL", R.T(L.Premium_BuyOnceUseForever), null, true)
            ];

            PremiumCore.Init(ProductType.AddOn, ThemeKind.Lay, ConceptKind.Premium,
            [
                //new(null, null, R.T(L.Premium_LimitTitle), string.Format(R.T(L.Premium_LimitDesc), Configs.Constants.LIMIT_TEXT), null, '\uE734'),
                //new(null, null, R.T(L.Premium_NoAdsTitle), R.T(L.Premium_NoAdsSubtitle1), null, "\uE734")
            ]);

#if DEBUG
            LicenseHelper.DevLicense = LicenseHelper.License.Trial;
#endif
        }

        public App()
        {
            InitializeComponent();

            SettingsCore.Options = SettingsCore.USE_THEME_SETTINGS | SettingsCore.USE_LANGUAGE_SETTINGS | SettingsCore.USE_STARTUP_SETTINGS;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            RegisterWindow(new(new(1000, 640)), config =>
            {
                var window = new MainWindow(config);
                window.Activate();
                return window;
            }, true, true);
        }

        [STAThread]
        static int Main() => ProgramEx.Main(() => _ = new App());
    }
}