using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.IntegrationTests.TestUtilities;

using AppBrowserType = WpfAutomation.Core.Configuration.BrowserType;

internal static class IntegrationHarness
{
    public static async Task<(BrowserSession? Session, DiagnosticsService Diagnostics)> TryStartSessionAsync(
        AppBrowserType browserType,
        BrowserOptions? options = null)
    {
        var diagnostics = new DiagnosticsService();

        try
        {
            var launcher = new BrowserLauncher(browserType, diagnosticsService: diagnostics);
            var session = await launcher.StartAsync(options ?? new BrowserOptions
            {
                Headless = true,
                TimeoutMs = 5000,
                RetryCount = 2,
            });

            return (session, diagnostics);
        }
        catch
        {
            return (null, diagnostics);
        }
    }

    public static string ToDataUrl(string html)
    {
        return "data:text/html," + Uri.EscapeDataString(html);
    }
}
