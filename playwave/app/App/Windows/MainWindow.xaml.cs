using CommunityToolkit.Mvvm.Input;
using FlyleafLib.MediaPlayer;
using H.Hooks;
using IOApp.Features;
using IOApp.Pages;
using IOCore;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Dialogs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.LazyQueue;
using IOCore.License;
using IOCore.Modules.Data;
using IOCore.UI;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using IOCore.Welcome;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using XMedia.Media.PlayerBase;
using static IOCore.Files.MediaFamily;

namespace IOApp.Windows;

internal partial class MainWindow : WindowEx
{
    public TitleBarEx TitleBar { get; }
    public NavigationViewEx NavigationView { get; }
    public NavigationViewEx? HomeNavigation => NavigationView.FindPageInCache<Home>()?.HomeNavigation;

    public bool IsSearchFilterVisible { get; set => SetAndNotify(ref field, value); }

    readonly LowLevelKeyboardHook _keyboardHook = new()
    {
        IsLeftRightGranularity = false,
        HandleModifierKeys = true,
        IsExtendedMode = true,
        Handling = true
    };

    public MainWindow(Config config) : base(config)
    {
        InitializeComponent();

        TitleBar = ResolveTitleBar(_tb);
        NavigationView = ResolveNavigationView(_nv, _f, _ => _.Navigate(typeof(Home), null));

        IsMinimizable = IsMaximizable = IsResizable = false;
        CInvokeEx.EnableMinimizable(Hwnd, true);
        CInvokeEx.EnableMaximizable(Hwnd, true);

        NavigationView.Navigated += (sender, _) =>
        {
            if (sender.CurrentPage is MediaPlayer)
            {
                Grid.SetRow(sender, 0);
                Grid.SetRowSpan(sender, 2);
            }
            else
            {
                Grid.SetRow(sender, 1);
                Grid.SetRowSpan(sender, 1);
            }

            SystemUtils.SetThreadExecutionState(sender.CurrentPage is MediaPlayer, sender.CurrentPage is MediaPlayer);
        };

        _ = DBManager.I.InitAsync(new AppDbContext(AppDir.LGet(AppDir.Type.BaseFolder)));
    }

