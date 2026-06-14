using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOCore;
using IOCore.AppManager;
using IOCore.Base;
using IOCore.Core;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
using IOCore.Services;
using IOCore.UI;
using IOCore.Utils;
using IOImage;
using IOImage.Presenter;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI;
using static IOApp.Features.Share;

namespace IOApp.Pages
{
    internal partial class ViewerPage : MainWindowPage, IDisposable
    {
        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();
        public ViewerConfig ViewerConfig { get; } = EncapsulatedSingleton<ViewerConfig>.ExposeInstance();
        public ViewerPackageStorage ViewerPackageStorage { get; } = EncapsulatedSingleton<ViewerPackageStorage>.ExposeInstance();

        public Presenter<ViewerItem> Presenter => Window.Presenter;

        public IOStatus<ViewerPage, S> Status { get; }

        public string InputTypes => FormatUtils.LayoutGridString(ViewerConfig.InputImageExtensions);

        readonly Debounce _debounce = new();
        bool _isHudVisible;
        public bool IsHudVisible { get => _isHudVisible; private set => SetAndNotify(ref _isHudVisible, value); }

        readonly MilestoneCounter _offerCounter = new();

        public ViewerPage()
        {
            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                Window.Cover.ShowLoading(Status.IsLoading, Cover.CanvasType.None);
                Window.SetBusy(Status.IsBusy);
            };
        }

        protected override void OnNavigatedToEx(Frame sender, object? parameter, NavigationEventArgs e)
        {
            if (NavigateCount == 1)
            {
                KeyboardUtils.AddInputs(Content,
                [
                    new(VirtualKeyModifiers.None, VirtualKey.L, (_, _) => ViewerPackageStorage.IsListVisible = !ViewerPackageStorage.IsListVisible),
                    new(VirtualKeyModifiers.None, VirtualKey.I, (_, _) => ViewerPackageStorage.IsInfoVisible = !ViewerPackageStorage.IsInfoVisible),
                    new(VirtualKeyModifiers.None, VirtualKey.F5, (_, _) => Reload()),
                    new(VirtualKeyModifiers.None, VirtualKey.Escape, (_, _) =>
                    {
                        if (Window.IsTiny)
                            Window.IsTiny = false;
                        else if (Window.IsFullScreen)
                            Window.IsFullScreen = false;
                        else
                            Window.TryGoBackCommand.Execute(null);
                    }),
                    new(VirtualKeyModifiers.None, VirtualKey.F11, (_, _) =>
                    {
                        if (Window.IsTiny)
                            Window.IsTiny = false;
                        else
                            Window.IsFullScreen = !Window.IsFullScreen;
                    }),
                    new(VirtualKeyModifiers.None, VirtualKey.Back, (_, _) => Window.TryGoBackCommand.Execute(null)),
                    new(VirtualKeyModifiers.None, VirtualKey.Left, (_, _) => SelectItem(Presenter.Album.Previous(Presenter.Current), true)),
                    new(VirtualKeyModifiers.None, VirtualKey.Right, (_, _) => SelectItem(Presenter.Album.Next(Presenter.Current), true)),
                    new(VirtualKeyModifiers.Control, VirtualKey.Number0, (_, _) => Presenter.Viewer.ZoomTo(Presenter.Viewer.Transformation.Scale == 100)),
                    new(VirtualKeyModifiers.Control, VirtualKey.NumberPad0, (_, _) => Presenter.Viewer.ZoomTo(Presenter.Viewer.Transformation.Scale == 100)),
                    new(VirtualKeyModifiers.Control, VirtualKey.Subtract, (_, _) => Presenter.Viewer.ZoomOut(null)),
                    new(VirtualKeyModifiers.Control, VirtualKey.Add, (_, _) => Presenter.Viewer.ZoomIn(null)),
                    new(VirtualKeyModifiers.Control, VirtualKey.R, (_, _) => Presenter.Viewer.Rotate(90)),
                ]);
            }

            _offerCounter.Reset();

            Window.PropertyChanged += Window_PropertyChanged;
            ViewerPackageStorage.PropertyChanged += ViewerPackageStorage_PropertyChanged;
            Presenter.Album.CollectionChanged += Album_CollectionChanged;

            _lzQueue.Resume();

            Notify(nameof(Presenter));

            Window.SetBusy(Status.IsBusy);
        }

