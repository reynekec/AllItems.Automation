# AllItems.Automation

WPF + Playwright automation framework with a fluent API, diagnostics, and page/element inspection support.

## Quick Start

1. Restore and build:
```powershell
dotnet build WpfAutomation.sln
```
2. Run tests:
```powershell
dotnet test WpfAutomation.sln --no-build
```
3. Launch the WPF host app:
```powershell
dotnet run --project AllItems.Automation.Browser.App/AllItems.Automation.Browser.App.csproj
```

## Fluent API Examples

Navigate and interact:
```csharp
using AllItems.Automation.Browser.Core;
using AllItems.Automation.Browser.Core.Configuration;

var page = await Automation
	.OpenBrowser(BrowserType.Chromium)
	.NavigateUrlAsync("https://example.com");

var button = page.Search().ByText("More information");
await button.ClickAsync();
```

Session-based usage:
```csharp
using AllItems.Automation.Browser.Core.Browser;
using AllItems.Automation.Browser.Core.Configuration;

var launcher = new BrowserLauncher(BrowserType.Chromium);
await using var session = await launcher.StartAsync(new BrowserOptions { Headless = true });
var page = await session.NewPageAsync();
await page.NavigateUrlAsync("https://playwright.dev");
```

## Inspection Examples

Element inspection:
```csharp
var report = await page
	.Search()
	.ById("target")
	.InspectAsync(new InspectOptions
	{
		IncludeShadowDom = true,
		IncludeFrames = true,
		IncludeScreenshot = true,
		ExportJson = true,
	});
```

Page inspection:
```csharp
var pageReport = await page.InspectPage().InspectAsync(new PageInspectOptions
{
	IncludeFrames = true,
	IncludeShadowDom = true,
	ExportJson = true,
});
```

## WPF Walkthrough

The host app provides:
- URL input for target navigation.
- Start/Stop commands with cancellation.
- Real-time diagnostics log streaming into the UI.
- Run status updates for running/completed/cancelled/failed states.

Main surfaces:
- App bootstrap and DI: `AllItems.Automation.Browser.App/App.xaml.cs`
- View model orchestration: `AllItems.Automation.Browser.App/ViewModels/MainViewModel.cs`
- UI layout: `AllItems.Automation.Browser.App/MainWindow.xaml`

## Actions Sidebar Contract (WPF)

The host now includes a reusable actions sidebar control designed for parent-owned state.

Parent data contract:
- `ActionCatalog`: full `ObservableCollection<UiActionCategory>` source.
- `ActionSearchText`: search text state (two-way bound).
- `FilteredActionCategories`: parent-projected filtered categories.
- `ExpandedCategoryIds`: parent-owned expand/collapse state.
- `SelectedSidebarAction`: currently selected item.

Parent command contract:
- `InvokeActionCommand`: receives `UiActionInvokeRequest`.
- `ToggleCategoryCommand`: receives `UiActionCategoryToggleRequest`.
- `StartDragCommand`: receives `UiActionDragRequest`.

Action authoring metadata requirement:
- Set `ActionMetadata.IsContainer` explicitly on each action definition.
- Use `IsContainer: true` for actions that should drop to canvas as `FlowContainerNodeModel`.
- Missing/null `IsContainer` is treated as `false` for backward compatibility.

Main window integration:
- The control is hosted in `AllItems.Automation.Browser.App/MainWindow.xaml` as `ActionsSidebarControl`.
- `MainViewModel` seeds a starter catalog and owns mutable sidebar state and callbacks.

Known parity gaps (v1 vs full Power Automate UI):
- Uses text placeholders for icons (`IconKeyOrPath`) and does not yet render a full icon asset pack.
- Drag payload is emitted and drag starts, but no downstream flow-canvas consumer is wired yet.

## Example Scenarios

1. Navigate to GitHub and open Issues:
```csharp
var page = await Automation.OpenBrowser(BrowserType.Chromium)
	.NavigateUrlAsync("https://github.com/microsoft/playwright-dotnet");

await page.Search().ByRole("Link").ClickAsync();
```

2. Generate a page inspection report:
```csharp
var inspection = await page.InspectPage().InspectAsync(new PageInspectOptions
{
	IncludeFrames = true,
	IncludeShadowDom = true,
	ExportJson = true,
});

Console.WriteLine(inspection.JsonExportPath);
```

## Build and Test Pipeline

Use scripts for repeatable local pipeline execution:
- `scripts/build.ps1`
- `scripts/test.ps1`

Optional CI workflow is available in `.github/workflows/ci.yml`.

## Performance Baseline (Current)

Observed from local integration runs (headless Chromium):
- Typical navigation action: sub-second to a few seconds depending on target/network.
- Typical interaction action (click/fill/type): tens of milliseconds to low hundreds.
- Inspection report size: small pages are typically KB-level; larger DOM/frame trees can grow quickly with descendants/shadow/frame capture enabled.

Use these values as relative baselines and re-measure when changing inspection depth or retry/timeout settings.