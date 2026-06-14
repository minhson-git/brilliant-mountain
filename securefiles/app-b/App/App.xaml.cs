using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Premium;
using IOMedia.Media;
using System;

namespace IOApp
{
    public partial class App : AppEx
    {
        static App()
        {
            Meta.APP_SLUG = "play-list";
            Meta.IO_APP_ID = "67a2f0bbd6ede156f83e2d36";

            LicenseCore.PRODUCT_STORE_ID = "9MWZKN2CXNCH";
            LicenseCore.ADD_ON_CONFIGS =
            [
                new("9PL71HDN6T24", R.T(L.Premium_1Month), R.T(L.Premium_MonthlyBilling)),
                new("9NFR1DRR918C", R.T(L.Premium_6Months), R.T(L.Premium_SemiAnnuallyBilling)),
                new("9NZDDS6075BX", R.T(L.Premium_BuyOnceUseForever), null, true)
            ];

            PremiumCore.Init(ProductType.AddOn, ThemeKind.Lay, ConceptKind.Premium,
            [
                //new(null, null, R.T(L.Premium_Player0Title),     R.T(L.Premium_Player0Headline),     null, '\uE708'),
                //new(null, null, R.T(L.Premium_Player1Title),     R.T(L.Premium_Player1Headline),     null, '\uEC49'),
                //new(null, null, R.T(L.Premium_Converter0Title),  R.T(L.Premium_Converter0Headline),  null, '\uE835'),
                //new(null, null, R.T(L.Premium_Converter1Title),  R.T(L.Premium_Converter1Headline),  null, '\uE728'),
                //new(null, null, R.T(L.Premium_Downloader0Title), R.T(L.Premium_Downloader0Headline), null, '\uF6B8'),
            ]);

            MediaContext.Init(null,
                PlayerProfile.INPUT_MEDIA_FAMILIES,
                PlayerProfile.INPUT_MEDIA_FAMILIES, PlayerProfile.OUTPUT_MEDIA_FAMILIES);

#if DEBUG
            LicenseHelper.DevLicense = LicenseHelper.License.Trial;
#endif
        }

        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            RegisterWindow(new(new(0, 0)), config =>
            {
                var window = new CoreWindow(config);
                window.Activate();
                window.AppWindow.Hide();
                return window;
            }, false, true);

            RegisterWindow(new(new(1280, 720 + 32)), config =>
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