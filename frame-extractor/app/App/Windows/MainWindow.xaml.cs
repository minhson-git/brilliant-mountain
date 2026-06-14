using IOApp.Pages;
using IOCore;
using IOCore.AppManager;
using IOCore.Core;
using IOCore.Files;
using IOCore.Premium;
using IOCore.UI;
using IOCore.Utils;
using IOMedia;
using IOMedia.Media.ConverterBase;
using System;
using ZAnnotation.IOCore;
using static IOCore.Files.MediaTypes;

namespace IOApp
{
    [BindingProxy]
    internal partial class MainWindow : WindowEx
    {
        public ConverterConfig ConverterConfig => ConverterConfig.ExposeInstance();

        public MainWindow(Config config) : base(config)
        {
            InitializeComponent();

            Status.PropertyChanged += (_, _) => SystemUtils.SetThreadExecutionState(Status.IsBusy, false);

            AttachTitleBarAndNavigationView(TitleBar, NavigationView.Init(_frame));
        }

        protected override void OnContentFirstLoaded()
        {
            var isPromotionFlyoutVisible = true;

            if (License.Status.IsTrial && AskSaver.Inst.ShouldDo(null, 1, true, TimeSpan.FromDays(2)))
            {
                _ = Dialog.Open(PremiumDialog.Create(this));
                isPromotionFlyoutVisible = false;
            }

            Promotion.Inst.Offer(items =>
            {
                PromotionAppItems.Replace(items);
                Notify(nameof(PromotionAppItems));

                if (PromotionAppItems.IsNotEmpty && MathUtils.Chance(Promotion.Inst.OfferRate) && isPromotionFlyoutVisible)
                    Menu.ShowPromotionCommand.Execute(PromotionAppItemButton);
            }, 1);

            NavigationView.Navigate(typeof(Home), null);

            Status.SetAndNotify(S.Ready);
        }

        protected override void OnLicenseStatusChanged()
        {
            if (License.Status.IsTrial)
            {
                ConvertArgv.DefaultMaxAudioBitrate = AudioBitrate._64;
                ConvertArgv.DefaultMaxResolution = Resolution.Hd_720p;
            }
            else
            {
                ConvertArgv.DefaultMaxAudioBitrate = AudioBitrate.Auto;
                ConvertArgv.DefaultMaxResolution = Resolution.Auto;
            }
        }
    }

    internal abstract partial class MainWindowConverterPage(MediaFamily.MediaType converterType) : ConverterPage<MainWindow>(converterType) { }
}