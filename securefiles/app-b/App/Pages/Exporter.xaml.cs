using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
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
        public IOStatus<Exporter, S> Status { get; }

        public AppLocalStorage AppLocalStorage { get; } = EncapsulatedSingleton<AppLocalStorage>.ExposeInstance();

        ulong _processTimestamp = 0UL;
        readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

        string _lastRun = "00:00:00";
        public string LastRun { get => _lastRun; set => SetAndNotify(ref _lastRun, value); }

        string _timeRun = "00:00:00";
        public string TimeRun { get => _timeRun; set => SetAndNotify(ref _timeRun, value); }

        public ObservableCollectionEx<SwitchItem<AppTypes.ExportType, string>> ExportOptions { get; private set; } = [];
        public ObservableCollectionEx<PrivateFileItem> FileItems { get; private set; } = [];

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

        protected override void OnNavigatedToEx(Frame sender, object? parameter, NavigationEventArgs e)
        {
            if (e.Parameter is IEnumerable<PrivateFileItem> items)
            {
                FileItems.Replace(items);
                ExportButton.IsEnabled = true;
            }
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

            OverwriteExistingOutputFilesCheckBox.IsChecked = false;
            OriginalOutputCheckBox.IsChecked = true;

            OutputFolderPathTextBox.IsEnabled = false;
            OutputFolderButton.IsEnabled = false;

            Status.SetAndNotify(S.Ready);
        }

        async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (Status.Any(S.Loading)) return;
            if ((sender as FrameworkElement)?.Tag is not string tag) return;
            if ((sender as FrameworkElement)?.DataContext is not PrivateFileItem item) return;

            if (tag == "RevealInExplorer")
                SystemUtils.RevealInFileExplorer(item.ExportedFilePath);
            //else if (tag == "Properties")
            //    await new PropertiesPopup(item, true).ShowDialog();
            else if (tag == "Remove")
                FileItems.Remove(item);
        }

        async void OutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var storageFolder = await Window.Picker.OpenSingleFolder();

            if (storageFolder != null)
                OutputFolderPathTextBox.Text = storageFolder.Path;
        }

        Argv PrepareArgv()
        {
            return new()
            {
                ExportToOriginalFolder = OriginalOutputCheckBox.IsChecked.GetValueOrDefault(false),
                OutputFolderPath = OutputFolderPathTextBox.Text,
                OverwriteExistingOutputFiles = OverwriteExistingOutputFilesCheckBox.IsChecked.GetValueOrDefault(false),
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

            IProgress<S> endProgress = new Progress<S>(s =>
            {
                Status.SetAndNotify(s);

                ExportButton.IsEnabled = FileItems.Any(i => i.Status.Not(FileItem.S.Processed));

                if (Status.Any(S.Processed))
                {
                    Window.Tip.Message(null, R.T(L.Features_ProcessCompletedSuccessfully), R.T(L.Features_AllInfoWillBeUpdated), () =>
                    {
                        _ = Ask.Inst.ToRate(Window, null, 1, true, TimeSpan.FromDays(2));
                    });
                }
                else if (Status.Any(S.ProcessFailed))
                    Window.Tip.Message(null, R.T(L.UnknownError));
            });

            IProgress<Tuple<PrivateFileItem, FileItem.S, Exception>> itemProgress = new Progress<Tuple<PrivateFileItem, FileItem.S, Exception>>(result =>
            {
                lock (locker)
                {
                    var item = result.Item1;
                    item.Status.SetAndNotify(result.Item2);

                    if (item.Status.Any(FileItem.S.Processed))
                    {
                        item.MessageText = R.T(L.Status_Processed);

                        if (argv.ExportType == AppTypes.ExportType.Export)
                        {
                            Home.Inst.SOURCE_FILE_ITEMS.Remove(item);
                            Home.Inst.FileItems.Remove(item);
                        }
                    }
                    else if (item.Status.Any(FileItem.S.ProcessFailed))
                    {
                        if (result.Item3 is IOException)
                            item.MessageText = R.T(L.FileOrFolderDoesNotExist);
                        else
                            item.MessageText = string.IsNullOrWhiteSpace(result.Item3.Message) ? R.T(L.UnknownError) : result.Item3.Message;
                    }

                    item.NotifyExportedFilePath();
                }
            });

            return Task.Run(() =>
            {
                try
                {
                    foreach (var item in FileItems)
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(item.ExportedFilePath))
                                throw new Exception(R.T(L.Exception_ItemAlreadyExported));

                            if (argv.ExportToOriginalFolder)
                                argv.OutputFolderPath = Path.GetDirectoryName(item.Footer.Metadata.OriginalName);

                            itemProgress.Report(Tuple.Create(item, FileItem.S.Processing, new Exception()));

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
                                CypherFileItem.DecodeOne(item.InputInfo.FullName, item.RecoveredFileOrFolderPath, AppLocalStorage.Password, true);

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

                            itemProgress.Report(Tuple.Create<PrivateFileItem, FileItem.S, Exception>(item, FileItem.S.Processed, null));
                        }
                        catch (Exception ex)
                        {
                            itemProgress.Report(Tuple.Create(item, FileItem.S.ProcessFailed, ex));
                        }
                    }

                    endProgress.Report(S.Processed);
                }
                catch
                {
                    endProgress.Report(S.ProcessFailed);
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

        void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            _processTimestamp = 0UL;

            LastRun = DateTime.Now.ToString(@"hh\:mm\:ss");
            TimeRun = TimeSpan.FromSeconds(_processTimestamp).ToString(@"hh\:mm\:ss");

            Export();
        }

        void OriginalOutputCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox) return;

            OutputFolderPathTextBox.IsEnabled = !checkBox.IsChecked.GetValueOrDefault(false);
            OutputFolderButton.IsEnabled = !checkBox.IsChecked.GetValueOrDefault(false);
        }

        void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Window.TryGoBackCommand.Execute(typeof(Home));
        }
    }
}