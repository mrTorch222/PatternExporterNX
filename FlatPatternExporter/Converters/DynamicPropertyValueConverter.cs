using System.Globalization;
using System.Windows.Data;
using FlatPatternExporter.Models;

namespace FlatPatternExporter.Converters;

public class DynamicPropertyValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is [IDynamicPropertyValueSource item, string propPath])
            return item.GetDynamicPropertyValue(propPath);
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return [];
    }
}