    protected override void OnContentFirstLoaded()
    {
        PlayerEx.I.Player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Player.Status))
            {
                if (PlayerEx.I.Current is not null && PlayerEx.I.Player.IsPlaying)
                    PlayerContext.I.AddToRecent(PlayerEx.I.Current as PlayerItem);
            }
        };

        PlayerEx.I.Player.Audio.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Audio.Mute))
                PlayerData.I.Mute = PlayerEx.I.Player.Audio.Mute;
        };

        var menuFlyoutItem = new MenuFlyoutItem
        {
            Text = T.OpenVideosOrMusic,
            Icon = new FontIcon() { Glyph = "\ue8E5" },
            KeyboardAccelerators = { new() { Modifiers = VirtualKeyModifiers.Control, Key = VirtualKey.O } }
        };

        menuFlyoutItem.Click += (_, _) => _ = PlayerContext.AddFilesToPlayer(this, null);
        Menu.InsertMenuItem(0, menuFlyoutItem);

        _keyboardHook.Up += (_, e) =>
        {
            if (e.Keys.Are(Key.Menu, Key.S))
            {
                DispatcherQueue.UI(() => PlayerEx.I.Player.Config.Audio.ToggleMute());
                e.IsHandled = true;
            }
        };

        _keyboardHook.Start();

        if (!CoreData.I.IsToSAccepted)
            DialogService.From(this)?.Open(_ =>
            {
                var dialog = WelcomeDialog.Create(_);
                dialog.ClosingAction = () => Config.Refresh(AppWindow);
                AppWindow.Resize(new((int)dialog.Width + 100, (int)dialog.Height + 100));
                return dialog;
            });
        else
            AppEx.I.LzQueue.Enqueue(new LzAction(() => DispatcherQueue.UI(() =>
            {
                if (IOLicense.I.Status.IsTrial && AskSaver.I.ShouldDo("AskToBuy", 1, true, TimeSpan.FromDays(2)))
                    AppEx.LoadWindow<PremiumWindow>(_ => _.Activate());
            })));

        _ = AppSession.I.Offer(Menu.PromotionAppItems.ReplaceRange, 1);

        if (AppEx.I.NavArgs.WindowType == GetType())
            Load();

        Status.SetAndNotify(S.Ready);
    }

    protected override void OnReactivate()
    {
        Activate();
        Load();
    }

    public void Load()
    {
        if (AppEx.I.NavArgs.PageType != typeof(MediaPlayer))
        {
            NavigationView.Navigate(AppEx.I.NavArgs.PageType, null);
            return;
        }

        if (AppEx.I.NavArgs.Params.FirstOrDefault() is not string firstPath || string.IsNullOrWhiteSpace(firstPath))
            return;

        if (NavigationView.CurrentPage is not MediaPlayer)
            NavigationView.Navigate(typeof(MediaPlayer), null).Var<MediaPlayer>(_ => PlayerEx.I.Playlist.Clear());

        MediaPlayer.Open(this, firstPath);

        try
        {
            if (AppEx.I.NavArgs.Params.Count == 1)
            {
                if (Path.GetDirectoryName(firstPath) is not string directoryName || string.IsNullOrWhiteSpace(directoryName))
                    return;

                Dispatch.Async(async () =>
                {
                    var filePaths = await Task.Run(() =>
                        Directory.EnumerateFiles(directoryName, "*", SearchOption.TopDirectoryOnly).Where(p => p != firstPath && PlayerConfig.I.IsInputAccepted(p, MediaType.Media)).ToList());

                    if (filePaths.Count > 0)
                        await PlayerContext.AddFilesToPlayer(this, filePaths);
                });
            }
            else if (AppEx.I.NavArgs.Params.Count > 1)
            {
                var filePaths = AppEx.I.NavArgs.Params.Skip(1).Where(p => PlayerConfig.I.IsInputAccepted(p, MediaType.Media)).ToList();
                if (filePaths.Count > 0)
                    Dispatch.Async(async () => await PlayerContext.AddFilesToPlayer(this, filePaths));
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex.ToString());
        }
    }

    public Action<object, IReadOnlyList<string>> OnDropped => (_, paths) =>
    {
        if (NavigationView.CurrentPage is not MediaPlayer)
        {
            if (NavigationView.CurrentPage is Home)
            {
                if (HomeNavigation?.CurrentPage is Playlist playlist)
                {
                    if (playlist.SelectedPlaylistItem is not null)
                    {
                        var filePaths = paths.Where(i => FileUtils.IsFile(i));
                        if (filePaths.Any())
                            PlayerContext.I.AddMediasToPlaylists([playlist.SelectedPlaylistItem], [.. filePaths], null);

                        _ = AskSaver.I.ToRate(this, "AddMediasToPlaylists", 1, true, TimeSpan.FromDays(2), true, 10);
                    }
                }
                else if (HomeNavigation?.CurrentPage is Folder)
                {
                    PlayerContext.I.AddFolders(paths.Where(i => FileUtils.IsDirectory(i)));
                    _ = AskSaver.I.ToRate(this, "AddFolder", 1, true, TimeSpan.FromDays(2), true, 10);
                }
            }
        }
        else
            _ = PlayerContext.AddFilesToPlayer(this, paths.Where(path => FileUtils.IsFile(path)));
    };

    [RelayCommand]
    void AddFiles() => _ = PlayerContext.AddFilesToPlayer(this, null, true);

    [RelayCommand]
    void ShowSearchFilter() => IsSearchFilterVisible = !IsSearchFilterVisible;

    [RelayCommand]
    void ShowGrabberWindow() => AppEx.LoadWindow<GrabberWindow>(_ => _.Activate());

    [RelayCommand]
    void ShowPremiumWindow() => AppEx.LoadWindow<PremiumWindow>(_ => _.Activate());

    protected override void OnClosing(AppWindowClosingEventArgs e)
    {
        PlayerEx.I.Player.Pause();

        AppWindow.Hide();
        e.Cancel = true;
    }
}

internal abstract partial class MainWindowPage : TypedWindowPageEx<MainWindow> { }
internal abstract partial class MainWindowPlayerPage : BasePlayerPage<MainWindow> { }