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
            if (value is string role)
            {
                if (parameter as string == "GridColumn")
                {
                    // Устанавливаем колонку в зависимости от роли
                    return role.ToLower() == "user" ? 2 : 0;
                }
                return role.ToLower() == "user" ? LayoutOptions.End : LayoutOptions.Start;
            }
            return LayoutOptions.Start;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
