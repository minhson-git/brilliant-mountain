using IOApp.Features;
using IOApp.Gens;
using IOApp.Pages;
using IOApp.Windows;
using IOCore;
using IOCore.License;
using IOCore.Modules.TrayIcon;
using IOCore.Premium;
using IOCore.UI.Behaviors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XMedia.Media;
using XMedia.Media.PlayerBase;
using static IOCore.Files.MediaFamily;

namespace IOApp;

public partial class Tray : BaseTray<Tray>
{
    MenuFlyoutItem? _premiumItem;

    Tray()
    {
    }

    protected override void OnMenuCreating(MenuFlyout menu)
    {
        MenuFlyoutItem item;

        item = new() { Text = T.Play, Icon = new FontIcon { Glyph = "\uE768" } };
        item.Click += (_, _) =>
            AppEx.LoadWindow<MainWindow>(async _ =>
                await PlayerContext.AddFilesToPlayer(_, null, true, () =>
                {
                    PlayerEx.I.Clear();
                    _.Activate();
                })
            );
        menu.Items.Add(item);

        item = new() { Text = T.Convert, Icon = new FontIcon { Glyph = "\uEA69" } };
        item.Click += (_, _) =>
            AppEx.LoadWindow<ConverterWindow>(async window =>
            {
                var pickFileResult = await Picker.OpenSingleFile(window, picker =>
                {
                    foreach (var i in PlayerConfig.I.InputMediaExtensions)
                        picker.FileTypeFilter.Add(i);
                });

                if (pickFileResult is not null)
                {
                    var info = MediaInfoBase.Create(pickFileResult.Path);
                    window.Activate();
                    window.Load(info.FullName, info.HasVideo ? MediaType.Video : MediaType.Audio);
                }
            });
        menu.Items.Add(item);

        item = new() { Text = T.OnlineMusicGrabber, Icon = new FontIcon { Glyph = "\uE8D6" } };
        item.Click += (_, _) =>
            AppEx.LoadWindow<GrabberWindow>(_ => _.Activate());
        menu.Items.Add(item);
    }

    protected override void OnMenuCreated(MenuFlyout menu)
    {
        MenuFlyoutItem item;

        item = _premiumItem = new() { Text = PremiumCore.I.ConceptText, Icon = new FontIcon { Glyph = "\uE728" } };
        item.Click += (_, _) =>
            AppEx.LoadWindow<PremiumWindow>(_ =>
            {
                _.Activate();
                _.NavigationView.Navigate(typeof(Premium), null);
            });
        menu.Items.Add(item);
    }

    protected override void OnLicenseStatusChanged()
    {
        _premiumItem?.Visibility = IOLicense.I.Status.IsTrial ? Visibility.Visible : Visibility.Collapsed;
    }
}
