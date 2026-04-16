using System.Globalization;
using System.Windows.Data;

namespace AllItems.Automation.Browser.App.Docking.Controls;

public sealed class PinButtonContentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPinned)
        {
            return isPinned ? "📌" : "📍";
        }

        return "📍";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
