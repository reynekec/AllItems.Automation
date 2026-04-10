using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WpfAutomation.App.Models.Flow;

namespace WpfAutomation.App.Views;

public sealed class ActionNodeCredentialAssignedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NavigateToUrlActionParameters navigateParameters)
        {
            return !navigateParameters.EnableAuthentication || string.IsNullOrWhiteSpace(navigateParameters.CredentialId)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}