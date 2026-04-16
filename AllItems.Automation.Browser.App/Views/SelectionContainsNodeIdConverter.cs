using System.Globalization;
using System.Windows.Data;

namespace AllItems.Automation.Browser.App.Views;

public sealed class SelectionContainsNodeIdConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string nodeId || values[1] is not IEnumerable<string> selectedNodeIds)
        {
            return false;
        }

        return selectedNodeIds.Contains(nodeId, StringComparer.Ordinal);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}