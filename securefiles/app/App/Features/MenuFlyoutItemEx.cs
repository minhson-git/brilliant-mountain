using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IOApp.Features
{
    public partial class MenuFlyoutItemEx : MenuFlyoutItem, INotifyPropertyChanged
    {
        static readonly Type _type = typeof(MenuFlyoutItemEx);

        #region NotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool SetAndNotify<T>(ref T field, T value, [CallerMemberName] string? propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Notify(propertyName);
            return true;
        }

        public void Notify([CallerMemberName] string? propertyName = "", params string[] propertyNames)
        {
            if (propertyName is null)
                PropertyChanged?.Invoke(this, new(null));
            else
                PropertyChanged?.Invoke(this, new(propertyName));

            foreach (var i in propertyNames)
                PropertyChanged?.Invoke(this, new(i));
        }
        #endregion

        public MenuFlyoutItemEx() { }

        #region Visibility

        public bool IsVisible0 { get => (bool)GetValue(IsVisible0Property); set => SetValue(IsVisible0Property, value); }
        static readonly DependencyProperty IsVisible0Property = DependencyProperty.Register(nameof(IsVisible0), typeof(bool), _type, new(true,
            (d, e) => d.Var<MenuFlyoutItemEx>(_ => _.Visible())));

        public bool IsVisible1 { get => (bool)GetValue(IsVisible1Property); set => SetValue(IsVisible1Property, value); }
        static readonly DependencyProperty IsVisible1Property = DependencyProperty.Register(nameof(IsVisible1), typeof(bool), _type, new(true,
            (d, e) => d.Var<MenuFlyoutItemEx>(_ => _.Visible())));

        public bool IsVisible2 { get => (bool)GetValue(IsVisible2Property); set => SetValue(IsVisible2Property, value); }
        static readonly DependencyProperty IsVisible2Property = DependencyProperty.Register(nameof(IsVisible2), typeof(bool), _type, new(true,
            (d, e) => d.Var<MenuFlyoutItemEx>(_ => _.Visible())));

        public bool IsVisible3 { get => (bool)GetValue(IsVisible3Property); set => SetValue(IsVisible3Property, value); }
        static readonly DependencyProperty IsVisible3Property = DependencyProperty.Register(nameof(IsVisible3), typeof(bool), _type, new(true,
            (d, e) => d.Var<MenuFlyoutItemEx>(_ => _.Visible())));

        public bool IsVisible4 { get => (bool)GetValue(IsVisible4Property); set => SetValue(IsVisible4Property, value); }
        static readonly DependencyProperty IsVisible4Property = DependencyProperty.Register(nameof(IsVisible4), typeof(bool), _type, new(true,
            (d, e) => d.Var<MenuFlyoutItemEx>(_ => _.Visible())));

        void Visible() => Visibility = IsVisible0 && IsVisible1 && IsVisible2 && IsVisible3 && IsVisible4 ? Visibility.Visible : Visibility.Collapsed;

        #endregion
    }
}