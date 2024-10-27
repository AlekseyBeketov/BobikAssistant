// RoleToHorizontalOptionsConverter.cs
using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace BobikAssistant
{
    public class RoleToHorizontalOptionsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string role = value as string;
            return role == "user" ? LayoutOptions.End : LayoutOptions.Start;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
