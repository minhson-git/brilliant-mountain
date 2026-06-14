using CommunityToolkit.Mvvm.Input;
using IOApp.Pages;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using static IOApp.Configs.AppTypes;

namespace IOApp.Features
{
    public enum PlayerItemEventKind
    {
        None,
        Preview,
        Open,
        PrivateChanged,
        FavoriteChanged,
        Remove,
        Delete,
    }

    internal class PlayerItemEventArgs(PlayerItemEventKind kind, PlayerItem item) : EventArgs
    {
        public PlayerItemEventKind Kind = kind;
        public PlayerItem Item = item;
    }

    internal partial class PlayerItemControl : UserControlEx
    {
        static readonly Type _type = typeof(PlayerItemControl);

        public delegate void EventHandler(object sender, PlayerItemEventArgs e);
        public event EventHandler? OnHandle;

        protected static readonly MilestoneCounter _privateChangeCounter = new();

        public bool UsePlayFeature { get => (bool)GetValue(UsePlayFeatureProperty); set => SetValue(UsePlayFeatureProperty, value); }
        public static readonly DependencyProperty UsePlayFeatureProperty = DependencyProperty.Register(nameof(UsePlayFeature), typeof(bool), _type, new(true,
            (d, e) => d.Var<PlayerItemControl>(self => self.Notify(nameof(UsePlayFeature)))));

        public bool UseMoreButton { get => (bool)GetValue(UseMoreButtonProperty); set => SetValue(UseMoreButtonProperty, value); }
        public static readonly DependencyProperty UseMoreButtonProperty = DependencyProperty.Register(nameof(UseMoreButton), typeof(bool), _type, new(true,
            (d, e) => d.Var<PlayerItemControl>(self => self.Notify(nameof(UseMoreButton)))));

        //

        public bool UseAddToPlaylistsItem { get => (bool)GetValue(UseAddToPlaylistsItemProperty); set => SetValue(UseAddToPlaylistsItemProperty, value); }
        public static readonly DependencyProperty UseAddToPlaylistsItemProperty = DependencyProperty.Register(nameof(UseAddToPlaylistsItem), typeof(bool), _type, new(true,
            (d, e) => d.Var<PlayerItemControl>(self => self.Notify(nameof(UseAddToPlaylistsItem)))));

        public bool UseRemoveItem { get => (bool)GetValue(UseRemoveItemProperty); set => SetValue(UseRemoveItemProperty, value); }
        public static readonly DependencyProperty UseRemoveItemProperty = DependencyProperty.Register(nameof(UseRemoveItem), typeof(bool), _type, new(true,
            (d, e) => d.Var<PlayerItemControl>(self => self.Notify(nameof(UseRemoveItem)))));

        public PlayerItemControl()
        {
        }

        readonly TapGesture _tapGesture = new();

        protected async void ActionPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (await _tapGesture.IsDoubleTap())
                return;

            if (UsePlayFeature)
                PreviewCommand.Execute(sender);
        }

        protected void ActionPanel_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            _tapGesture.SetDoubleTap();

            if (UsePlayFeature)
                sender.LetDataContext<PlayerItem>(item =>
                    OnHandle?.Invoke(sender, new(PlayerItemEventKind.Open, item))
                );
        }

        [RelayCommand]
        void Preview(object sender) => sender.LetDataContext<PlayerItem>(item =>
            OnHandle?.Invoke(sender, new(PlayerItemEventKind.Preview, item))
        );

        [RelayCommand]
        void Favorite(object sender) => sender.LetDataContext<PlayerItem>(item =>
        {
            //PlayerContext.Inst.AddOrRemovePlaylist(BuiltInPlaylist.Favorite, item, null);
            OnHandle?.Invoke(sender, new(PlayerItemEventKind.FavoriteChanged, item));
        });

        [RelayCommand]
        void ShowMenu(object sender) => sender.LetDataContext<PlayerItem>(item =>
        {
            //MenuContext.Inst.Update(item, UsePlayFeature, UseAddToPlaylistsItem, false, UseRemoveItem,
            //    i => OnHandle?.Invoke(sender, new(PlayerItemEventKind.Open, item)),
            //    i =>
            //    {
            //        //PlayerContext.Inst.RemoveAll([i], false);
            //        //OnHandle?.Invoke(sender, new(PlayerItemEventKind.Remove, i));
            //    },
            //    i =>
            //    {
            //        Window.Tip.Confirm(null, R.T(L.RemovePermanently), R.T(L.CannotBeUndone), () =>
            //        {
            //            if (Window is MainWindow mainWindow)
            //            {
            //                mainWindow.HomeNavigation?.CurrentPage?.Let(page =>
            //                {
            //                    if (page is HomePage homePage)
            //                        homePage.PlayerEx.Player.Stop();
            //                });
            //            }

            //            PlayerContext.Inst.RemoveAll([i], true);
            //        });
            //        OnHandle?.Invoke(sender, new(PlayerItemEventKind.Delete, i));
            //    })
            //.ShowAt(sender as FrameworkElement);
        });
    }
}