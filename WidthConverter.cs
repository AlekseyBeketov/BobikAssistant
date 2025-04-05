using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace BobikAssistant
{
    public class WidthConverter : IValueConverter
    {
        private const double MaxWidth = 700; // Максимальная ширина сообщения
        private const double MinWidth = 370; // Минимальная ширина сообщения
        private const double ScaleFactor = 0.8; // Коэффициент масштабирования


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                double baseWidth = width * ScaleFactor;
                return Math.Min(Math.Max(baseWidth, MinWidth), MaxWidth);
            }
            return MaxWidth; // Значение по умолчанию
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 