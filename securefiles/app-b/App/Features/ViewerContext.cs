using IOCore;
using IOCore.Base;
using IOCore.Dialogs;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Utils;
using IOImage;
using IOImage.Presenter;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;

namespace IOApp.Features
{
    public class MenuContext : Singleton<MenuContext>
    {
        WindowEx? _window;

        public MenuFlyout _menu = new() { Placement = FlyoutPlacementMode.Bottom };

        readonly MenuFlyoutItem _viewItem;
        readonly MenuFlyoutItem _convertItem;
        readonly MenuFlyoutItem _addToAlbumsItem;

        readonly MenuFlyoutItem _removeItem;
        readonly MenuFlyoutItem _deleteItem;

        ViewerItem? _viewerItem;

        public bool UseViewFeature { get; private set; } = false;
        public bool UseAddToAlbumsItem { get; private set; } = false;

        public bool UseRemoveItem { get; private set; } = false;

        Action<ViewerItem>? _openAction;
        Action<ViewerItem>? _removeAction;
        Action<ViewerItem>? _deleteAction;

        MenuContext()
        {
            var item = _viewItem = new MenuFlyoutItem() { Text = R.T(L.View), Icon = new FontIcon() { Glyph = "\uE890" } };
            item.Click += (_, _) =>
            {
                if (_viewerItem != null)
                    _openAction?.Invoke(_viewerItem);
            };
            _menu.Items.Add(item);

            item = new MenuFlyoutItem() { Text = R.T(L.RevealInFileExplorer), Icon = new FontIcon() { Glyph = "\ue8e5" } };
            item.Click += (_, _) => SystemUtils.RevealInFileExplorer(_viewerItem?.InputInfo.FullName);
            _menu.Items.Add(item);

            item = new MenuFlyoutItem() { Text = R.T(L.OpenWithDefaultApp), Icon = new FontIcon() { Glyph = "\ue8e5" } };
            item.Click += (_, _) =>
            {
                if (_viewerItem != null)
                    SystemUtils.OpenFileWithDefaultApp(_viewerItem.InputInfo.FullName);
            };
            _menu.Items.Add(item);

            item = new MenuFlyoutItem() { Text = R.T(L.Properties), Icon = new FontIcon() { Glyph = "\ue946" } };
            item.Click += async (_, _) =>
            {
                if (_viewerItem != null && _window != null)
                {
                    var dialog = new PropertiesDialog(_window);
                    await _window.Dialog.Open(dialog.Load(_viewerItem.InputInfo.Info));
                }    
            };
            _menu.Items.Add(item);

            item = _removeItem = new MenuFlyoutItem() { Text = R.T(L.Remove), Icon = new FontIcon() { Glyph = "\ue711" } };
            item.Click += (_, _) =>
            {
                if (_viewerItem != null)
                    _removeAction?.Invoke(_viewerItem);
            };
            _menu.Items.Add(item);

            item = _deleteItem = new MenuFlyoutItem() { Text = R.T(L.RemovePermanently), Icon = new FontIcon() { Glyph = "\ue711", Foreground = new SolidColorBrush(Colors.Red) } };
            item.Click += (_, _) =>
            {
                if (_viewerItem != null)
                    _deleteAction?.Invoke(_viewerItem);
            };
            _menu.Items.Add(item);
        }

        public void Init(WindowEx window) => _window = window;

        public MenuFlyout Update(
            ViewerItem item, bool useViewFeature, bool useAddToAlbumsItem, bool useRemoveItem,
            Action<ViewerItem>? openAction, Action<ViewerItem>? removeAction, Action<ViewerItem>? deleteAction)
        {
            _viewerItem = item;

            UseViewFeature = useViewFeature;
            UseAddToAlbumsItem = useAddToAlbumsItem;

            UseRemoveItem = useRemoveItem;

            _openAction = openAction;
            _removeAction = removeAction;
            _deleteAction = deleteAction;

            _viewerItem.Visibility = useViewFeature ? Visibility.Visible : Visibility.Collapsed;
            _addToAlbumsItem.Visibility = useAddToAlbumsItem ? Visibility.Visible : Visibility.Collapsed;

            _removeItem.Visibility = _deleteItem.Visibility = useRemoveItem ? Visibility.Visible : Visibility.Collapsed;

            _viewItem.IsEnabled = _convertItem.IsEnabled = _addToAlbumsItem.IsEnabled = !_viewerItem.InputInfo.IsCorrupted;

            return _menu;
        }
    }

    public partial class ViewerItem : PresenterItem
    {
        public AppPackageStorage AppPackageStorage { get; } = EncapsulatedSingleton<AppPackageStorage>.ExposeInstance();

        public ViewerItem(ImageInfoBase inputInfo) : base(inputInfo)
        {
            InputInfo.PropertyChanged += (_, _) =>
                Notify(nameof(AppPackageStorage));
        }

        internal static ViewerItem Create(SecuredFileEntity entity, bool isRecent, bool ignoreError = false)
        {
            if (!FileUtils.IsFile(entity.Path) && !ignoreError)
                throw new FileNotFoundException();

            return new(new ImageInfoBase(entity.Path)) { IsRecent = isRecent, LastOpenedAt = entity.LastOpenedAt };
        }

        public override bool Equals(object? obj) => this == obj;

        public override int GetHashCode() => InputInfo.FullName.GetHashCode();
    }
}