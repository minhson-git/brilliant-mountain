using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Dialogs;
using IOCore.Exs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static IOApp.Features.Share;

namespace IOApp.Pages
{
    internal partial class Exporter : MainWindowPage
    {
        public AppLocalStorage AppLocalStorage { get; } = EncapsulatedSingleton<AppLocalStorage>.ExposeInstance();

        public SecureContext SecureContext { get; } = SecureContext.Inst;

        public IOStatus<Exporter, S> Status { get; }

        ulong _processTimestamp = 0UL;
        readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

        string _lastRun = "00:00:00";
        public string LastRun { get => _lastRun; set => SetAndNotify(ref _lastRun, value); }

        string _timeRun = "00:00:00";
        public string TimeRun { get => _timeRun; set => SetAndNotify(ref _timeRun, value); }

        public ObservableCollectionEx<SwitchItem<AppTypes.ExportType, string>> ExportOptions { get; private set; } = [];
        public ObservableCollectionEx<SecureFileItem> FileItems { get; private set; } = [];

        string _outputFolderPath = "";
        public string OutputFolderPath { get => _outputFolderPath; set => SetAndNotify(ref _outputFolderPath, value); }

        bool _overwrite = false;
        public bool Overwrite { get => _overwrite; set => SetAndNotify(ref _overwrite, value); }

        bool _exportToOriginal = true;
        public bool ExportToOriginal { get => _exportToOriginal; set => SetAndNotify(ref _exportToOriginal, value); }

        public Exporter()
        {
            InitializeComponent();
            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) =>
            {
                Window.SetBusy(Status.IsBusy);

                if (Status.Any(S.Processing))
                    _timer.Start();
                else
                    _timer.Stop();
            };

            InitAllControls();
        }

        protected override void OnNavigatedToEx(NavigationEventArgs e)
        {
            if (e.Parameter is IList<SecureFileItem> items)
                FileItems.Replace(items);
        }

        void InitAllControls()
        {
            _timer.Tick += (sender, e) =>
            {
                _processTimestamp++;
                TimeRun = TimeSpan.FromSeconds(_processTimestamp).ToString(@"hh\:mm\:ss");
            };

            foreach (var i in AppTypes.EXPORTS)
                ExportOptions.Add(new OptionItem<AppTypes.ExportType, string>(i.Key, R.T(i.Value), false, true));
            ExportOptions[0].IsOn = true;

            Status.SetAndNotify(S.Ready);
        }

        async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (Status.Any(S.Loading)) return;
            if ((sender as FrameworkElement)?.Tag is not string tag) return;
            if ((sender as FrameworkElement)?.DataContext is not SecureFileItem item) return;

