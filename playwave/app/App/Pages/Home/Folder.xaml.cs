using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOApp.Gens;
using IOCore.Annotation;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Helpers;
using IOCore.License;
using IOCore.Types;
using IOCore.UI;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XMedia.Media.PlayerBase;

namespace IOApp.Pages;

[BindingProxy]
internal partial class Folder : HomePage
{
    public CheckableCollection<FolderItem> FolderCheckList { get; }

    public Folder()
    {
        InitializeComponent();

        FolderCheckList = new((source, isChecked) => new FolderCheckableItem(source, isChecked));
        FolderCheckList.CheckChanged += (_, e) =>
        {
            if (e is not null)
                PlayerContext.ToggleFolder(e);

            var items = new List<PlayerItem>();
            foreach (var i in FolderCheckList.CheckedItems)
                items.AddRange(PlayerContext.I.Data.FOLDER_DATA_ITEMS.Where(fileItem => PathUtils.IsSubPath(i.Path, fileItem.InputInfo.FullName)));

            RefinedPlayerItems.ReplaceRange(items);
        };

        PlayerContext.I.Data.FolderItemsAdded += (_, items) => DispatcherQueue.UI(() => FolderCheckList.AddRangeIfNotExist(items, true));
        PlayerContext.I.Data.FolderItemsRemoved += (_, items) => DispatcherQueue.UI(() => FolderCheckList.RemoveAll(i => items.Contains(i.Source)));

        PlayerContext.I.Data.FolderDataItemsAdded += (_, items) => DispatcherQueue.UI(() =>
        {
            var oldSortStatus = RefinedPlayerItems.IsSortPaused;
            RefinedPlayerItems.IsSortPaused = true;
            RefinedPlayerItems.AddRangeIfNotExisted(items.Where(i => i.BaseFolderPath is not null && FolderCheckList.CheckedItems.Any(folderItem => PathUtils.IsSubPath(i.BaseFolderPath, folderItem.Path))));
            RefinedPlayerItems.IsSortPaused = oldSortStatus;
        });

        PlayerContext.I.Data.FolderDataItemsRemoved += (_, items) => DispatcherQueue.UI(() => RefinedPlayerItems.RemoveRange(items));

        PlayerContext.I.PrepareFolderItems();
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
    async Task AddFolder()
    {
        (await Picker.OpenSingleFolder(Window))
        .Let(async _ =>
        {
            PlayerContext.I.AddFolders([_.Path]);
            await AskSaver.I.ToRate(Window, "AddFolder", 1, true, TimeSpan.FromDays(2), true, 10);
        });
    }

    [RelayCommand]
    void PlayAll() => RefinedPlayerItems.FirstOrDefault(i => i.InputInfo is not null && !i.InputInfo.IsCorrupted).Let(item => PlayerEx.I.Open(item, [.. RefinedPlayerItems]));

    [RelayCommand]
    void RemoveFolder(object sender) => Tip.Confirm(sender as FrameworkElement, T.RemoveSelected, null, () =>
        sender.LetDataContext<FolderCheckableItem>(item => PlayerContext.I.RemoveFolder(item.Source)));
}