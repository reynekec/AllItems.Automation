using System.Globalization;
using System.Windows.Data;

namespace AllItems.Automation.Browser.App.Views;

public sealed class StatusBarIconGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var glyphToken = value as string;
        if (string.IsNullOrWhiteSpace(glyphToken))
        {
            return string.Empty;
        }

        return glyphToken.Trim() switch
        {
            "$(pulse)" => "●",
            "$(globe)" => "◎",
            "$(browser)" => "◫",
            "$(output)" => "☰",
            "$(info)" => "ℹ",
            "$(debug-stop)" => "■",
            "$(layout)" => "▦",
            "$(sync~spin)" => "↻",
            "$(check)" => "✓",
            "$(circle-slash)" => "⊘",
            "$(error)" => "✕",
            "$(warning)" => "⚠",
            "$(symbol-event)" => "◆",
            _ => string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}