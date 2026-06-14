using IOCore;
using IOCore.Base;
using IOCore.Files;
using IOCore.Libs;
using IOMedia.Media;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static IOCore.Files.MediaFamily;

namespace IOApp.Features
{
    public partial class DriveItem(string name, string label, DriveType driveType) : BaseItem
    {
        public PlayerConfig PlayerConfig { get; } = EncapsulatedSingleton<PlayerConfig>.ExposeInstance();

        string _name = name;
        public string Name { get => _name; private set => SetAndNotify(ref _name, value); }

        string _label = label;
        public string Label { get => _label; private set => SetAndNotify(ref _label, value); }

        DriveType _driveType = driveType;
        public DriveType DriveType { get => _driveType; private set => SetAndNotify(ref _driveType, value); }

        public List<string> Paths { get; private set; } = [];
        public bool IsScanned { get; private set; }

        public ObservableCollectionEx<PlayerItem> PlayerItems { get; } = [];
        public bool IsLoaded { get; private set; }

        public new string Text => $"{_label} ({_name})";
        public string Icon => _driveType == DriveType.Removable ? "\uE88E" : "\uE958";

        public void Update(string label, DriveType driveType)
        {
            Label = label;
            DriveType = driveType;

            Notify(
                nameof(Text),
                nameof(Icon));
        }

        public Task ScanPaths()
        {
            if (IsScanned)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                try
                {
                    Paths.AddRange(FileUtils.GetFiles(Name, "*").Where(i => PlayerConfig.IsInputAccepted(i, MediaType.Media)));
                }
                catch { }
                finally
                {
                    IsScanned = true;

                    AppEx.UI(() =>
                        Notify(
                            nameof(IsScanned),
                            nameof(Paths)));
                }
            });
        }

        public async void LoadItems(Action? startAction, Action<bool, Exception?> endAction)
        {
            var exceptions = new ConcurrentBag<Exception>();

            try
            {
                if (PlayerItems.Count > 0)
                {
                    IsLoaded = true;
                    endAction?.Invoke(true, null);
                    return;
                }

                startAction?.Invoke();

                var phases = Paths.Phases(null);

                foreach (var (phase, i) in phases.Select((value, i) => (value, i)))
                {
                    foreach (var package in phase)
                    {
                        var items = new ConcurrentBag<PlayerItem>();

                        foreach (var itemsPerProcess in package.Chunk(Environment.ProcessorCount))
                        {
                            await Parallel.ForEachAsync(itemsPerProcess, (path, ct) =>
                            {
                                try
                                {
                                    if (!PlayerItems.Any(i => i.InputInfo.FullName == path))
                                    {
                                        var item = PlayerItem.Create<PlayerItem>(path, false);
                                        item.InputInfo.Analyze();
                                        item.InputInfo.FixedDrive = false;

                                        if (item.InputInfo.IsCorrupted)
                                            throw new Exception();

                                        items.Add(item);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    exceptions.Add(ex);
                                }

                                return ValueTask.CompletedTask;
                            });
                        }

                        if (!items.IsEmpty)
                            PlayerItems.Add(items.OrderBy(i => Array.IndexOf(package, i.InputInfo.FullName)));

                        await Task.Delay(100);
                    }
                }

                endAction?.Invoke(true, exceptions.IsEmpty ? null : new AggregateException(exceptions));
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                endAction?.Invoke(false, new AggregateException(exceptions));
            }
        }
    }
}