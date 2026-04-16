using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AllItems.Automation.Browser.App.Models.Flow;

namespace AllItems.Automation.Browser.App.Views;

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