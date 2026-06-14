using IOApp.Configs;
using IOCore;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.License;
using IOCore.Premium;
using IOMedia.Media;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Web;
using Windows.ApplicationModel.Activation;

namespace IOApp
{
    public partial class App : AppEx
    {
        static App()
        {
            Meta.APP_SLUG = "securefiles";
            Meta.IO_APP_ID = "68da54a309d12917203f63bf";

            LicenseCore.PRODUCT_STORE_ID = "9NQ1DW8DMWSM";
            LicenseCore.ADD_ON_CONFIGS =
            [
                new("9NNC4JJ7H6G8", R.T(L.Premium_1Month), R.T(L.Premium_MonthlyBilling)),
                new("9NFHS63FHG7K", R.T(L.Premium_6Months), R.T(L.Premium_SemiAnnuallyBilling)),
                new("9P9MXD3MX3B0", R.T(L.Premium_BuyOnceUseForever), null, true)
            ];

            PremiumCore.Init(ThemeKind.Lay, ConceptKind.Premium,
            [
                //new(null, null, R.T(L.Premium_Player0Title),     R.T(L.Premium_Player0Headline),     null, '\uE708'),
                //new(null, null, R.T(L.Premium_Player1Title),     R.T(L.Premium_Player1Headline),     null, '\uEC49'),
                //new(null, null, R.T(L.Premium_Converter0Title),  R.T(L.Premium_Converter0Headline),  null, '\uE835'),
                //new(null, null, R.T(L.Premium_Converter1Title),  R.T(L.Premium_Converter1Headline),  null, '\uE728'),
                //new(null, null, R.T(L.Premium_Downloader0Title), R.T(L.Premium_Downloader0Headline), null, '\uF6B8'),
            ]);

            MediaContext.Init(null, PlayerProfile.INPUT_MEDIA_FAMILIES);

#if DEBUG
            IOLicense.DevLicense = IOLicense.License.Trial;
#endif
        }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            RegisterWindow(new(new(1280, 720 + 32)), config =>
            {
                var window = new MainWindow(config);
                window.Activate();
                return window;
            }, true, true);
        }

        public enum VerbType
        {
            Undefined,
        }

        protected override void UpdateParamsBeforeReactivate(ExtendedActivationKind kind, object data)
        {
            if (kind == ExtendedActivationKind.Protocol && data is ProtocolActivatedEventArgs protocolArgs)
            {
                var query = HttpUtility.ParseQueryString(protocolArgs.Uri.Query);
                if (query is not null)
                {
                    Verb = query.Get("verb") ?? "";
                    Params = [HttpUtility.UrlDecode(query.Get("params")) ?? ""];
                }
            }
        }

        [STAThread]
        static int Main() => ProgramEx.Main(() => _ = new App());
    }
}