using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace friction_tester
{
    public class LocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string resourceKey && !string.IsNullOrEmpty(resourceKey))
            {
                try
                {
                    // Try to find the resource in the current application resources
                    var resource = Application.Current.FindResource(resourceKey);
                    return resource?.ToString() ?? resourceKey;
                }
                catch
                {
                    // If resource not found, return the key as fallback
                    return resourceKey;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}