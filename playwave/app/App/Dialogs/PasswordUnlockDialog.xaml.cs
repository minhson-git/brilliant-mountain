using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOCore;
using IOCore.Annotation;
using IOCore.Dialogs;
using IOCore.Gens;
using System;

namespace IOApp.Dialogs;

[NotifyPropertyChanged]
internal sealed partial class PasswordUnlockDialog : DialogEx
{
    public static bool UnlockedInThisSession;

    public string Message { get; set => SetAndNotify(ref field, value); } = string.Empty;

    readonly Action _success;

    public PasswordUnlockDialog(WindowEx windowEx, Action success) : base(windowEx)
    {
        InitializeComponent();

        _success = success;

        CloseButton.IsEnabled = false;
        CloseButton.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    [RelayCommand]
    void Unlock()
    {
        if (PrivatePasswordBox.Password == AppData.I.PrivateItemsLockedPassword)
        {
            UnlockedInThisSession = true;
            _success?.Invoke();

            CloseCommand.Execute(null);
        }
        else Message = T.IncorrectPassword;
    }
}