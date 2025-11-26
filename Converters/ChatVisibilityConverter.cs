using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CursorBackup.Models;

namespace CursorBackup.Converters
{
    public class ChatVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SettingType type)
            {
                // Hide chat histories and documentations in the main list, they're shown in their respective Expanders
                return (type == SettingType.ChatHistory || type == SettingType.Documentation) 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

