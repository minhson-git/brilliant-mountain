using IOApp.Pages;
using IOCore;
using IOCore.Files;
using IOCore.Libs;
using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IOApp.Features
{
    internal partial class FileControl : UserControlEx
    {
        static readonly Type _type = typeof(FileControl);

        public FileControl()
        {
            InitializeComponent();
        }

        //

        public delegate void EventHandler(object sender, EventArgs e);

        public event EventHandler OnViewOrPlay;
        public event EventHandler OnCensor;
        public event RoutedEventHandler OnDelete;
        public event EventHandler OnRemove;

        void Item_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender.GetDataContext() is not PrivateFileItem item) return;

            if (item.FileType.Anys(ZFile.FileType.Image, ZFile.FileType.Video, ZFile.FileType.Audio))
                OnViewOrPlay?.Invoke(sender, EventArgs.Empty);
        }

        async void MenuItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender.GetDataContext() is not PrivateFileItem item) return;

            var tag = sender.GetTagOrDefault();

            if (tag == "Menu")
            {
                if (item.IsCorrupted)
                    CorruptedItemMenuFlyout.ShowAt(sender as FrameworkElement);
                else
                    ItemMenuFlyout.ShowAt(sender as FrameworkElement);
            }
            else if (tag == "ViewOrPlay")
                OnViewOrPlay?.Invoke(sender, EventArgs.Empty);
            else if (tag == "Censor")
                OnCensor?.Invoke(sender, EventArgs.Empty);
            //else if (tag == "Properties")
            //    await new PropertiesPopup(item, true).ShowDialog();
            else if (tag == "Export")
                AppEx.FindWindow<MainWindow>().Navigate<Exporter>(new List<PrivateFileItem>() { item });
            else if (tag == "RemovePermanently")
                OnDelete?.Invoke(sender, e);
            else if (tag == "Remove")
                OnRemove?.Invoke(sender, EventArgs.Empty);
        }
    }
}