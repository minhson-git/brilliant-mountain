using IOApp.Configs;
using IOCore;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.License;
using IOCore.Premium;
using IOCore.UI;
using IOImage;
using IOMedia;
using Microsoft.UI.Xaml;
using System;

namespace IOApp
{
    public partial class App : AppEx
    {
        static App()
        {
            Meta.Setup("frames-extractor");

            LicenseCore.Setup("9NTTH4QW70VS", [
                //new("9PH6BHX0R966", R.T(L.Premium_1Month), R.T(L.Premium_MonthlyBilling)),
                //new("9N2HH3BGK50R", R.T(L.Premium_6Months), R.T(L.Premium_SemiAnnuallyBilling)),
                //new("9NW46C03ZH7D", R.T(L.Premium_BuyOnceUseForever), null, true)
            ]);

            PremiumCore.Init(ThemeKind.Lay, ConceptKind.Premium,
            [
                //new(null, null, R.T(L.Premium_BetterResolutionTitle), R.T(L.Premium_BetterResolutionSubtitle), null, '\uE73E'),
                //new(null, null, R.T(L.Premium_AllVideoFormatTitle), R.T(L.Premium_AllVideoFormatSubtitle), null, '\uE73E'),
                //new(null, null, R.T(L.Premium_NoAdsTitle), R.T(L.Premium_NoAdsSubtitle1), null, '\uE73E')
            ]);

            ImageContext.Init(null, null, ImageConverterProfile._INPUT_IMAGE_FAMILIES, ImageConverterProfile._OUTPUT_IMAGE_FAMILIES);

            MediaContext.Init(null, null, MediaConverterProfile._INPUT_MEDIA_FAMILIES, MediaConverterProfile._OUTPUT_MEDIA_FAMILIES);

            IOLicense.ProLicense = IOLicense.License.Premium;
#if DEBUG
            IOLicense.DevLicense = IOLicense.License.Premium;
#endif
        }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            RegisterWindow(new(new(960, 640)), config =>
            {
                var window = new MainWindow(config);
                window.Activate();
                return window;
            }, true, true);
        }

        [STAThread]
        static int Main() => EP(() => _ = new App());
    }
}