using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace BobikAssistant
{
    public class MuteIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object? parameter, CultureInfo? culture)
        {
            bool isMuted = (bool)value;
            string imagePath = isMuted ? "D:/Sources/BobikAssistant/BobikAssistant/Resources/Icons/sound_off.png" : "D:/Sources/BobikAssistant/BobikAssistant/Resources/Icons/sound_on.png";
            return ImageSource.FromFile(imagePath);
        }

        public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotImplementedException();
        }
    }
}