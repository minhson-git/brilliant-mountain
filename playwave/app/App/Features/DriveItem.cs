using IOCore;
using IOCore.Collections;
using IOCore.Helpers;
using IOCore.Modules.WMI;
using IOCore.Types.Items;
using IOCore.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XMedia.Media.PlayerBase;
using static IOCore.Files.MediaFamily;
using static IOCore.Utils.DriveTypes;

namespace IOApp.Features;

public partial class DriveItem(string name, string label, DriveKind driveKind) : BaseItem
{
    public string Name { get; private set => SetAndNotify(ref field, value); } = name;
    public string Label { get; private set => SetAndNotify(ref field, value); } = label;
    public DriveKind DriveKind { get; private set => SetAndNotify(ref field, value); } = driveKind;

    public List<string> Paths { get; } = [];
    public bool IsScanned { get; private set; }

    public ObservableCollectionEx<PlayerItem> PlayerItems { get; } = [];
    public bool IsLoaded { get; private set; }

    public new string Text => $"{Label} ({Name})";
    public string Icon => DriveKind is DriveKind.Removable ? "\uE88E" : "\uE958";

    public void Update(string label, DriveKind driveKind)
    {
        Label = label;
        DriveKind = driveKind;

        Notify(nameof(Text), nameof(Icon));
    }

    public Task ScanPaths()
    {
        if (IsScanned)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(Path.GetFullPath(Name), "*", SearchOption.AllDirectories))
                    if (PlayerConfig.I.IsInputAccepted(path, MediaType.Media))
                        Paths.Add(path);
            }
            catch { }
            finally
            {
                IsScanned = true;
                AppEx.I.DispatcherQueue.UI(() => Notify(nameof(IsScanned), nameof(Paths)));
            }
        });
    }

    public async void LoadItems(Action? startAction, Action<bool, Exception?> endAction)
    {
        if (PlayerItems.Count > 0)
        {
            IsLoaded = true;
            endAction?.Invoke(true, null);
            return;
        }

        var exceptions = new ConcurrentQueue<Exception>();

        try
        {
            startAction?.Invoke();

            var phases = Paths.Phases(null);

            foreach (var (phase, i) in phases.Select((value, i) => (value, i)))
            {
                foreach (var package in phase)
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
                                else if (!PlayerItems.Any(i => PathUtils.Is(path, i.InputInfo.FullName)))
                                {
                                    var item = new PlayerItem(path);
                                    item.InputInfo.Analyze();
                                    item.InputInfo.FixedDrive = false;

                                    if (item.InputInfo.IsCorrupted)
                                        throw new Exception();

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
                        PlayerItems.AddRange(items.OrderBy(i => Array.IndexOf(package, i.InputInfo.FullName)));
                        await Task.Delay(20);
                    }
                }
            }

            endAction?.Invoke(true, exceptions.IsEmpty ? null : new AggregateException(exceptions));
        }
        catch (Exception ex)
        {
            exceptions.Enqueue(ex);
            endAction?.Invoke(false, new AggregateException(exceptions));
        }
    }
}