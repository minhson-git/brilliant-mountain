using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using System;

namespace IOApp.Features
{
    internal partial class ListMenuLayout : UserControlEx
    {
        static readonly Type _type = typeof(ListMenuLayout);

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), _type, new(null,
            (d, e) => d.Var<ListMenuLayout>(_ => _.Notify(nameof(Title)))));
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static readonly DependencyProperty TitlePanelProperty = DependencyProperty.Register(nameof(TitlePanel), typeof(FrameworkElement), _type, new(null,
            (d, e) => d.Var<ListMenuLayout>(_ => _.Notify(nameof(TitlePanel)))));
        public FrameworkElement TitlePanel { get => (FrameworkElement)GetValue(TitlePanelProperty); set => SetValue(TitlePanelProperty, value); }

        public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(nameof(EmptyTextProperty), typeof(string), _type, new(null,
            (d, e) => d.Var<ListMenuLayout>(_ => _.Notify(nameof(EmptyText)))));
        public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

        public static readonly DependencyProperty ContentPanelProperty = DependencyProperty.Register(nameof(ContentPanel), typeof(FrameworkElement), _type, new(null,
            (d, e) => d.Var<ListMenuLayout>(_ => _.Notify(nameof(ContentPanel)))));
        public FrameworkElement ContentPanel { get => (FrameworkElement)GetValue(ContentPanelProperty); set => SetValue(ContentPanelProperty, value); }

        public static readonly DependencyProperty IsEmptyProperty = DependencyProperty.Register(nameof(IsEmpty), typeof(bool), _type, new(true,
            (d, e) => d.Var<ListMenuLayout>(_ => _.Notify(nameof(IsEmpty)))));
        public bool IsEmpty { get => (bool)GetValue(IsEmptyProperty); set => SetValue(IsEmptyProperty, value); }

        public ListMenuLayout()
        {
            InitializeComponent();
        }
    }
}