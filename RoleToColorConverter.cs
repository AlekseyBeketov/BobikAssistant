// RoleToColorConverter.cs
using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace BobikAssistant
{
    public class RoleToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string role = value as string;
            return role == "user" ? Color.FromHex("#273f87") : Color.FromHex("#4169E1");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
