using IOCore.UI;
using IOCore.Utils;
using Microsoft.UI.Xaml;
using System;
using System.Drawing;

namespace IOApp.Features
{
    public partial class MediaThumbnail : ControlEx
    {
        static readonly Type _type = typeof(MediaThumbnail);

        public MediaThumbnail()
        {
            InitializeComponent();
        }

        public string ThumbnailPath
        {
            get
            {
                var ret = (string)GetValue(ThumbnailPathProperty);
                return string.IsNullOrWhiteSpace(ret) ? "-" : ret;
            }
            set => SetValue(ThumbnailPathProperty, value);
        }
        public static readonly DependencyProperty ThumbnailPathProperty = DependencyProperty.Register(nameof(ThumbnailPath), typeof(string), _type, new("-",
            (d, e) => d.Var<MediaThumbnail>(_ => _.Notify(nameof(ThumbnailPath)))));

        public TimeSpan Duration { get => (TimeSpan)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(TimeSpan), _type, new(null,
            (d, e) => d.Var<MediaThumbnail>(_ =>
            {
                _.Notify(nameof(IsDurationVisible));
                _.Notify(nameof(Duration));
            })));

        public string MediaPath { get => (string)GetValue(MediaPathProperty); set => SetValue(MediaPathProperty, value); }
        public static readonly DependencyProperty MediaPathProperty = DependencyProperty.Register(nameof(MediaPath), typeof(string), _type, new(null,
            (d, e) => d.Var<MediaThumbnail>(_ => _.Notify(nameof(MediaPath)))));

        public bool CanPlay { get => (bool)GetValue(CanPlayProperty); set => SetValue(CanPlayProperty, value); }
        public static readonly DependencyProperty CanPlayProperty = DependencyProperty.Register(nameof(CanPlay), typeof(bool), _type, new(false,
            (d, e) => d.Var<MediaThumbnail>(_ => _.Notify(nameof(CanPlay)))));

        public bool Mini { get => (bool)GetValue(MiniProperty); set => SetValue(MiniProperty, value); }
        public static readonly DependencyProperty MiniProperty = DependencyProperty.Register(nameof(Mini), typeof(bool), _type, new(false,
            (d, e) => d.Var<MediaThumbnail>(_ => _.Notify(nameof(Mini), nameof(ThumbnailSize)))));

        public Size ThumbnailSize => Mini ? new(60, 60) : new(256, 144);
        public bool IsDurationVisible => Duration != TimeSpan.Zero;
    }
}