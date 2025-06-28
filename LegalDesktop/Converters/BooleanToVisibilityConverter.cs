using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LegalDesktop.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        // Opcional: propiedad para invertir la lógica (Visibility.Collapsed cuando es true)
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (Invert)
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibilityValue)
            {
                if (Invert)
                {
                    return visibilityValue != Visibility.Visible;
                }
                return visibilityValue == Visibility.Visible;
            }
            return false;
        }
    }
}