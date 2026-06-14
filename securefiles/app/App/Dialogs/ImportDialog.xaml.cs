using IOCore;
using IOCore.Dialogs;
using IOCore.Files;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using static IOApp.Features.Share;

namespace IOApp.Dialogs
{
    internal sealed partial class ImportDialog : DialogEx
    {
        public IOStatus<ImportDialog, S> Status { get; }

        FileItem? _fileItem;
        public FileItem? FileItem { get => _fileItem; set => SetAndNotify(ref _fileItem, value); }

        public ImportDialog(WindowEx windowEx) : base(windowEx)
        {
            InitializeComponent();

            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) => {
                Window.SetBusy(Status.IsBusy);

                EnableOutputControls(Status.IsBusy);
                //Inst.FileItem.IsEnabled = Status.IsBusy;
            };

            InitAllControls();
            MakeDefaultAllConfigs();
        }

        public void Init(string filePath)
        {
            try
            {
                if (FileUtils.IsFile(filePath))
                {
                    //FileItem = new FileItem(filePath);
                    //FileItem.LoadBasicInfo();
                }
            }
            catch { }
        }

        async void OutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var storageFolder = await Window.Picker.OpenSingleFolder();

            //if (storageFolder is not null)
            //    OutputFolderPathTextBox.Text = storageFolder.Path;
        }

        void RevealInFileExplorer_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string tag) return;
            SystemUtils.RevealInFileExplorer(tag);
        }

        void ProcessOneButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fileItem is null) return;
            Status.SetAndNotify(S.Processing);

            //      _argv = PrepareArgv();

            //      IProgress<Tuple<StatusType, Exception>> processProgress = new Progress<Tuple<StatusType, Exception>>(progressItem =>
            //      {
            //          Status = progressItem.Item1;
            //          var ex = progressItem.Item2;

            //          if (_status == StatusType.Processed)
            //          {
            //              if (_argv.DeleteInputFiles)
            //                  ZUtils.Delete(_fileItem.InputFileOrFolderPath);

            //              if (AppX.Window() is MainWindow)
            //              {
            //                  var mainItem = Main.Inst.FileItems.FirstOrDefault(i => i.InputFileOrFolderPath == _fileItem.InputFileOrFolderPath);

            //                  if (mainItem is not null)
            //                  {
            //                      mainItem.OutputFileOrFolderPath = new(_fileItem.OutputFileOrFolderPath);
            //                      mainItem.MoveOutputPathToInputPath();
            //                      Main.Inst.Save();
            //                  }
            //              }

            //              _fileItem.MoveOutputPathToInputPath();

            //              AppX.Window().ShowMessageTeachingTip(null, R.T(L.Features_ProcessCompletedSuccessfully), R.T(L.Features_AllInfoWillBeUpdated), () =>
            //              {
            //EncodePasswordPasswordBox.Password = ConfirmEncodePasswordPasswordBox.Password = string.Empty;
            //                  DecodePasswordPasswordBox.Password = string.Empty;

            //                  if (AppX.Window() is MainWindow)
            //                      Hide();

            //                  _ = AskForRate.Inst.Request(true, AskForRate.TimeTest, true, 2);
            //              });
            //          }
            //          else if (_status == StatusType.ProcessFailed)
            //          {
            //              if (AppX.Window() is MainWindow)
            //              {
            //                  var mainItem = Main.Inst.FileItems.FirstOrDefault(i => i.InputFileOrFolderPath == _fileItem.InputFileOrFolderPath);

            //                  if (mainItem is not null)
            //                      Main.Inst.Save();
            //              }

            //              if (ex is PasswordRequiredException)
            //                  AppX.Window().ShowMessageTeachingTip(null, R.T(L.PasswordIsNotEmpty), null, () =>
            //                  {
            //                      AdvancedSettingToggleButton.IsChecked = true;
            //                      DecodePasswordPasswordBox.Focus(FocusState.Programmatic);
            //                  });
            //              else if (ex is IncorrectPasswordException)
            //                  AppX.Window().ShowMessageTeachingTip(null, R.T(L.IncorrectPassword), null, () =>
            //                  {
            //                      AdvancedSettingToggleButton.IsChecked = true;
            //                      DecodePasswordPasswordBox.Focus(FocusState.Programmatic);
            //                  });
            //              else if (ex is PasswordAndConfirmPasswordNotMatchException)
            //                  AppX.Window().ShowMessageTeachingTip(null, R.T(L.PasswordAndConfirmationPasswordDoNotMatch), null, () =>
            //                  {
            //                      AdvancedSettingToggleButton.IsChecked = true;
            //                  });
            //              else if (ex is IOException)
            //              {
            //                  _fileItem.LoadBasicInfo();

            //                  AppX.Window().ShowMessageTeachingTip(null, R.T(L.FileOrFolderDoesNotExist), null, () =>
            //                  {
            //                      EncodePasswordPasswordBox.Password = ConfirmEncodePasswordPasswordBox.Password = string.Empty;
            //                      DecodePasswordPasswordBox.Password = string.Empty;

            //                      //Hide();
            //                  });
            //              }
            //              else
            //                  AppX.Window().ShowMessageTeachingTip(null, R.T(L.UnknownError));
            //          }
            //      });

            //      _ = Task.Run(() =>
            //      {
            //          try
            //          {
            //              processProgress.Report(Tuple.Create(StatusType.Processing, new Exception(string.Empty)));

            //              if (_fileItem.FileType == FileSystemItem.Type.Normal)
            //                  Share.EncodeOne(_fileItem, _argv);
            //              else
            //                  Share.DecodeOne(_fileItem, _argv);

            //              processProgress.Report(Tuple.Create(StatusType.Processed, new Exception(string.Empty)));
            //          }
            //          catch (Exception ex)
            //          {
            //              processProgress.Report(Tuple.Create(StatusType.ProcessFailed, ex));
            //          }
            //      });
        }

        void OutputSameFolderInputCheckBox_Click(object sender, RoutedEventArgs e)
        {
            //if (sender is not CheckBox checkBox) return;
            //OutputFolderButton.IsEnabled = !checkBox.IsChecked.GetValueOrDefault(false);
        }

        void EnableOutputControls(bool isEnabled)
        {
            //EncodePasswordPasswordBox.IsEnabled = ConfirmEncodePasswordPasswordBox.IsEnabled = isEnabled;
            //DecodePasswordPasswordBox.IsEnabled = isEnabled;
            //AdvancedSettingToggleButton.IsEnabled = isEnabled;
            //OutputSameFolderInputCheckBox.IsEnabled = isEnabled;
            //OutputFolderButton.IsEnabled = isEnabled && !OutputSameFolderInputCheckBox.IsChecked.GetValueOrDefault(false);
            //DeleteInputFilesCheckBox.IsEnabled = isEnabled;
            //MakeBaseFolderIfNeededCheckBox.IsEnabled = isEnabled;
            //OverwriteExistingOutputFilesCheckBox.IsEnabled = isEnabled;
        }

        void InitAllControls()
        {
            //OutputFolderPathTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        void MakeDefaultAllConfigs()
        {
            //EncodePasswordPasswordBox.Password = ConfirmEncodePasswordPasswordBox.Password = string.Empty;
            //DecodePasswordPasswordBox.Password = string.Empty;

            //OutputSameFolderInputCheckBox.IsChecked = true;
            //DeleteInputFilesCheckBox.IsChecked = true;
            //OverwriteExistingOutputFilesCheckBox.IsChecked = false;
            //MakeBaseFolderIfNeededCheckBox.IsChecked = true;

            //OuputExtensionComboBox.SelectedIndex = 0;

            //OutputFolderPathTextBox.IsEnabled = !OutputSameFolderInputCheckBox.IsChecked.GetValueOrDefault(false);
        }

        void HideOrClose()
        {
            //if (AppX.Window() is MainWindow)
            //    Hide();
            //else if (AppX.Window() is ServiceWindow)
            //    Application.Current.Exit();
        }

        void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideOrClose();
        }
    }
}