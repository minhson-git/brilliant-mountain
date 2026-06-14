using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOApp.Gens;
using IOCore.AppManager;
using IOCore.Helpers;
using IOCore.License;
using IOCore.UI.Behaviors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using XMedia.Media.PlayerBase;

namespace IOApp.Pages;

internal partial class Recent : HomePage
{
    public Recent()
    {
        InitializeComponent();

        PlayerContext.I.Data.RecentDataItemsAdded += (_, items) => DispatcherQueue.UI(() => RefinedPlayerItems.InsertRangeIfNotExist(0, items));
        PlayerContext.I.Data.RecentDataItemsRemoved += (_, items) => DispatcherQueue.UI(() => RefinedPlayerItems.RemoveRange(items));

        RefinedPlayerItems.AddRange(PlayerContext.I.Data.RECENT_DATA_ITEMS);
    }

    protected override void OnNavigatedToEx(NavigationEventArgs e)
    {
        _lzQueue.Resume();
        Status.NotifyAll();
    }

    protected override void OnNavigatingFromEx(NavigatingCancelEventArgs e)
    {
        _lzQueue.Stop(true);
    }

    protected override void OnLicenseStatusChanged()
    {
        if (IOLicense.I.Status.IsTrial)
            _ = AppSession.I.Offer(Menu.PromotionAppItems.ReplaceRange, 1);
    }

    void PlayerItemControl_OnHandle(object sender, PlayerItemEventArgs e)
    {
        if (e.Kind is PlayerItemEventKind.Preview)
            PlayerEx.I.Open(e.Item, [.. RefinedPlayerItems]);
        else if (e.Kind is PlayerItemEventKind.Open)
            MediaPlayer.Open<MediaPlayer>(Window, e.Item, [.. RefinedPlayerItems]);
    }

    [RelayCommand]
    void RemoveAll(object sender) => Tip.Confirm(sender as FrameworkElement, T.ClearItemList, null, () => PlayerContext.I.RemoveAll([.. RefinedPlayerItems], false));
}