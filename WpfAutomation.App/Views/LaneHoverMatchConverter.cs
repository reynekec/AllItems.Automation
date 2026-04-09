using System.Globalization;
using System.Windows.Data;

namespace WpfAutomation.App.Views;

public sealed class LaneHoverMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string laneId || values[1] is not string hoveredLaneId)
        {
            return false;
        }

        var isLaneMatch = string.Equals(laneId, hoveredLaneId, StringComparison.Ordinal);
        if (!isLaneMatch)
        {
            return false;
        }

        // Optional first-state constraints: empty lane + active drag preview.
        if (values.Length >= 4)
        {
            if (values[2] is not int itemCount || itemCount != 0)
            {
                return false;
            }

            if (values[3] is not bool isDragPreviewVisible || !isDragPreviewVisible)
            {
                return false;
            }
        }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
