using IOApp.Configs;
using IOCore;
using IOCore.Base;
using IOCore.Gens;
using IOCore.License;
using IOCore.Premium;
using IOCore.Settings;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using XMedia;
using XMedia.Tube;

namespace IOApp;

public partial class App : AppEx
{
    static App()
    {
        Meta.Setup("vidsound");

        StoreContextProxy.Setup("9PPMDH0DB47M",
        [
            new("9NVMMDKK75TN", R.T(L._1Month), R.T(L.Premium_MonthlyBilling)),
            new("9NCWDLZQGLBF", R.T(L.Premium_BuyOnceUseForever), null, true)
        ]);

        PremiumCore.I.Init(ThemeKind.Standard, ConceptKind.Premium,
        [
            new(null, null, R.T(L.Premium_Title0), R.T(L.Premium_Headline0), null, '\uF0E9'),
            new(null, null, R.T(L.Premium_Title1), R.T(L.Premium_Headline1), null, '\uF0E9'),
            new(null, null, R.T(L.Premium_Title2), R.T(L.Premium_Headline2), null, '\uF0E9'),
        ]);

        MediaContext.Init(null, new(PlayerProfile._INPUT_MEDIA_FAMILIES, PlayerProfile._INPUT_MEDIA_FAMILIES), new(PlayerProfile._OUTPUT_MEDIA_FAMILIES));

        VQualityRecord.PremiumQuality = VQuality._720;
        AQualityRecord.PremiumQuality = AQuality._96;

#if DEBUG
        IOLicense.DevLicense = IOLicense.License.Premium;
#endif
    }

    public App()
    {
        InitializeComponent();

        SettingsData.I.StartupSetting.IsForced = true;
        SettingsData.I.Options = SettingsData.SettingOption.Theme | SettingsData.SettingOption.Language | SettingsData.SettingOption.Startup;
    }

    protected override Task OnReady(LaunchActivatedEventArgs args)
    {
        RegisterWindow(new(Role.Main, new(1000, 640)), config =>
        {
            var window = new MainWindow(config);
            window.Activate();
            return window;
        }, !SettingsData.I.StartupSetting.Hide);

        return Task.CompletedTask;
    }

    [STAThread]
    static int Main() => EP(() => _ = new App());
}
