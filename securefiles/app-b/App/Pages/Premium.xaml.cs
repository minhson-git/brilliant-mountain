using IOCore.Base;
using IOCore.Core;
using IOCore.Pixels;
using IOCore.Premium;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IOApp.Pages
{
    public partial class StoreIdToGradientConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var colorMapping = new Dictionary<string, (string, string)>
            {
                { LicenseCore.ADD_ON_CONFIGS[0].StoreId, ("#FF7DDFFF", "#FFD5D5D5") },
                { LicenseCore.ADD_ON_CONFIGS[1].StoreId, ("#FFFF85C0", "#FFD5D5D5") },
                { LicenseCore.ADD_ON_CONFIGS[2].StoreId, ("#FFEEFF88", "#FFD5D5D5") }
            };

            var colors = ("#FF7DDFFF", "#FFD5D5D5");

            if (value is string storeId && colorMapping.TryGetValue(storeId, out var mappedColors))
                colors = mappedColors;

            return new LinearGradientBrush
            {
                GradientStops =
                {
                    new() { Color = ColorHelper.ToColor(colors.Item1), Offset = 0.0 },
                    new() { Color = ColorHelper.ToColor(colors.Item2), Offset = 1.0 }
                }
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    internal sealed partial class Premium : MainWindowPremiumPage
    {
        public class FeatureEx(char? icon, string? title, string? headline, bool isAvailableInFree, bool isAvailableInPremium) :
            Feature(null, null, title, headline, null, icon)
        {
            public bool IsAvailableInFree { get; private set; } = isAvailableInFree;
            public bool IsAvailableInPremium { get; private set; } = isAvailableInPremium;
        }

        public ObservableCollectionEx<FeatureEx> FeatureExs { get; private set; } = [];

        public Premium()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnNavigatedToEx(Frame sender, object? parameter, NavigationEventArgs e)
        {
            base.OnNavigatedToEx(sender, parameter, e);

            if (NavigateCount == 1)
                FeatureExs.Replace(PremiumCore.Features.Select(i => new FeatureEx(i.Icon, i.Title, i.Headline, false, true)));
        }
    }

    internal class PremiumProxy : BindingProxy<Premium> { }
}