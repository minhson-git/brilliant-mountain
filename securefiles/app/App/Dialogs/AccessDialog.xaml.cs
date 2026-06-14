using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Dialogs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AppLifecycle;
using Windows.System;

namespace IOApp.Dialogs
{
    internal sealed partial class AccessDialog : DialogEx
    {
        string _errorText = "*";
        public string ErrorText { get => _errorText; set => SetAndNotify(ref _errorText, value); }

        public static AppLocalStorage AppLocalStorage { get; } = EncapsulatedSingleton<AppLocalStorage>.ExposeInstance();
        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();

        public AccessDialog(WindowEx window) : base(window)
        {
            InitializeComponent();
            DataContext = this;
        }

        void Login(string password)
        {
            if (password == AppLocalStorage.Password)
                CloseCommand.Execute(null);
            else
                ErrorText = R.T(L.IncorrectPassword);
        }

        [RelayCommand]
        void Login() => Login(PasswordBox.Password.Trim());

        [RelayCommand]
        void Reset(object sender)
        {
            Window.Tip.Confirm(sender, R.T(L.Features_ResetApplication), R.T(L.Features_ResetMessage),
                () =>
                {
                    FileUtils.Delete(AppPackageStorage.AppDir);
                    AppPackageStorage.Remove(nameof(AppPackageStorage.AppDir));

                    AppInstance.Restart(string.Empty);
                });
        }

        [RelayCommand]
        void LearnMore() => _ = Launcher.LaunchUriAsync(new(Constants.URL_IO_HOW_TO_USE_FAQ));

        void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (args.KeyboardAccelerator.Key == VirtualKey.Enter)
                Login();
        }
    }
}