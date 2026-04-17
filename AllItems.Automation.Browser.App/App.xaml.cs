using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using AllItems.Automation.Browser.App.Docking.Services;
using AllItems.Automation.Browser.App.NodeInspector.Contracts;
using AllItems.Automation.Browser.App.NodeInspector.Services;
using AllItems.Automation.Browser.App.Services;
using AllItems.Automation.Browser.App.Services.Credentials;
using AllItems.Automation.Browser.App.Services.Diagnostics;
using AllItems.Automation.Browser.App.Services.Flow;
using AllItems.Automation.Browser.App.ViewModels;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;

namespace AllItems.Automation.Browser.App;

public partial class App : Application
{
	private ServiceProvider? _serviceProvider;

	protected override void OnStartup(StartupEventArgs e)
	{
		AppCrashLogger.Initialize();
		AppCrashLogger.RegisterDispatcher(this);

		try
		{
			AppCrashLogger.Info("Application startup begin.");
			base.OnStartup(e);

			var services = new ServiceCollection();
			ConfigureServices(services);
			_serviceProvider = services.BuildServiceProvider();

			var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
			mainWindow.Show();
			AppCrashLogger.Info("Application startup complete.");
		}
		catch (Exception exception)
		{
			AppCrashLogger.Error("Startup failure.", exception);
			throw;
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		AppCrashLogger.Info($"Application exit code: {e.ApplicationExitCode}");
		_serviceProvider?.Dispose();
		base.OnExit(e);
	}

	private static void ConfigureServices(IServiceCollection services)
	{
		var browserOptions = new BrowserOptions
		{
			Headless = true,
			TimeoutMs = 5000,
			RetryCount = 3,
		};

		services.AddSingleton(browserOptions);
		services.AddSingleton<DiagnosticsService>();
		services.AddSingleton(provider => new ScreenshotService(provider.GetRequiredService<BrowserOptions>()));
		services.AddSingleton<ICredentialStore, CredentialStore>();
		services.AddSingleton<ICredentialUnlockDialogService, CredentialUnlockDialogService>();
		services.AddSingleton<ICredentialManagerDialogService, CredentialManagerDialogService>();
		services.AddSingleton<IMasterPasswordService, MasterPasswordService>();
		services.AddSingleton<IDockLayoutStateStore, InMemoryDockLayoutStateStore>();
		services.AddSingleton<IDockLayoutPersistenceService>(_ => new DockLayoutPersistenceService(GetDockLayoutPath("main")));
		services.AddSingleton<IUiDispatcherService, UiDispatcherService>();
		services.AddSingleton<IAutomationOrchestrator, AutomationOrchestrator>();
		services.AddSingleton<IActionCatalogBuilder, ActionCatalogBuilder>();
		services.AddSingleton<IFlowActionParameterResolver, FlowActionParameterResolver>();
		services.AddSingleton<IFlowEditingService, FlowEditingService>();
		services.AddSingleton<IFlowPersistenceService, FlowPersistenceService>();
		services.AddSingleton<IFlowRecentFileService, FlowRecentFileService>();
		services.AddSingleton<IFlowEdgeRoutingService, FlowEdgeRoutingService>();
		services.AddSingleton<IFlowHitTestService, FlowHitTestService>();
		services.AddSingleton<IFlowLayoutService, FlowLayoutService>();
		services.AddSingleton<IFlowDocumentMapper<ExecutionFlowGraph>, FlowExecutionMapper>();
		services.AddSingleton<IFlowRuntimeExecutor, FlowRuntimeExecutor>();
		services.AddSingleton<IBrowserLauncherFactory, BrowserLauncherFactory>();
		services.AddSingleton<IWebAuthExecutor, WebAuthExecutor>();
		services.AddSingleton<IUserConfirmationDialogService, UserConfirmationDialogService>();
		services.AddSingleton<IFlowExecutionBridge, PlaywrightFlowExecutionBridge>();
		services.AddSingleton<INodeInspectorRuntimeBindingExtension, NullNodeInspectorRuntimeBindingExtension>();
		services.AddSingleton<INodeInspectorFactory, DefaultNodeInspectorFactory>();
		services.AddSingleton<FlowCanvasViewModel>();
		services.AddSingleton<ITestDockWindowFactory, TestDockWindowFactory>();
		services.AddSingleton<ITestDockWindowService, TestDockWindowService>();
		services.AddSingleton<MainViewModel>();
		services.AddTransient(_ => new TestDockViewModel(new DockLayoutPersistenceService(GetDockLayoutPath("test"))));
		services.AddTransient<MainWindow>();
		services.AddTransient<TestDockWindow>();
	}

	private static string GetDockLayoutPath(string profileName)
	{
		var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AllItems.Automation");
		Directory.CreateDirectory(root);
		return Path.Combine(root, $"dock-layout-{profileName}.json");
	}
}

