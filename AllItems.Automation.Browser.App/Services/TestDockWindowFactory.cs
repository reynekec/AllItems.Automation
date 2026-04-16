using Microsoft.Extensions.DependencyInjection;

namespace AllItems.Automation.Browser.App.Services;

public sealed class TestDockWindowFactory : ITestDockWindowFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TestDockWindowFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ITestDockWindowHandle Create()
    {
        var window = _serviceProvider.GetRequiredService<TestDockWindow>();
        return new TestDockWindowHandle(window);
    }
}
