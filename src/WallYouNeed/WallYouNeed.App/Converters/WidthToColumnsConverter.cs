using System;
using System.Globalization;
using System.Windows.Data;

namespace WallYouNeed.App.Converters
{
    public class WidthToColumnsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string minWidth)
            {
                var minColumnWidth = double.Parse(minWidth);
                return Math.Max(1, (int)(width / minColumnWidth));
            }
            return 4; // Default fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 