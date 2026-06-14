using CommunityToolkit.Mvvm.Input;
using IOApp.Windows;
using IOCore.Annotation;
using System.Text.Json.Serialization;

namespace IOApp.Features;

[DataSaver]
public partial class AppData
{
    public partial AppData() { }

    public bool PrivateItemsRestricted { get; set => Set(ref field, value); } = false;
    public int PrivateItemsRestrictedHitCount { get; set { if (PrivateItemsRestricted) Set(ref field, value); } } = 0;
    public bool PrivateItemsForgotten { get; set => Set(ref field, value); } = false;
    public bool PrivateItemsPasswordLocked { get; set { if (Set(ref field, value) && !value) PrivateItemsLockedPassword = string.Empty; } } = false;
    public string PrivateItemsLockedPassword { get; set { if (Set(ref field, value)) SetPasswordCount += 1; } } = string.Empty;
    public bool PrivateItemsEncrypted { get; set => Set(ref field, value); } = false;
    public int SetPasswordCount { get; set => Set(ref field, value); } = 0;
    public bool IsEchoShieldEnabled { get; set => Set(ref field, value); } = false;
    public bool IsGrabberVisible { get; set => Set(ref field, value); } = true;

    public void Reset()
    {
        Silent(() =>
        {
            PrivateItemsRestricted = false;
            PrivateItemsForgotten = false;

            PrivateItemsPasswordLocked = false;
            PrivateItemsLockedPassword = string.Empty;

            PrivateItemsEncrypted = false;

            SetPasswordCount = 0;

            IsEchoShieldEnabled = false;
        }, true, true);

        Save();
        NotifyAll();
    }

    [RelayCommand]
    void ShowGrabber() => IOCore.AppEx.LoadWindow<GrabberWindow>(_ => _.Activate());
}

[JsonSerializable(typeof(AppData))]
public partial class AppDataJsonContext : JsonSerializerContext { }
