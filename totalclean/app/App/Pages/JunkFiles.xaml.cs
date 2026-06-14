using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;
using static IOApp.Features.Share;

namespace IOApp.Pages
{
    internal partial class JunkFiles : MainWindowPage
    {
        public IOStatus<JunkFiles, S> Status { get; }

        public ObservableCollectionEx<JunkFileGroupItem> JunkFileGroupItems { get; } = [];

        public long TotalSize { get; private set; }
        public long JunkFileSize { get; private set; }

        bool _isChecked = true;
        public bool IsChecked
        {
            get => _isChecked;
            set => SetAndNotify(ref _isChecked, value);
        }

        public JunkFiles()
        {
            InitializeComponent();
            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) => Window.SetBusy(Status.IsBusy);
        }

        async Task UpdateItemAndTotalSize(JunkFileGroupItem item, bool isCleanOrScan = false)
        {
            item.Status.SetAndNotify(JunkFileGroupItem.S.Processing);

            await Task.Delay(500);

            if (isCleanOrScan)
                await item.Clean(action => DispatcherQueue.TryEnqueue(action.Invoke));
            else
                await item.Scan(action => DispatcherQueue.TryEnqueue(action.Invoke));

            item.Status.SetAndNotify(JunkFileGroupItem.S.Processed);

            TotalSize = JunkFileGroupItems.Select(i => i.TotalSize).Sum();
            JunkFileSize = JunkFileGroupItems.Where(i => i.IsSelected).Select(i => i.TotalSize).Sum();
            
            Notify(nameof(JunkFileSize));
            Notify(nameof(TotalSize));
        }

        [RelayCommand]
        void SelectItem(JunkFileGroupItem item)
        {
            item.IsSelected = !item.IsSelected;
            JunkFileSize = JunkFileGroupItems.Where(i => i.IsSelected).Select(i => i.TotalSize).Sum();
            Notify(nameof(JunkFileSize));
        }

        [RelayCommand]
        void SelectFiles()
        {
            IsChecked = !IsChecked;
            JunkFileGroupItems.ForEach(i => i.IsSelected = IsChecked);

            TotalSize = JunkFileGroupItems.Select(i => i.TotalSize).Sum();
            JunkFileSize = JunkFileGroupItems.Where(i => i.IsSelected).Select(i => i.TotalSize).Sum();

            Notify(nameof(JunkFileSize), nameof(TotalSize));
        }


        [RelayCommand]
        void ScanItem(JunkFileGroupItem item) => Dispatch.Async(async () => await UpdateItemAndTotalSize(item));

        [RelayCommand]
        void CleanItem(JunkFileGroupItem item) => Window.Tip.Confirm(null, "Clean Item", "Clean Item Sub", async () =>
        {
            var cleanedSize = item.TotalSize;
            await UpdateItemAndTotalSize(item, true);

            Window.Tip.Message(null, R.T(L.Info_CleanResultTitle), FileUtils.GetReadableByteSizeText(cleanedSize));
        });

        [RelayCommand]
        void CancelItem(JunkFileGroupItem item)
        {
            item.Status.SetAndNotify(JunkFileGroupItem.S.ProcessStopped);
            item.CancellationTokenSource?.Cancel();
        }

        [RelayCommand]
        void ScanAll()
        {
            Dispatch.Async(async () =>
            {
                if (Status.IsReady)
                    AppTypes.CACHE_DIRECTORIES.ForEach(i =>
                    {
                        if (i.Key == AppTypes.CacheDirectoryType.RecycleBin)
                            JunkFileGroupItems.Add(new JunkFileGroupItem(i.Value.Item1, i.Value.Item2, [], true));
                        else
                            JunkFileGroupItems.Add(new JunkFileGroupItem(i.Value.Item1, i.Value.Item2, [.. i.Value.Item3]));
                    });

                Status.SetAndNotify(S.Processing);

                JunkFileGroupItems.ForEach(item => item.Status.SetAndNotify(JunkFileGroupItem.S.ProcessInQueue));

                foreach (var item in JunkFileGroupItems)
                    await UpdateItemAndTotalSize(item);

                Status.SetAndNotify(S.Processed);
            });
        }

        [RelayCommand]
        void CleanAll()
        {
            Window.Tip.Confirm(null, R.T(L.Notification_CleanAll), R.T(L.Notification_CleanAllSub), async () =>
            {
                Status.SetAndNotify(S.Processing);

                var cleanedSize = TotalSize;

                JunkFileGroupItems.ForEach(i => i.Status.SetAndNotify(JunkFileGroupItem.S.ProcessInQueue));

                foreach (var item in JunkFileGroupItems)
                {
                    if (item.IsSelected && item.IsActivated)
                        await UpdateItemAndTotalSize(item, true);
                    else
                        item.Status.SetAndNotify(JunkFileGroupItem.S.Processed);
                }

                cleanedSize -= TotalSize;

                Window.Tip.Message(null, R.T(L.Info_CleanResultTitle), FileUtils.GetReadableByteSizeText(cleanedSize));

                Status.SetAndNotify(S.Processed);
            });
        }

        [RelayCommand]
        void CancelAll()
        {
            JunkFileGroupItems.ForEach(i => i.Status.SetAndNotify(JunkFileGroupItem.S.ProcessStopped));
            JunkFileGroupItems.ForEach(i => i.CancellationTokenSource?.Cancel());
        }
    }

    internal class JunkFilesProxy : BindingProxy<JunkFiles> { }
}