            if (tag == "RevealInExplorer")
                SystemUtils.RevealInFileExplorer(item.ExportedFilePath);
            else if (tag == "Properties")
                await Window.Dialog.Open(new PropertiesDialog(Window, item.InputInfo.Info));
            else if (tag == "Remove")
                FileItems.Remove(item);
        }

        [RelayCommand]
        void ToggleOverwrite() => Overwrite = !Overwrite;

        [RelayCommand]
        void UpdateOutputFolder() => UpdateOutputFolderPath();

        async void UpdateOutputFolderPath()
        {
            var storageFolder = await Window.Picker.OpenSingleFolder();

            if (storageFolder is not null)
                OutputFolderPath = storageFolder.Path;
        }

        Argv PrepareArgv()
        {
            return new()
            {
                ExportToOriginalFolder = _exportToOriginal,
                OutputFolderPath = OutputFolderPath,
                OverwriteExistingOutputFiles = _overwrite,
                ExportType = ExportOptions.FirstOrDefault(i => i.IsOn)?.Key ?? AppTypes.ExportType.Export,
            };
        }

        public Task Export()
        {
            var argv = PrepareArgv();

            if (!argv.ExportToOriginalFolder && string.IsNullOrWhiteSpace(argv.OutputFolderPath))
            {
                Window.Tip.Message(null, R.T(L.OutputFolderMustBeSet));
                return Task.CompletedTask;
            }

            var locker = new object();

            Status.SetAndNotify(S.Processing);

            foreach (var i in FileItems)
                i.Status.SetAndNotify(FileItem.S.ProcessInQueue);

            //

            void ui(S s)
            {
                Status.SetAndNotify(s);

                if (Status.Any(S.Processed))
                {
                    Window.Tip.Message(null, R.T(L.Features_ProcessCompletedSuccessfully), R.T(L.Features_AllInfoWillBeUpdated), () =>
                    {
                        _ = Ask.Inst.ToRate(Window, null, 1, true, TimeSpan.FromDays(2));
                    });
                }
                else if (Status.Any(S.ProcessFailed))
                    Window.Tip.Message(null, R.T(L.UnknownError));
            }

            void itemUi(SecureFileItem item, FileItem.S s, Exception? ex)
            {
                lock (locker)
                {
                    item.Status.SetAndNotify(s);

                    if (item.Status.Any(FileItem.S.Processed))
                    {
                        item.MessageText = R.T(L.Status_Processed);

                        if (argv.ExportType == AppTypes.ExportType.Export)
                            SecureContext.SECURE_ITEMS.Remove(item);
                    }
                    else if (item.Status.Any(FileItem.S.ProcessFailed))
                    {
                        if (ex is IOException)
                            item.MessageText = R.T(L.FileOrFolderDoesNotExist);
                        else if (ex is not null)
                            item.MessageText = string.IsNullOrWhiteSpace(ex.Message) ? R.T(L.UnknownError) : ex.Message;
                    }

                    item.Notify(nameof(item.ExportedFilePath));
                }
            }

            return Task.Run(async () =>
            {
                try
                {
                    foreach (var item in FileItems)
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(item.ExportedFilePath))
                                throw new Exception(R.T(L.Exception_ItemAlreadyExported));

                            if (item.Footer is null || item.Footer.Metadata is null)
                                throw new Exception("Corrupted item.");

                            var outputDirPath = Path.GetDirectoryName(item.Footer.Metadata.OriginalName) ?? throw new Exception("Destination folder path does not exist.");

                            if (argv.ExportToOriginalFolder)
                                argv.OutputFolderPath = outputDirPath;

                            DispatcherQueue.TryEnqueue(() => itemUi(item, FileItem.S.Processing, new Exception()));

                            var outputFileOrFolderPath = Path.Combine(argv.OutputFolderPath, Path.GetFileName(item.Footer.Metadata.OriginalName));

                            if (!argv.OverwriteExistingOutputFiles)
                            {
                                if (item.FileType == ZFile.FileType.Directory)
                                    outputFileOrFolderPath = PathUtils.NextAvailablePath(outputFileOrFolderPath);
                                else
                                    outputFileOrFolderPath = PathUtils.NextAvailablePath(outputFileOrFolderPath);
                            }

                            if (argv.ExportType == AppTypes.ExportType.ExportLockedCopy)
                                File.Copy(item.EncryptedInfo.FullName, outputFileOrFolderPath, argv.OverwriteExistingOutputFiles);
                            else
                            {
                                item.RecoveredFileOrFolderPath = AppDir.PGetFilePath(AppDir.Type.TemporaryFolder, Guid.NewGuid().ToString());
                                await CypherFileItem.DecodeOne(item.InputInfo.FullName, item.RecoveredFileOrFolderPath, AppLocalStorage.Password, true);

                                FileUtils.CreateDirectoryIfNotExist(Path.GetDirectoryName(outputFileOrFolderPath));

                                if (item.FileType == ZFile.FileType.Directory)
                                {
                                    // @nguyenducphu: Source and destination path must have identical roots. Move will not work across volumes
                                    FileUtils.CopyDirectory(item.RecoveredFileOrFolderPath, outputFileOrFolderPath, true);
                                    FileUtils.Delete(item.RecoveredFileOrFolderPath);
                                }
                                else
                                    File.Move(item.RecoveredFileOrFolderPath, outputFileOrFolderPath, argv.OverwriteExistingOutputFiles);

                                if (argv.ExportType == AppTypes.ExportType.Export)
                                    item.Removed();
                            }

                            item.ExportedFilePath = outputFileOrFolderPath;

                            DispatcherQueue.TryEnqueue(() => itemUi(item, FileItem.S.Processed, null));
                        }
                        catch (Exception ex)
                        {
                            DispatcherQueue.TryEnqueue(() => itemUi(item, FileItem.S.ProcessFailed, ex));
                        }
                    }

                    DispatcherQueue.TryEnqueue(() => ui(S.Processed));
                }
                catch
                {
                    DispatcherQueue.TryEnqueue(() => ui(S.ProcessFailed));
                }
            });
        }

        void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton control) return;
            if (control.GetTagObjectOrDefault() is not AppTypes.ExportType exportType) return;

            ExportOptions.ForEach(i =>
            {
                if (i.Key == exportType)
                    i.IsOn = true;
                else
                    i.IsOn = false;
            });
        }

        [RelayCommand]
        void Process()
        {
            _processTimestamp = 0UL;

            LastRun = DateTime.Now.ToString(@"hh\:mm\:ss");
            TimeRun = TimeSpan.FromSeconds(_processTimestamp).ToString(@"hh\:mm\:ss");

            Export();
        }

        [RelayCommand]
        void ToggleExportToOriginal() => ExportToOriginal = !ExportToOriginal;

        [RelayCommand]
        void CloseExport() => Window.TryGoBackCommand.Execute(null);
    }
}