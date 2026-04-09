using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.Core.Page;

public sealed class NavigationService
{
    private readonly BrowserOptions _options;
    private readonly DiagnosticsService _diagnosticsService;

    public NavigationService(BrowserOptions options, DiagnosticsService diagnosticsService)
    {
        _options = options;
        _diagnosticsService = diagnosticsService;
    }

    public async Task ExecuteAsync(Func<int, int, CancellationToken, Task> navigateAction, CancellationToken cancellationToken = default)
    {
        var maxAttempts = Math.Max(1, _options.RetryCount + 1);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await navigateAction(attempt, maxAttempts, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastException = exception;
                if (attempt >= maxAttempts)
                {
                    break;
                }

                _diagnosticsService.Warn($"Navigation attempt {attempt}/{maxAttempts} failed. Retrying...");
            }
        }

        throw lastException ?? new InvalidOperationException("Navigation failed for an unknown reason.");
    }
}