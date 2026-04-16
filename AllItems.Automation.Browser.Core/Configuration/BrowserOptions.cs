using Microsoft.Playwright;
using System.ComponentModel.DataAnnotations;

namespace AllItems.Automation.Browser.Core.Configuration;

public sealed class BrowserOptions
{
    public bool Headless { get; set; }

    [Range(1, int.MaxValue)]
    public int TimeoutMs { get; set; } = 5000;

    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    public bool NavigationWaitUntilNetworkIdle { get; set; }

    public string? ScreenshotDirectory { get; set; }

    public string? InspectionExportDirectory { get; set; }

    public HttpCredentials? HttpCredentials { get; set; }

    public IReadOnlyList<ClientCertificate>? ClientCertificates { get; set; }

    public IReadOnlyList<KeyValuePair<string, string>>? ExtraHttpHeaders { get; set; }
}
