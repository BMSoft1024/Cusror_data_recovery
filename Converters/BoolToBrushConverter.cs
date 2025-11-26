using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CursorBackup.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAvailable)
            {
                return isAvailable ? new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)) : new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            }
            return new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAvailable)
            {
                return isAvailable ? "✓ Available" : "✗ Not Available";
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAvailable)
            {
                return isAvailable ? new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)) : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

