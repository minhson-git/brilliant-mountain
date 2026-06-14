using IOApp.Configs;
using IOApp.Features;
using IOCore.Base;
using IOCore.Core;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using Windows.ApplicationModel;
using Windows.System;

namespace IOApp.Pages
{
    internal partial class Installer : MainWindowPage
    {
        public AppLocalStorage AppLocalStorage { get; } = EncapsulatedSingleton<AppLocalStorage>.ExposeInstance();
        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();

        public SharedStorage SharedStorage { get; } = EncapsulatedSingleton<SharedStorage>.ExposeInstance();

        public enum InstallStep
        {
            SetAppDir,
            SetPassword,
            Login,
            Done
        }

        public InstallStep InstallProgress = InstallStep.SetAppDir;

        void Evaluate()
        {
            if (AppPackageStorage.Exists(nameof(AppPackageStorage.AppDir)))
            {
                AppDir.InitBase(AppPackageStorage.AppDir);

                InstallProgress = InstallStep.SetPassword;

                if (!string.IsNullOrWhiteSpace(AppLocalStorage.Password))
                    InstallProgress = InstallStep.Login;
            }
        }

        string _errorText = "*";
        public string ErrorText { get => _errorText; set => SetAndNotify(ref _errorText, value); }

        public BitmapImage? Icon { get; } = IM.Inst.Get(IM.Id.Icon);
        public string AppName { get; } = Package.Current.DisplayName;

        public Installer()
        {
            InitializeComponent();
            DataContext = this;

            FileUtils.Delete(AppPackageStorage.OldAppDir);

            Evaluate();
            DisplayPanel();
        }

        void DisplayPanel()
        {
            StorageConfigurationPanel.Visibility = Visibility.Collapsed;
            PasswordConfigurationPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Collapsed;

            if (InstallProgress == InstallStep.SetAppDir)
            {
                StorageConfigurationPanel.Visibility = Visibility.Visible;
                OutputFolderPathTextBox.TextBox.Text = Path.Combine(AppDir.PACKAGE_FOLDER, "AppDir");
            }
            else if (InstallProgress == InstallStep.SetPassword)
                PasswordConfigurationPanel.Visibility = Visibility.Visible;
            else if (InstallProgress == InstallStep.Login)
            {
                SharedStorage.Sync(true);

                FileUtils.CreateDirectoryIfNotExist(AppDir.Get(AppDir.Type.LocalFolder, "Encrypted"));
                FileUtils.CreateDirectoryIfNotExist(AppDir.Get(AppDir.Type.LocalFolder, "Queue"));

                LoginPanel.Visibility = Visibility.Visible;
            }

            ErrorText = "*";
        }

        async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Control control) return;

            if (control == OutputFolderButton)
            {
                var storageFolder = await Window.Picker.OpenSingleFolder();
                if (storageFolder != null)
                {
                    if (FileUtils.IsEmptyDirectory(storageFolder.Path))
                        OutputFolderPathTextBox.TextBox.Text = storageFolder.Path;
                    else
                        Window.Tip.Message(null, R.T(L.Features_SelectEmptyFolderMessage));
                }
            }
            else if (control == SetStorageButton)
            {
                Window.Tip.Confirm(null, R.T(L.Features_ConfirmPathTitle), null, () =>
                {
                    AppPackageStorage.AppDir = OutputFolderPathTextBox.TextBox.Text;
                    AppDir.InitBase(AppPackageStorage.AppDir);

                    InstallProgress = InstallStep.SetPassword;

                    if (!string.IsNullOrWhiteSpace(AppLocalStorage.Password))
                        InstallProgress = InstallStep.Login;

                    DisplayPanel();
                });
            }
            else if (control == SetPasswordButton)
                SetPassword();
            else if (control == LoginButton)
                Login();
            else if (control == ResetApplicationHyperlinkButton)
            {
                Window.Tip.Confirm(sender, R.T(L.Features_ResetApplication), R.T(L.Features_ResetMessage),
                () =>
                {
                    FileUtils.Delete(AppPackageStorage.AppDir);
                    AppPackageStorage.Remove(nameof(AppPackageStorage.AppDir));

                    Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
                });
            }
            else if (control == LearnMoreHyperlinkButton)
                _ = Launcher.LaunchUriAsync(new(Constants.URL_IO_HOW_TO_USE_FAQ));
        }

        void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (args.KeyboardAccelerator.Key == VirtualKey.Enter)
            {
                if (InstallProgress == InstallStep.SetPassword)
                    SetPassword();
                else if (InstallProgress == InstallStep.Login)
                    Login();
            }
        }

        void SetPassword()
        {
            try
            {
                PasswordException.ThrowIfNullOrWhiteSpace(InitialPasswordBox.Password);
                PasswordException.ThrowIfNotMatch(InitialPasswordBox.Password, InitialConfirmedPasswordBox.Password);

                AppLocalStorage.Password = InitialPasswordBox.Password.Trim();
                InstallProgress = InstallStep.Login;

                DisplayPanel();
            }
            catch (Exception ex)
            {
                if (ex is PasswordException passwordEx)
                {
                    if (passwordEx.Kind == PasswordException.ExceptionKind.Required)
                        ErrorText = R.T(L.PasswordIsNotEmpty);
                    else if (passwordEx.Kind == PasswordException.ExceptionKind.NotMatch)
                        ErrorText = R.T(L.PasswordAndConfirmationPasswordDoNotMatch);
                }
            }
        }

        void Login()
        {
            try
            {
                PasswordException.ThrowIfNullOrWhiteSpace(InitialPasswordBox.Password);
                PasswordException.ThrowIfNotCorrect(InitialPasswordBox.Password, AppLocalStorage.Password);

                InstallProgress = InstallStep.Done;
                Window.GoToMain();
            }
            catch (Exception ex)
            {
                if (ex is PasswordException passwordEx)
                {
                    if (passwordEx.Kind == PasswordException.ExceptionKind.Required)
                        ErrorText = R.T(L.PasswordIsNotEmpty);
                    else if (passwordEx.Kind == PasswordException.ExceptionKind.NotCorrect)
                        ErrorText = R.T(L.IncorrectPassword);
                }
            }
        }
    }
}