using System;
using System.Globalization;
using System.Windows.Data;

namespace map_creator.Utilities
{
    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return parameter?.ToString();

            return Binding.DoNothing;
        }
    }
}
