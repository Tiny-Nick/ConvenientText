// ============================================================
//  ColorToBrushConverter.cs
//  作用：XAML 绑定用的转换器。Avalonia 的 Foreground 需要
//  Brush（画刷），而我们的数据是 Color（颜色），直接绑定
//  会报“无法转换”的警告且颜色不显示，所以过一道转换。
//  用法：{Binding DotColor, Converter={x:Static conv:ColorToBrushConverter.Instance}}
// ============================================================

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ConvenientText.Converters
{
    /// <summary>
    /// 把 Color 转换成画刷（Brush）。
    /// 【修复】XAML 里 Foreground 直接绑定 Color 类型会报
    /// "Could not convert ... to IBrush" 警告且颜色不显示，
    /// 需要通过这个转换器包一层 SolidColorBrush。
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public static readonly ColorToBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
                return new SolidColorBrush(color);
            return Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
                return brush.Color;
            return Colors.White;
        }
    }
}
