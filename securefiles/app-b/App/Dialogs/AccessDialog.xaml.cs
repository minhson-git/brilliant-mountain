using IOApp.Configs;
using IOApp.Features;
using IOCore;
using IOCore.Base;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Libs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AppLifecycle;
using Windows.System;

namespace IOApp.Popups
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

        void Login()
        {
            if (PasswordBox.Password.Trim() == AppLocalStorage.Password)
                CloseCommand.Execute(null);
            else
                ErrorText = R.T(L.IncorrectPassword);
        }

        void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string tag) return;

            if (tag == "Login")
                Login();
            if (tag == "LearnMore")
                _ = Launcher.LaunchUriAsync(new(Constants.URL_IO_HOW_TO_USE_FAQ));
            else if (tag == "ResetApplication")
                Window.Tip.Confirm(sender, R.T(L.Features_ResetApplication), R.T(L.Features_ResetMessage),
                    () =>
                    {
                        FileUtils.Delete(AppPackageStorage.AppDir);
                        AppPackageStorage.Remove(nameof(AppPackageStorage.AppDir));

                        AppInstance.Restart(string.Empty);
                    });
        }

        void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (args.KeyboardAccelerator.Key == VirtualKey.Enter)
                Login();
        }
    }
}