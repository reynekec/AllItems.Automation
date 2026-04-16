using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;

namespace AllItems.Automation.Browser.Core.Elements;

public sealed class ElementActionExecutor
{
    private readonly BrowserOptions _options;
    private readonly DiagnosticsService _diagnosticsService;

    public ElementActionExecutor(BrowserOptions options, DiagnosticsService diagnosticsService)
    {
        _options = options;
        _diagnosticsService = diagnosticsService;
    }

    public Task ExecuteAsync(
        string actionName,
        Func<Task> operation,
        Func<Exception, string?, Exception> exceptionFactory,
        Func<Task<string?>> captureFailureScreenshot,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<object?>(
            actionName,
            async () =>
            {
                await operation();
                return null;
            },
            exceptionFactory,
                captureFailureScreenshot,
                cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        string actionName,
        Func<Task<T>> operation,
        Func<Exception, string?, Exception> exceptionFactory,
        Func<Task<string?>> captureFailureScreenshot,
        CancellationToken cancellationToken = default)
    {
        var maxAttempts = Math.Max(1, _options.RetryCount + 1);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _diagnosticsService.Info($"{actionName} start (attempt {attempt}/{maxAttempts})");
                var result = await operation();
                _diagnosticsService.Info($"{actionName} complete");
                return result;
            }
            catch (OperationCanceledException)
            {
                _diagnosticsService.Warn($"{actionName} cancelled");
                throw exceptionFactory(new OperationCanceledException($"{actionName} cancelled."), null);
            }
            catch (Exception exception)
            {
                lastException = exception;
                if (attempt < maxAttempts)
                {
                    _diagnosticsService.Warn($"{actionName} failed on attempt {attempt}/{maxAttempts}. {exception.GetType().Name}: {exception.Message}. Retrying...");
                }
            }
        }

        var screenshotPath = await captureFailureScreenshot();
        throw exceptionFactory(lastException ?? new InvalidOperationException("Element action failed."), screenshotPath);
    }
}