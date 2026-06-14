using CommunityToolkit.Mvvm.Input;
using IOApp.Features;
using IOCore;
using IOCore.Annotation;
using IOCore.Collections;
using IOCore.Dialogs;
using XMedia.Media.PlayerBase;

namespace IOApp.Dialogs;

[BindingProxy]
[NotifyPropertyChanged]
internal partial class DriveDialog : DialogEx
{
    public DriveItem DriveItem { get; private set => SetAndNotify(ref field, value); }
    public ObservableCollectionEx<PlayerItem> PlayerItems { get; } = [];

    public DriveDialog(WindowEx windowEx, DriveItem item) : base(windowEx)
    {
        InitializeComponent();

        DriveItem = item;

        PlayerItems.ReplaceRange(item.PlayerItems);
    }

    [RelayCommand]
    void Play(PlayerItem item)
    {
        PlayerEx.I.Open(item, [.. PlayerItems]);
        CloseCommand.Execute(null);
    }
}