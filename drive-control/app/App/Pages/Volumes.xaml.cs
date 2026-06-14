using CommunityToolkit.Mvvm.Input;
using IOApp.Configs;
using IOApp.Dialogs;
using IOApp.Features;
using IOCore;
using IOCore.Core;
using IOCore.Exs;
using IOCore.UI;
using IOWMI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Linq;
using ZAnnotation.IOCore;
using static IOApp.Features.Share;

namespace IOApp.Pages
{
    [BindingProxy]
    internal partial class Volumes : MainWindowPage
    {
        public IOStatus<Volumes, S> Status { get; }

        public List<VolumeItem> VOLUMEITEMS { get; } = [];
        public ObservableCollectionEx<VolumeItem> VolumeItems { get; } = [];

        public VolumeItem? SelectedVolumeItem
        {
            get;
            set
            {
                var oldField = field;

                if (SetAndNotify(ref field, value ?? VolumeItems.FirstOrDefault()))
                {
                    oldField?.IsSelected = false;
                    field?.IsSelected = true;
                }
            }
        }

        public MenuFlyout SecureFilterMenuFlyout { get; } = new() { Placement = FlyoutPlacementMode.Bottom };
        public AppTypes.SecureFilter SecureFilter => SecureFilterMenuFlyout.GetCheckedRadioMenuFlyoutItemTagValue<AppTypes.SecureFilter>();
        public string SecureFilterIcon => AppTypes.SECURE_FILTERS[SecureFilter].Item1;

        public Volumes()
        {
            InitializeComponent();
            DataContext = this;

            Status = new(this);
            Status.PropertyChanged += (_, _) => Window.SetBusy(Status.IsBusy);

            //

            foreach (var i in AppTypes.SECURE_FILTERS)
            {
                var item = new RadioMenuFlyoutItem()
                {
                    GroupName = nameof(SecureFilterMenuFlyout),
                    Tag = i.Key,
                    Icon = new FontIcon() { Glyph = i.Value.Item1 },
                    Text = R.T(i.Value.Item2),
                    IsChecked = i.Key is AppTypes.SecureFilter.All
                };

                item.Click += (_, _) =>
                {
                    Notify(nameof(SecureFilterIcon));
                    ApplyFilter();
                };

                Notify(nameof(SecureFilterIcon));

                SecureFilterMenuFlyout.Items.Add(item);
            }

            ApplyFilter();
        }

        protected override void OnNavigatedToEx(NavigationEventArgs e)
        {
            Window.Cover.ShowLoading(Status.IsLoading);
            Window.SetBusy(Status.IsBusy);
        }

        public void ApplyFilter()
        {
            DriveManager.Inst.Execute(Update);

            var items = SecureFilter switch
            {
                AppTypes.SecureFilter.Unsecured => VOLUMEITEMS.Where(i => i.SecureType is SecureType.Unsecured),
                AppTypes.SecureFilter.Secured => VOLUMEITEMS.Where(i => i.SecureType is not SecureType.Unsecured),
                _ => VOLUMEITEMS,
            };

            VolumeItems.Replace(items);
        }

        public void Update(DriveData data)
        {
            var newVolumeItems = data.Volumes.Select(volume => new VolumeItem(volume)).ToList();

            var removingCount = 0;

            foreach (var oldItem in VOLUMEITEMS)
            {
                if (!newVolumeItems.Any(newItem => oldItem.DeviceID == newItem.DeviceID && oldItem.Name == newItem.Name))
                    removingCount++;
            }

            var addingItems = newVolumeItems.Where(newItem => !VOLUMEITEMS.Any(oldItem => oldItem.Name == newItem.Name)).ToList();

            if (addingItems.Count > 0)
                addingItems.ForEach(i => VOLUMEITEMS.Add(i));

            var updatingCount = VOLUMEITEMS.Update(newVolumeItems,
                (oldItem, newItem) => oldItem.DeviceID == newItem.DeviceID && oldItem.Name == newItem.Name,
                (oldItem, newItem) => oldItem.Refresh(newItem.ManagementObject, newItem.DiskCaption, newItem.SerialNumber, newItem.DeviceID));

            if (removingCount > 0 || addingItems.Count > 0 || updatingCount > 0)
            {
                if (removingCount > 0 || addingItems.Count > 0)
                    ApplyFilter();
            }

            if (addingItems.Count > 0)
                Window.Activate();
        }

        [RelayCommand]
        void ShowCleanVolume()
        {
            if (SelectedVolumeItem is null || VolumeItems.IsEmpty)
                return;

            _ = Window.Dialog.Open(new CleanVolumeDialog(Window, SelectedVolumeItem, VolumeItems));
        }

        [RelayCommand]
        void ShowCopyFromPcToVolume()
        {
            if (SelectedVolumeItem is null || VolumeItems.IsEmpty)
                return;

            _ = Window.Dialog.Open(new CopyFromPcToVolumeDialog(Window, SelectedVolumeItem, VolumeItems));
        }

        [RelayCommand]
        void ShowCopyFromVolumeToPc()
        {
            if (SelectedVolumeItem is null || VolumeItems.IsEmpty)
                return;

            _ = Window.Dialog.Open(new CopyFromVolumeToPcDialog(Window, SelectedVolumeItem, VolumeItems));
        }

        [RelayCommand]
        void ShowSecureVolume()
        {
            if (SelectedVolumeItem is null || VolumeItems.IsEmpty)
                return;

            _ = Window.Dialog.Open(new SecureVolumeDialog(Window, SelectedVolumeItem, VolumeItems));
        }

        [RelayCommand]
        void ShowVolumeSettings()
        {
            if (SelectedVolumeItem is null || VolumeItems.IsEmpty)
                return;

            _ = Window.Dialog.Open(new SettingUSBDialog(Window, SelectedVolumeItem));
        }

        [RelayCommand]
        void ShowSyncVolume()
        {
            if (SelectedVolumeItem is null || VolumeItems.IsEmpty)
                return;

            var volumes = VolumeItems.Where(i => i != SelectedVolumeItem);

            _ = Window.Dialog.Open(new SyncVolumeDialog(Window, SelectedVolumeItem, volumes));
        }
    }
}