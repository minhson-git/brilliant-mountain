using IOApp.Pages;
using IOCore;
using IOCore.Dialogs;
using IOCore.Files;
using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;

namespace IOApp.Features
{
    internal partial class FileControl : UserControlEx
    {
        public FileControl()
        {
            InitializeComponent();
        }

        //

        public delegate void EventHandler(object sender, EventArgs e);

        public event EventHandler? OnViewOrPlay;
        public event EventHandler? OnCensor;
        public event RoutedEventHandler? OnDelete;
        public event EventHandler? OnRemove;

        void Item_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender.GetDataContext() is not SecureFileItem item) return;

            if (item.FileType is ZFile.FileType.Image or ZFile.FileType.Video or ZFile.FileType.Audio)
                OnViewOrPlay?.Invoke(sender, EventArgs.Empty);
        }

        void MenuItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender.GetDataContext() is not SecureFileItem item) return;

            var tag = sender.GetTagOrDefault();

            if (tag == "ViewOrPlay")
                OnViewOrPlay?.Invoke(sender, EventArgs.Empty);
            else if (tag == "Censor")
                OnCensor?.Invoke(sender, EventArgs.Empty);
            else if (tag == "Properties")
            {
                AppEx.FindWindow<MainWindow>().Let(w =>
                {
                    w.Dialog.Open(new PropertiesDialog(w, item.GetInfo()));
                });
            }
            else if (tag == "Export")
                AppEx.FindWindow<MainWindow>().Let(w => w.Navigate(typeof(Exporter), new List<SecureFileItem>() { item }));
            else if (tag == "RemovePermanently")
                OnDelete?.Invoke(sender, e);
            else if (tag == "Remove")
                OnRemove?.Invoke(sender, EventArgs.Empty);
        }
    }
}