        protected override void OnNavigatingFromEx(NavigatingCancelEventArgs e)
        {
            _lzQueue.Stop(true);

            Window.IsFullScreen = Window.IsTiny = false;

            Window.PropertyChanged -= Window_PropertyChanged;
            ViewerPackageStorage.PropertyChanged -= ViewerPackageStorage_PropertyChanged;
            Presenter.Album.CollectionChanged -= Album_CollectionChanged;
		}

        void Window_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WindowEx.IsTiny))
            {
                Window.IsMinimizable = Window.IsMaximizable = !Window.IsTiny;
                Window.IsAlwaysOnTop = Window.IsTiny;

                if (Window.IsTiny)
                    Window.Config.SetSizeOption(Config.Option.Tiny, Window.AppWindow);
                else
                    Window.Config.SetSizeOption(Config.Option.Normal, Window.AppWindow);

                ScreenUtils.CenterOnScreen(Window.Hwnd);

                _ = Ask.Inst.ToRate(Window, "IsTiny", 1, true, TimeSpan.FromDays(2), true, 10);
            }
        }

        protected void ViewerPackageStorage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewerPackageStorage.IsListVisible))
                Presenter.Viewer.ResetTransformations(false);
        }

        void Album_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Notify(nameof(Presenter));
        }

        protected void Host_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Panel>(panel =>
            {
                Presenter.XG.PresentTo(panel);
                Presenter.AutoPlay = false;

                Notify(nameof(Presenter));
            });

        protected void DragDropGrid_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<Panel>(panel =>
            {
                panel.Loaded -= DragDropGrid_Loaded;
                DragDrop.Register(panel, storageItems =>
                {
                    //var filePaths = storageItems.Where(i => FileUtils.IsFile(i.Path)).Select(i => i.Path);
                    //if (filePaths.IsNotNullAndNotEmpty())
                    //    _ = ViewerContext.AddFiles(Window, [.. filePaths]);
                });
            });

        protected void ListViewEx_Loaded(object sender, RoutedEventArgs e) =>
            sender.Var<ListViewEx>(listView =>
            {
                listView.Loaded -= ListViewEx_Loaded;

                listView.SelectedIndex = listView.Items.IndexOf(Presenter.Current);
                listView.UpdateLayout();
                listView.ScrollIntoView(listView.SelectedItem);

                Presenter.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(Presenter.Current))
                    {
                        listView.SelectedIndex = listView.Items.IndexOf(Presenter.Current);
                        listView.UpdateLayout();
                        listView.ScrollIntoView(listView.SelectedItem);
                    }
                };
            });

        public static void Open(WindowEx window, ViewerItem item, IEnumerable<ViewerItem> items)
        {
            if (item == null || item.InputInfo.IsCorrupted)
                return;

            items = items.Where(i => !i.InputInfo.IsCorrupted);

            if (items.Any(i => i == item))
                window.Navigate<ImageViewer>(null).Let(page =>
                {
                    page.Presenter.Open(item, [.. items]);
                    page.SelectItem(item, true);
                });
        }

        public void RemoveAll(IEnumerable<ViewerItem> removingItems, bool doDelete)
        {
            var nextSelectedItem = Presenter.Album.NextSelected(Presenter.Current, removingItems);

            var items = Presenter.Album.Where(i => removingItems.Any(item => item.InputInfo.FullName == i.InputInfo.FullName)).ToList();
            foreach (var item in items)
                Presenter.Album.RemoveEx(item);

            _lzQueue.Clear();

            SelectItem(nextSelectedItem, true);

            //if (doDelete)
            //    ViewerContext.Inst.RemoveAll(removingItems, true);
        }

        public void SelectItem(object? item, bool scrollToView) =>
            item.Var<ViewerItem>(_ =>
            {
                Presenter.Current = _;
                Presenter.Current.ThumbnailEnqueued(_lzQueue);

                Notify(nameof(Presenter));

                //ViewerContext.Inst.AddToRecent(Presenter.Current);

                if (Window.LicenseStatus.IsTrial)
                {
                    var slug = ViewerConfig.GetExtra(Presenter.Current.InputInfo.Extension);
                    Promotion.Inst.Offer(items =>
                    {
                        PromotionAppItems.Replace(items);
                        Notify(nameof(PromotionAppItems));
                    }, 1, null, slug == null ? null : [slug]);
                }
            });

        protected void Container_SizeChanged(object sender, SizeChangedEventArgs e) => Presenter.Viewer.ResetTransformations(true);

        protected void Container_PointerMoved(object sender, PointerRoutedEventArgs e) =>
            sender.Var<GridEx>(panel =>
            {
                if (!_isHudVisible)
                {
                    IsHudVisible = true;
                    panel.TryVisibleCursor(true);
                }

                _debounce.Run(() => DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isHudVisible)
                    {
                        IsHudVisible = false;
                        panel.TryVisibleCursor(false);
                    }
                }), 3000);
            });

        protected void Container_RightTapped(object sender, RightTappedRoutedEventArgs e) =>
            Presenter.Current.Let(viewerItem =>
            {
                MenuContext.Inst.Update(viewerItem, false, true, false,
                    null,
                    i => RemoveAll([i], false),
                    i => Window.Tip.Confirm(null, R.T(L.RemovePermanently), R.T(L.CannotBeUndone), () => RemoveAll([i], true)))
                .ShowAt(e.OriginalSource as UIElement, e.GetPosition(e.OriginalSource as UIElement));
            });

        protected void Item_RightTapped(object sender, RightTappedRoutedEventArgs e) =>
            (e.OriginalSource as UIElement)?.LetDataContext<ViewerItem>(viewerItem =>
            {
                MenuContext.Inst.Update(viewerItem, true, true, true,
                    null,
                    i => RemoveAll([i], false),
                    i => Window.Tip.Confirm(null, R.T(L.RemovePermanently), R.T(L.CannotBeUndone), () => RemoveAll([i], true)))
                .ShowAt(sender as UIElement, new Windows.Foundation.Point(0, 0));
            });

        protected void ListViewEx_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (Presenter.Current != e.ClickedItem)
                SelectItem(e.ClickedItem, false);
        }

        protected void ListViewEx_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue)
                if (args.Item is ViewerItem item)
                    item.ThumbnailEnqueued(_lzQueue);
        }

        #region Commands

        [RelayCommand]
        void AddFiles()
        {
            //_ = ViewerContext.AddFiles(Window, null);
        }

        [RelayCommand]
        void Reload() => SelectItem(Presenter.Current, true);

        [RelayCommand]
        void HorizontalFlip() => Presenter.Viewer.FlipFlop(true, false);

        [RelayCommand]
        void VerticalFlip() => Presenter.Viewer.FlipFlop(false, true);

        [RelayCommand]
        void ClockwiseRotation() => Presenter.Viewer.Rotate(90);

        [RelayCommand]
        void Reset() => Presenter.Viewer.ResetTransformations(true);

        [RelayCommand]
        void ZoomIn() => Presenter.Viewer.ZoomIn(null);

        [RelayCommand]
        void ZoomOut() => Presenter.Viewer.ZoomOut(null);

        [RelayCommand]
        void ResetZoom() => Presenter.Viewer.ZoomTo(Presenter.Viewer.Transformation.Scale == 100);

        [RelayCommand]
        void Previous() => SelectItem(Presenter.Album.Previous(Presenter.Current), true);

        [RelayCommand]
        void Next() => SelectItem(Presenter.Album.Next(Presenter.Current), true);

        [RelayCommand]
        void RotateRight() => Presenter.Viewer.Rotate(90);

        [RelayCommand]
        void DisplayList() => ViewerPackageStorage.IsListVisible = !ViewerPackageStorage.IsListVisible;

        [RelayCommand]
        void More(object sender) =>
            Presenter.Current.Let(item =>
                MenuContext.Inst.Update(item, true, true, false,
                        null,
                        i => RemoveAll([i], false),
                        i => Window.Tip.Confirm(null, R.T(L.RemovePermanently), R.T(L.CannotBeUndone), () => RemoveAll([i], true)))
                    .ShowAt(sender as FrameworkElement, new Windows.Foundation.Point(0, 0))
            );

        #endregion

        async Task AddFiles(IEnumerable<string> paths, Action<S> startAction, Action<List<ViewerItem>, int, int> packageAction, Action<S, Exception?, bool> endAction)
        {
            var exceptions = new ConcurrentBag<Exception>();

            startAction?.Invoke(S.Loading);

            try
            {
                var phases = paths.Phases(null);

                foreach (var (phase, phaseIndex) in phases.Select((value, i) => (value, i)))
                {
                    foreach (var (package, packageIndex) in phase.Select((value, i) => (value, i)))
                    {
                        var items = new ConcurrentBag<ViewerItem>();

                        foreach (var itemsPerProcess in package.Chunk(Environment.ProcessorCount))
                        {
                            await Parallel.ForEachAsync(itemsPerProcess, (path, ct) =>
                            {
                                try
                                {
                                    if (!Presenter.Album.Any(i => i.InputInfo.FullName == path))
                                    {
                                        if (!ViewerConfig.IsInputAccepted(path))
                                            throw new UnacceptedInputException();

                                        var item = ImageItem.Create<ViewerItem>(path);
                                        item.InputInfo.Analyze();

                                        if (!item.InputInfo.IsCorrupted)
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
                            packageAction?.Invoke([..items.OrderBy(i => Array.IndexOf(package, i.InputInfo.FullName))], phaseIndex, packageIndex);

                        await Task.Delay(100);
                    }
                }

                endAction?.Invoke(S.Loaded, exceptions.IsEmpty ? null : new AggregateException(exceptions), false);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                endAction?.Invoke(S.LoadFailed, new AggregateException(exceptions), false);
            }

            _openFilesLzQueue.Clear();
            await Task.Delay(100);
        }

        public Task OpenNewFiles(IEnumerable<string> filePaths)
        {
            var firstPath = filePaths.FirstOrDefault();

            var fileItemPaths = Presenter.Album.Select(i => i.InputInfo.FullName);
            filePaths = [..filePaths.Where(fp => fp == firstPath || !fileItemPaths.Any(fip => fip == fp))];

            var firstItem = Presenter.Album.FirstOrDefault(i => i.InputInfo.FullName == firstPath);
            firstItem.Let(i => DispatcherQueue.TryEnqueue(() => SelectItem(i, true)));

            return AddFiles(filePaths,
                s => DispatcherQueue.TryEnqueue(() => Status.SetAndNotify(s)),
                (items, _, _) => DispatcherQueue.TryEnqueue(() =>
                {
                    Presenter.Album.Add(items);

                    if (firstItem == null)
                    {
                        firstItem = Presenter.Album.FirstOrDefault(i => i.InputInfo.FullName == firstPath);
                        firstItem.Let(i => SelectItem(i, true));
                    }
                }),
                (s, _, _) => DispatcherQueue.TryEnqueue(() => Status.SetAndNotify(s)));
        }

        readonly LzQueue _openFilesLzQueue = LzQueueService.Inst.Spawn();

        public void OpenNewFilesEnqueued(List<string> filePaths) =>
            _openFilesLzQueue.Enqueue(
                new LzTask<List<string>>(filePaths, OpenNewFiles
            ));

        public void Dispose()
        {
            _debounce.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    internal class ViewerPageProxy : BindingProxy<ViewerPage> { }

    public partial class ViewerPackageStorage : PackageStorage<ViewerPackageStorage>, INotifyPropertyChanged
    {
        public ViewerPackageStorage() { }

        public bool IsListVisible
        {
            get => GetValueOrDefault(Name(), true);
            set => Set(Name(), value);
        }

        public bool IsInfoVisible
        {
            get => GetValueOrDefault(Name(), true);
            set => Set(Name(), value);
        }

        //

        //public const Presenter.Viewer.CensorType DefaultCensorType = Presenter.Viewer.CensorType.Blur;

        public const float MinimumCensorBlurAmount = 24;
        public const float MaximumCensorBlurAmount = 48;
        public const float DefaultCensorBlurAmount = MinimumCensorBlurAmount;

        public static readonly Color DefaultCensorColor = ResourceHelper.GetColorOrDefault("ThemeColor");

        public const float MinimumViewRadius = 16;
        public const float MaximumViewRadius = 48;
        public const float DefaultViewRadius = MinimumViewRadius;

        public bool IsCensored
        {
            get => GetValueOrDefault(Name(), false);
            set => Set(Name(), value);
        }

        //public ViewerEx.CensorType CensorType
        //{
        //    get => GetValueOrDefault(Name(), DefaultCensorType);
        //    set => Set(Name(), value);
        //}

        public double CensorBlurAmount
        {
            get => GetValueOrDefault(Name(), DefaultCensorBlurAmount);
            set => Set(Name(), value);
        }

        public Color CensorColor
        {
            get => GetValueOrDefault(Name(), DefaultCensorColor);
            set => Set(Name(), value);
        }

        public double ViewRadius
        {
            get => GetValueOrDefault(Name(), DefaultViewRadius);
            set => Set(Name(), value);
        }
    }
}