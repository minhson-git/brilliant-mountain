using IOApp.Configs;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Premium;
using IOMedia.Media;
using IOMedia.Tube.TubeBase;
using Microsoft.UI.Xaml;
using System;
using static IOMedia.Tube.Profile;

namespace IOApp
{
    public partial class App : AppEx
    {
        static App()
        {
            Meta.APP_SLUG = "vidsound";
            Meta.IO_APP_ID = "68dbb98009d1291720434b54";

            LicenseCore.PRODUCT_STORE_ID = "9PPMDH0DB47M";
            LicenseCore.ADD_ON_CONFIGS =
            [
                new("9NVMMDKK75TN", R.T(L.Premium_1Month), R.T(L.Premium_MonthlyBilling)),
                new("9NCWDLZQGLBF", R.T(L.Premium_BuyOnceUseForever), null, true)
            ];

            PremiumCore.Init(ProductType.AddOn, ThemeKind.Standard, ConceptKind.Premium,
            [
                new(null, null, R.T(L.Premium_Title0), R.T(L.Premium_Headline0), null, '\uF0E9'),
                new(null, null, R.T(L.Premium_Title1), R.T(L.Premium_Headline1), null, '\uF0E9'),
                new(null, null, R.T(L.Premium_Title2), R.T(L.Premium_Headline2), null, '\uF0E9'),
            ]);

            AppDir.InitBase();

            EncapsulatedSingleton<TubeConfig>.ExposeInstance().Init(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

            MediaContext.Init(null, PlayerProfile._INPUT_MEDIA_FAMILIES, PlayerProfile._INPUT_MEDIA_FAMILIES, PlayerProfile._OUTPUT_MEDIA_FAMILIES);

            VQualityRecord.PremiumQuality = VQuality._720;
            AQualityRecord.PremiumQuality = AQuality._96;

#if DEBUG
            LicenseHelper.DevLicense = LicenseHelper.License.Premium;
#endif
        }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
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
