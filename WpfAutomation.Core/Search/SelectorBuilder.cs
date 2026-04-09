namespace WpfAutomation.Core.Search;

public static class SelectorBuilder
{
    public static string ById(string id)
    {
        return $"#{EscapeCssIdentifier(id)}";
    }

    public static string BuildCssPath(IEnumerable<string> segments)
    {
        return string.Join(" > ", segments);
    }

    public static string BuildXPath(IEnumerable<string> segments)
    {
        return "/" + string.Join("/", segments.Select(segment => segment.Trim('/')));
    }

    private static string EscapeCssIdentifier(string value)
    {
        return value.Replace("\"", "\\\"");
    }
}