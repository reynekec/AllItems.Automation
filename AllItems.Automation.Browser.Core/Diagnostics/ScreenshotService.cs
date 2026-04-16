using Microsoft.Playwright;
using AllItems.Automation.Browser.Core.Configuration;

namespace AllItems.Automation.Browser.Core.Diagnostics;

public sealed class ScreenshotService
{
    private readonly BrowserOptions _options;

    public ScreenshotService(BrowserOptions options)
    {
        _options = options;
    }

    public async Task<string?> CapturePageAsync(IPage page, string? filename = null)
    {
        try
        {
            var filePath = BuildScreenshotPath(filename, "page-failure");

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = filePath,
                FullPage = true,
            });

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> CaptureElementAsync(ILocator locator, string? filename = null)
    {
        try
        {
            var filePath = BuildScreenshotPath(filename, "element-failure");

            await locator.ScreenshotAsync(new LocatorScreenshotOptions
            {
                Path = filePath,
            });

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    private string BuildScreenshotPath(string? filename, string prefix)
    {
        var directory = _options.ScreenshotDirectory
            ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "screenshots");

        Directory.CreateDirectory(directory);

        var safeName = SanitizeFileName(filename);
        var fileName = string.IsNullOrWhiteSpace(safeName)
            ? $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png"
            : $"{safeName}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png";

        return Path.GetFullPath(Path.Combine(directory, fileName));
    }

    private static string? SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Where(ch => !invalidChars.Contains(ch)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }
}