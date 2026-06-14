using IOApp.Windows;
using IOCore.Annotation;
using IOCore.Colour;
using IOCore.Converters;
using IOCore.License;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace IOApp.Pages;

public partial class StoreIdToGradientConverter : BaseValueConverter
{
    public override object Convert(object value, Type targetType, object parameter, string language)
    {
        var colorMapping = new Dictionary<string, (string, string)>
        {
            { StoreContextProxy.ADD_ON_CONFIGS[0].StoreId, ("#FF7DDFFF", "#FFD5D5D5") },
            { StoreContextProxy.ADD_ON_CONFIGS[1].StoreId, ("#FFFF85C0", "#FFD5D5D5") },
            { StoreContextProxy.ADD_ON_CONFIGS[2].StoreId, ("#FFEEFF88", "#FFD5D5D5") }
        };

        var colors = ("#FF7DDFFF", "#FFD5D5D5");

        if (value is string storeId && colorMapping.TryGetValue(storeId, out var mappedColors))
            colors = mappedColors;

        return new LinearGradientBrush
        {
            GradientStops =
            {
                new() { Color = ColourUtils.ToColor(colors.Item1), Offset = 0.0 },
                new() { Color = ColourUtils.ToColor(colors.Item2), Offset = 1.0 }
            }
        };
    }
}

[BindingProxy]
internal partial class Premium : PremiumWindowPremiumPage
{
    public Premium()
    {
        InitializeComponent();
    }
}