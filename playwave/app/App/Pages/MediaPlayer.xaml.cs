using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOApp.Windows;
using IOCore;
using IOCore.Base;
using IOCore.Collections;
using IOCore.Gens;
using IOCore.License;
using IOCore.UI.Behaviors;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using XMedia.Media.PlayerBase;
using static IOCore.Files.MediaFamily;

namespace IOApp.Pages;

internal partial class MediaPlayer : MainWindowPlayerPage
{
    public MediaPlayer()
    {
        InitializeComponent();

        OnLicenseStatusChanged();
    }

    protected override void OnLicenseStatusChanged()
    {
        foreach (var i in SleepCheckableMenuFlyout.TypedItems)
        {
            i.IsEnabled = IOLicense.I.Status.IsPremium || i.Key is SleepDelay.Off or SleepDelay._60 or SleepDelay.Ended;
            i.Text = $"{(i.IsEnabled ? string.Empty : "🌟 ")}{i.OriginalText}";
        }

        foreach (var i in SpeedCheckableMenuFlyout.TypedItems)
        {
            i.IsEnabled = IOLicense.I.Status.IsPremium || i.Key is Speed._0_5 or Speed._0_75 or Speed._1 or Speed._1_25 or Speed._1_5;
            i.Text = $"{(i.IsEnabled ? string.Empty : "🌟 ")}{i.OriginalText}";
        }
    }

    protected void Container_RightTapped(object sender, RightTappedRoutedEventArgs e) =>
        PlayerEx.I.Current.Var<PlayerItem>(playerItem =>
            MenuContext.I.Update(playerItem, false, true, true, false, false,
                null,
                null,
                i => RemoveAll([i], false),
                i => Tip.Confirm(Window, T.RemovePermanently, T.CannotBeUndone, () => RemoveAll([i], true)))
            .ShowAt(e.OriginalSource as UIElement, e.GetPosition(e.OriginalSource as UIElement)));

    public override bool SelectItem(object? item, bool scrollToView)
    {
        if (base.SelectItem(item, scrollToView))
        {
            PlayerContext.I.AddToRecent(PlayerEx.I.Current as PlayerItem);
            return true;
        }

        return false;
    }

    public override void RemoveAll(IEnumerable<BasePlayerItem> removingItems, bool doDelete)
    {
        base.RemoveAll(removingItems, doDelete);
        PlayerContext.I.RemoveAll(removingItems.OfType<PlayerItem>(), doDelete);
    }

    [RelayCommand]
    void AddFiles() => _ = PlayerContext.AddFilesToPlayer(Window, null);

    [RelayCommand]
    void Convert()
    {
        if (PlayerEx.I.Current is not null)
            AppEx.LoadWindow<ConverterWindow>(window =>
            {
                PlayerEx.I.Player.Pause();
                window.Activate();
                window.Load(PlayerEx.I.Current.InputInfo.FullName, PlayerEx.I.Current.InputInfo.HasVideo ? MediaType.Video : MediaType.Audio);
            });
    }

    async Task AddFiles(IEnumerable<string> paths, Action<PlayerEx.S> startAction, Action<List<PlayerItem>, int, int> packageAction, Action<PlayerEx.S, Exception?, bool> endAction)
    {
        var exceptions = new ConcurrentQueue<Exception>();

        startAction?.Invoke(PlayerEx.S.Loading);

        try
        {
            var phases = paths.Phases(null);

            foreach (var (phase, phaseIndex) in phases.Select((value, i) => (value, i)))
            {
                foreach (var (package, packageIndex) in phase.Select((value, i) => (value, i)))
                {
                    var items = new ConcurrentQueue<PlayerItem>();

                    foreach (var itemsPerProcess in package.Chunk(Environment.ProcessorCount))
                    {
                        await Parallel.ForEachAsync(itemsPerProcess, (path, ct) =>
                        {
                            try
                            {
                                var samePathInstanceInContext = PlayerContext.I.Data.DATA_ITEMS.FirstOrDefault(i => PathUtils.Is(path, i.InputInfo.FullName));

                                if (samePathInstanceInContext is not null)
                                    items.Enqueue(samePathInstanceInContext);
                                else if (!PlayerEx.I.Playlist.Any(i => PathUtils.Is(i.InputInfo.FullName, path)))
                                {
                                    if (!PlayerConfig.I.IsInputAccepted(path, MediaType.Media))
                                        throw new UnacceptedInputException();

                                    var item = new PlayerItem(path);
                                    item.InputInfo.Analyze();

                                    if (!item.InputInfo.IsCorrupted)
                                        items.Enqueue(item);
                                }
                            }
                            catch (Exception ex)
                            {
                                exceptions.Enqueue(ex);
                            }

                            return ValueTask.CompletedTask;
                        });
                    }

                    if (!items.IsEmpty)
                    {
                        packageAction?.Invoke([.. items.OrderBy(i => Array.IndexOf(package, i.InputInfo.FullName))], phaseIndex, packageIndex);
                        await Task.Delay(20);
                    }
                }
            }

            endAction?.Invoke(PlayerEx.S.Loaded, exceptions.IsEmpty ? null : new AggregateException(exceptions), false);
        }
        catch (Exception ex)
        {
            exceptions.Enqueue(ex);
            endAction?.Invoke(PlayerEx.S.LoadFailed, new AggregateException(exceptions), false);
        }
    }

    public static bool Open(WindowEx window, string path)
    {
        var item = new PlayerItem(path);
        item.InputInfo.Analyze();

        return Open<MediaPlayer>(window, item, [item]);
    }

    public override void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}