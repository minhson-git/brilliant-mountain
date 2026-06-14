using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOCore;
using IOCore.AppManager;
using IOCore.Exs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.UI;
using IOCore.Utils;
using IOImage;
using IOMedia.Media.ConverterBase;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZAnnotation.IOCore;
using static IOCore.Files.MediaFamily;
using static IOMedia.Media.ConverterBase.ConverterMeta;

namespace IOApp.Pages
{
    [BindingProxy]
    internal partial class Home : MainWindowConverterPage
    {
        public ObservableCollectionEx<ExtractingItem> ExtractingItems { get; } = [];

        public IOImage.ConverterBase.ConverterMeta ImageMeta { get; } = new();
        public ExtractingConvertArgv ExtractingConvertArgv { get; } = new();

        public Home() : base(MediaType.Video) { }

        protected override void OnNavigatedToEx(NavigationEventArgs e)
        {
            base.OnNavigatedToEx(e);

            InitializeComponent();
            DataContext = this;
        }

        protected override void OnLicenseStatusChanged()
        {
            if (Window.License.Status.IsTrial)
                Promotion.Inst.Offer(items =>
                {
                    PromotionAppItems.Replace(items);
                    Notify(nameof(PromotionAppItems));
                }, 1);
        }

        public new async void AddFilesAction(IEnumerable<string>? paths)
        {
            if (Status.S is S.Loading or S.Processing or S.ProcessPaused)
                return;

            if (paths is null)
            {
                var pickFileResults = await Window.Picker.OpenMultipleFiles(picker =>
                {
                    foreach (var i in Meta.Extensions)
                        picker.FileTypeFilter.Add(i);
                });

                if (pickFileResults is null)
                    return;

                paths = [.. pickFileResults.Select(i => i.Path)];
            }

            if (paths is null || !paths.Any())
                return;

            Status.SetAndNotify(S.Loading);

            var exceptions = new ConcurrentBag<Exception>();

            try
            {
                var phases = paths.Phases(null);

                foreach (var (phase, i) in phases.Select((value, i) => (value, i)))
                {
                    foreach (var package in phase)
                    {
                        var items = new ConcurrentBag<ExtractingItem>();

                        foreach (var itemsPerProcess in package.Chunk(Environment.ProcessorCount))
                        {
                            await Parallel.ForEachAsync(itemsPerProcess, (path, ct) =>
                            {
                                try
                                {
                                    if (FileUtils.IsFile(path) && !ExtractingItems.Any(i => i.InputInfo.FullName == path))
                                    {
                                        if (!ConverterConfig.IsInputAccepted(path, Meta.InputType))
                                            throw new UnacceptedInputException();

                                        var item = new ExtractingItem(path);
                                        item.InputInfo.Analyze();

                                        if (!item.InputInfo.IsCorrupted)
                                        {
                                            if (item.InputInfo.Media is not null)
                                            {
                                                var frameRate = int.TryParse(item.InputInfo.Media.FrameRate, out var parsedFrameRate) ? parsedFrameRate : 30;
                                                item.ExtractingConvertArgv.NumberOfFramesToExtract = frameRate;
                                            }

                                            items.Add(item);
                                        }    
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
                        {
                            foreach (var item in items)
                            {
                                item.Status.PropertyChanged += (_, _) =>
                                {
                                    Notify(nameof(AnyItemsProcessing), nameof(ProcessedCount));

                                    Window.SetBusy(AnyItemsProcessing);
                                };

                                item.ExtractingConvertArgv.PropertyChanged += (_, e) =>
                                {
                                    if (e.PropertyName is nameof(Features.ExtractingConvertArgv.FamilyIndex))
                                        item.ExtractingConvertArgv.Build();
                                };

                                item.ExtractingConvertArgv.Build(ImageMeta.FamilyTypes);
                            }
                             
                            var orderItems = items.OrderBy(i => Array.IndexOf(package, i.InputInfo.FullName)).ToList();

                            foreach (var item in orderItems)
                                ExtractingItems.Add(item);
                        }
                    }
                }

                Status.SetAndNotify(S.Loaded);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                Status.SetAndNotify(S.LoadFailed);
            }

            if (!exceptions.IsEmpty)
                Window.Tip.Message(null, string.Empty, R.T(L.LoadCorruptedSomeFiles));
        }

        [RelayCommand]
        void AddVideos() => AddFilesAction(null);

        [RelayCommand]
        void ClearVideos(object sender) => Window.Tip.Confirm(sender, R.T(L.ClearItemList), null, ExtractingItems.Clear);

        [RelayCommand]
        void MakeDefaultAdvancedConfigs(object sender)
        {
            if (AppEx.FindWindow<MainWindow>() is not MainWindow window)
                return;

            if (sender.GetDataContext<ExtractingItem>() is not ExtractingItem item)
                return;

            window.Tip.Confirm(sender, R.T(L.Features_ResetAdvancedSettingsToDefault), null, () => item.ExtractingConvertArgv.Reset(null));
        }

        async Task ExtractAllAction()
        {
            if (ExtractingItems is null || ExtractingItems.Count == 0)
                return;

            Status.SetAndNotify(S.Processing);

            FileUtils.CreateDirectoryIfNotExist(MediaData.OutputPath);

            int successCount = 0;
            int failCount = 0;

            foreach (var item in ExtractingItems)
            {
                await item.Process(MediaData.OutputPath);

                if (item.Status.IsProcessed)
                    successCount++;
                else
                    failCount++;
            }

            var isSuccess = failCount == 0 && successCount > 0;
            
            Status.SetAndNotify(isSuccess ? S.Processed : S.ProcessFailed);
        }

        [RelayCommand]
        void ExtractOne(ExtractingItem item) => _ = item.Process(MediaData.OutputPath);

        [RelayCommand]
        void ExtractAll() => _ = ExtractAllAction();

        [RelayCommand]
        void CancelOne(ExtractingItem item) => item.Cancel();

        [RelayCommand]
        void CancelAll()
        {
            foreach (var item in ExtractingItems)
                item.Cancel();

            Status.SetAndNotify(S.ProcessStopped);
        }

        [RelayCommand]
        void ReplaceExtractItem(ExtractingItem item)
        {
            _ = Window.Picker.OpenSingleFile(
                picker =>
                {
                    foreach (var i in Meta.Extensions)
                        picker.FileTypeFilter.Add(i);
                },
                pickFileResult =>
                {
                    if (pickFileResult.Path != item.InputInfo.FullName)
                    {
                        var existingItem = ExtractingItems.FirstOrDefault(i => i.InputInfo.FullName == pickFileResult.Path);
                        if (existingItem is not null)
                        {
                            ExtractingItems.Remove(existingItem);
                            ExtractingItems.Set(item, existingItem);
                        }
                        else
                        {
                            var newItem = new ExtractingItem(pickFileResult.Path);
                            newItem.InputInfo.Analyze();

                            if (newItem.InputInfo.IsCorrupted)
                                Window.Tip.Message(null, string.Empty, R.T(L.LoadCorruptedSomeFiles));
                            else
                            {
                                newItem.ConvertArgv.Build(Meta.FamilyTypes);
                                ExtractingItems.Set(item, newItem);
                            }
                        }
                    }
                }
            );
        }

        [RelayCommand]
        void ApplyToAllExtractItem(object sender) =>
            sender.LetDataContext<ExtractingItem>(item =>
                Window.Let(window => window.Tip.Confirm(sender, R.T(L.Features_ApplyThisSettingToAllItems), null, () =>
                {
                    foreach (var i in ExtractingItems)
                        i.ExtractingConvertArgv.CopyExtractingConvertArgv(item.ExtractingConvertArgv);
                }))
        );

        [RelayCommand]
        void RemoveOneExtractItem(ExtractingItem item) => ExtractingItems.Remove(item);

        [RelayCommand]
        void ClearExtractItemsAll(object sender) => Window.Tip.Confirm(sender, R.T(L.ClearItemList), null, ExtractingItems.Clear);
    }
}