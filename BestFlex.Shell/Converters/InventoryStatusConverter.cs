using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BestFlex.Shell.Converters
{
    public class InventoryStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "In Stock" => new SolidColorBrush(Colors.Green),
                    "Low Stock" => new SolidColorBrush(Colors.Orange),
                    "Very Low" => new SolidColorBrush(Colors.Red),
                    "Out of Stock" => new SolidColorBrush(Colors.Red),
                    "Unknown" => new SolidColorBrush(Colors.Gray),
                    "Error" => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
