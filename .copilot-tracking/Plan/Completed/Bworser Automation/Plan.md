# Bworser Automation - Implementation Plan

## Executive Summary
Implement a .NET 10 WPF host application with a reusable automation core library that wraps Microsoft.Playwright behind a fluent, domain-style API. The implementation is prioritized to deliver browser automation fundamentals first (launch, navigation, search, interaction, diagnostics), followed by the advanced inspection subsystem (DOM, iframe, shadow DOM traversal, structured reporting).

---

## Problem Statement
The WPF Playwright Automation Framework aims to provide a testable, reusable abstraction over Playwright for WPF applications. It requires:
- Strong separation between WPF UI concerns and browser automation logic
- Fluent, discoverable API for navigation, element search, and interaction
- First-class diagnostics, cancellation, and error handling
- Ability to inspect and analyze the DOM, frames, and shadow DOM
- Full async/await support to keep the WPF UI responsive
- Multi-browser support (Chromium, Firefox, WebKit)

---

## Proposed Approach

### Architecture Principles
1. **Layered Separation**: WPF UI logic isolated in `WpfAutomation.App`, automation core in `WpfAutomation.Core`.
2. **Interface Abstraction**: Core browser automation hidden behind interfaces (`IBrowserLauncher`, `IPageWrapper`, `ISearchContext`, `IUIElement`, `IPageInspector`, `IElementInspector`).
3. **Fluent API**: Encourage chain-like usage: `page.Search().ById("btn").ClickAsync()`.
4. **Diagnostics-First**: Every action is logged; failures capture context (URL, selector, timeout, screenshot).
5. **Async-First**: All automation flows async; UI remains responsive via background task execution.
6. **Phased Delivery**: Core automation first, inspection subsystem second.

### Key Design Decisions
- **Configuration**: Per-session via `BrowserOptions`; no global static state.
- **Exception Model**: Wrapped exceptions with enriched diagnostic context.
- **Cancellation**: Full support via `CancellationToken` throughout the stack.
- **Test Strategy**: Unit tests for core abstractions; integration tests for end-to-end workflows.
- **JavaScript Isolation**: Inspection JS snippets managed separately under `Inspection/JavaScript`.

---

## Phases

### Phase 1: Project Scaffold & Core Infrastructure
**Goal**: Set up solution structure, projects, and foundational abstractions.

- [x] Create `WpfAutomation.sln` with three projects:
  - [x] `WpfAutomation.App` (WPF .NET 10)
  - [x] `WpfAutomation.Core` (Class Library .NET 10)
  - [x] `tests/WpfAutomation.Core.Tests` (xUnit or NUnit)
  
- [x] Add NuGet dependencies:
  - [x] `Microsoft.Playwright` (latest)
  - [x] `Microsoft.Extensions.DependencyInjection` (DI for WPF)
  - [x] `System.ComponentModel.DataAnnotations` (validation)
  - [x] Test dependencies (xUnit, Moq, FluentAssertions)

- [x] Create folder structure as per PRD section 17:
  - [x] `WpfAutomation.Core/Abstractions/` (interfaces)
  - [x] `WpfAutomation.Core/Browser/`
  - [x] `WpfAutomation.Core/Page/`
  - [x] `WpfAutomation.Core/Search/`
  - [x] `WpfAutomation.Core/Elements/`
  - [x] `WpfAutomation.Core/Inspection/` (JavaScript subdir)
  - [x] `WpfAutomation.Core/Diagnostics/`
  - [x] `WpfAutomation.Core/Configuration/`
  - [x] `WpfAutomation.Core/Exceptions/`
  - [x] `WpfAutomation.Core/Reports/` (DTO models)
  - [x] `WpfAutomation.App/Views/`, `ViewModels/`, `Commands/`, `Services/`, `Models/`

- [x] Create interface definitions (no implementation yet):
  - [x] `IBrowserLauncher.cs`
  - [x] `IPageWrapper.cs`
  - [x] `ISearchContext.cs`
  - [x] `IUIElement.cs`
  - [x] `IPageInspector.cs`
  - [x] `IElementInspector.cs`

- [x] Create configuration models:
  - [x] `BrowserOptions.cs` (headless, timeout, retry, screenshot/export dirs)
  - [x] `BrowserType.cs` (enum: Chromium, Firefox, WebKit)

- [x] Create base exception hierarchy:
  - [x] `AutomationException.cs` (base)
  - [x] `NavigationException.cs`
  - [x] `UIElementNotFoundException.cs`
  - [x] `ElementInteractionException.cs`
  - [x] `InspectionException.cs`

**Acceptance Criteria for Phase 1**:
- Solution builds without errors
- All folders and interfaces in place
- Interfaces match PRD specs
- Exception hierarchy covers major failure modes
- Configuration models have expected properties

---

### Phase 2: Browser Lifecycle & Entry Point
**Goal**: Implement browser launch, context creation, and entry-point API.

- [x] Implement `BrowserLauncher.cs`:
  - [x] Constructor takes `BrowserType`
  - [x] `StartAsync(BrowserOptions)` -> launches Playwright browser
  - [x] Wraps `IPlaywright.CreateAsync()` and browser `LaunchAsync()`
  - [x] Returns `BrowserSession`
  - [x] Handles launch failures with wrapped exceptions

- [x] Implement `BrowserSession.cs`:
  - [x] Constructor stores browser, context, options
  - [x] `NewPageAsync()` -> creates new Playwright page, returns wrapped `PageWrapper`
  - [x] `GetPagesAsync()` -> lists open pages
  - [x] `CloseAsync()` -> closes context and browser
  - [x] Implements `IAsyncDisposable` for cleanup

- [x] Implement static entry point `Automation.cs`:
  - [x] `static OpenBrowser(BrowserType type)` -> returns `IBrowserLauncher`
  - [x] Allows fluent chaining: `OpenBrowser(...).NavigateUrlAsync(...)`

- [x] Create minimal `DiagnosticsService.cs`:
  - [x] `Info(string message)`, `Warn()`, `Error()` methods
  - [x] Logs to console or in-memory list (testable)
  - [x] Injectable into other components

- [x] Add unit tests for Phase 2:
  - [x] Test browser launch with each type (Chromium, Firefox, WebKit)
  - [x] Test session creation
  - [x] Test page creation
  - [x] Test cleanup

**Acceptance Criteria for Phase 2**:
- Browser launch works for all three types
- `StartAsync()` returns valid `BrowserSession`
- `NewPageAsync()` creates pages successfully
- Cleanup is safe and idempotent
- Unit tests pass

---

### Phase 3: Page Navigation & Wrapper
**Goal**: Implement page navigation and the `PageWrapper` abstraction.

- [x] Implement `PageWrapper.cs`:
  - [x] Constructor takes Playwright `IPage`, `BrowserSession`, `DiagnosticsService`
  - [x] `NavigateUrlAsync(string url)` -> calls `page.GotoAsync(url)` with wait/retry logic
  - [x] Validates URL format
  - [x] Logs navigation actions via diagnostics
  - [x] Returns `Task<IPageWrapper>` for chaining
  - [x] Handles navigation failures with `NavigationException`
  - [x] Captures screenshot on navigation failure

- [x] Implement `NavigationService.cs` (helper):
  - [x] Wraps retry logic and wait strategies
  - [x] Configurable timeout from `BrowserOptions`
  - [x] Retry count from `BrowserOptions`

- [x] Add properties to `IPageWrapper`:
  - [x] `CurrentUrl` property (from `page.Url`)
  - [x] `Title` property
  
- [x] Implement placeholder methods (return Task without impl):
  - [x] `Search()` -> returns `ISearchContext` (impl in Phase 4)
  - [x] `InspectPage()` -> returns `IPageInspector` (impl in Phase 6)

- [x] Add unit tests for Phase 3:
  - [x] Test navigation to valid URL
  - [x] Test navigation failure with exception
  - [x] Test retry logic
  - [x] Test screenshot capture on failure
  - [x] Test logging

**Acceptance Criteria for Phase 3**:
- Navigation succeeds to real URL
- Navigation failures wrapped in `NavigationException`
- Screenshot captured on failure
- Retry logic applied
- Logs contain navigation start/end events
- Unit tests pass

---

### Phase 4: Search API & Element Wrapping
**Goal**: Implement element search and the `UIElement` abstraction.

- [x] Implement `SearchContext.cs`:
  - [x] Constructor takes Playwright `IPage`, `DiagnosticsService`
  - [x] `ById(string id)` -> creates Playwright locator via CSS selector
  - [x] `ByCss(string selector)` -> Playwright CSS locator
  - [x] `ByRole(string role)` -> Playwright role locator
  - [x] `ByText(string text)` -> Playwright text locator
  - [x] `ByLabel(string label)` -> Playwright label locator
  - [x] `ByPlaceholder(string text)` -> Playwright placeholder locator
  - [x] `ByTitle(string title)` -> Playwright title attribute locator
  - [x] `ByTestId(string testId)` -> Playwright test ID locator
  - [x] All methods log selection attempts
  - [x] All methods return `UIElement` wrapping the locator

- [x] Implement `UIElement.cs`:
  - [x] Constructor takes Playwright `ILocator`, `DiagnosticsService`, `ScreenshotService`
  - [x] `ClickAsync()` -> locator.ClickAsync() with wait + retry
  - [x] `TypeAsync(string text)` -> locator.TypeAsync()
  - [x] `FillAsync(string text)` -> locator.FillAsync()
  - [x] `GetTextAsync()` -> locator.TextContentAsync()
  - [x] `GetAttributeAsync(string name)` -> locator.GetAttributeAsync(name)
  - [x] `IsVisibleAsync()` -> locator.IsVisibleAsync()
  - [x] `IsEnabledAsync()` -> locator.IsEnabledAsync()
  - [x] `HoverAsync()` -> locator.HoverAsync()
  - [x] `CheckAsync()` -> locator.CheckAsync()
  - [x] `UncheckAsync()` -> locator.UncheckAsync()
  - [x] `SelectAsync(string value)` -> locator.SelectOptionAsync(value)
  - [x] `WaitForAsync()` -> locator.WaitForAsync()
  - [x] All actions:
    - Log start/end with action name
    - Apply timeout from `BrowserOptions`
    - Apply retry logic
    - Capture screenshot on failure
    - Throw wrapped exception on failure
  - [x] Placeholder `InspectAsync()` (impl in Phase 6)

- [x] Implement `ElementActionExecutor.cs` (helper):
  - [x] Centralize retry/timeout/error handling logic
  - [x] Called by `UIElement` action methods

- [x] Create `SelectorBuilder.cs` (helper):
  - [x] Utilities for CSS path, XPath building (used in inspection later)

- [x] Add unit tests for Phase 4:
  - [x] Test each search method
  - [x] Test click, fill, type on valid elements
  - [x] Test visibility/enabled state queries
  - [x] Test failure cases (element not found, timeout)
  - [x] Test screenshot on failure
  - [x] Test retry logic
  - [x] Test logging

**Acceptance Criteria for Phase 4**:
- All search methods work correctly
- Element interactions (click, fill, type) succeed
- Timeout and retry applied to interactions
- Failures throw wrapped exceptions
- Screenshots captured on failure
- Logs show all major actions
- Unit tests pass

---

### Phase 5: Diagnostics, Cancellation & Error Handling
**Goal**: Implement robust diagnostics, logging, failure handling, and cancellation support.

- [x] Enhance `DiagnosticsService.cs`:
  - [x] In-memory log list with `LogEntry` objects
  - [x] Timestamps
  - [x] Log levels (Info, Warn, Error)
  - [x] `GetLogs()` method for retrieval
  - [x] `ClearLogs()` method

- [x] Implement `ScreenshotService.cs`:
  - [x] `CapturePageAsync(IPage page, string? filename)` -> saves screenshot
  - [x] `CaptureElementAsync(ILocator locator, string? filename)` -> element screenshot
  - [x] Uses `BrowserOptions.ScreenshotDirectory` for storage
  - [x] Generates path-safe filenames with timestamp
  - [x] Returns full path to saved screenshot

- [x] Implement `LogEntry.cs`:
  - [x] Timestamp, level (Info/Warn/Error), message, optional context data
  - [x] Serializable for export

- [x] Add cancellation support throughout:
  - [x] Update `BrowserLauncher.StartAsync()` to accept `CancellationToken`
  - [x] Update `PageWrapper.NavigateUrlAsync()` to accept `CancellationToken`
  - [x] Update `UIElement` action methods to accept `CancellationToken`
  - [x] Update `ISearchContext` methods to accept `CancellationToken`
  - [x] Pass token through to Playwright calls
  - [x] Handle `OperationCanceledException` and wrap as diagnostic

- [x] Enhance all exception types:
  - [x] Include URL, action name, selector/description, timeout, screenshot path
  - [x] Include inner exception details
  - [x] Implement `ToString()` for readable diagnostics

- [x] Create cancellation helper:
  - [x] Optional `CancellationManager` class for orchestration layer

- [x] Add integration tests for Phase 5:
  - [x] Test cancellation at various points (launch, navigation, interaction)
  - [x] Test exception details (URL, selector, timeout, screenshot)
  - [x] Test log capture and retrieval
  - [x] Test screenshot generation on failure

**Acceptance Criteria for Phase 5**:
- Cancellation stops active operations cleanly
- Screenshots captured on failure
- Logs include all major actions with timestamps
- Exceptions include enriched context
- Exception details match PRD requirements
- Integration tests pass

---

### Phase 6: WPF Host Application
**Goal**: Build the WPF UI and MVVM orchestration layer.

- [x] Create `App.xaml` and `App.xaml.cs`:
  - [x] Wire up MVVM container
  - [x] Register services via `IServiceCollection`
  - [x] No code-behind logic except minimal view wiring

- [x] Implement `MainViewModel.cs`:
  - [x] `Url` property (bindable)
  - [x] `Status` property (bindable)
  - [x] `Logs` observable collection
  - [x] `IsRunning` boolean property
  - [x] `StartCommand` (async relay command)
  - [x] `StopCommand` (cancellation command)
  - [x] Connect logs from `DiagnosticsService` in real-time

- [x] Implement `MainWindow.xaml` and codebehind:
  - [x] URL input textbox
  - [x] Start/Stop buttons
  - [x] Log output panel (ListBox or DataGrid)
  - [x] Status display
  - [x] Minimal codebehind (just DataContext binding)

- [x] Implement `AutomationOrchestrator.cs` (app-layer service):
  - [x] Constructor takes `BrowserOptions`, `DiagnosticsService`, `ScreenshotService`
  - [x] `RunNavigationAsync(string url, CancellationToken)` method
  - [x] Creates `BrowserSession`, navigates, returns `PageWrapper`
  - [x] Handles cleanup on success/failure
  - [x] Propagates diagnostics and screenshots to UI

- [x] Implement relay commands:
  - [x] `AsyncRelayCommand<T>` base class (async MVVM command)
  - [x] Handles `CanExecute` gating based on `IsRunning`
  - [x] Captures cancellation

- [x] Implement `UiDispatcherService.cs`:
  - [x] Bridges logs from background task to UI thread
  - [x] Ensures WPF dispatcher is used when updating ObservableCollections

- [x] Create `UiRunState.cs` and `UiLogItem.cs` models:
  - [x] Represent UI-layer abstractions of automation state

- [x] Add integration tests for Phase 6:
  - [x] Test command execution (without real browser if possible)
  - [x] Test log binding to UI
  - [x] Test cancellation from UI
  - [x] Test status updates during execution

**Acceptance Criteria for Phase 6**:
- WPF window displays
- URL input and Start button work
- Stop button cancels execution
- Logs appear in real-time during execution
- Status updates correctly
- UI remains responsive during automation
- Exception details displayed in logs
- Screenshots accessible in log items or status

---

### Phase 7: Element Inspection - DOM Traversal JavaScript
**Goal**: Build JavaScript snippets for DOM and frame traversal.

- [x] Create `Inspection/JavaScript/InspectElement.js`:
  - [x] Receives target element as parameter
  - [x] Returns structured data:
    - Tag name, ID, name, text, inner text, classes, all attributes
    - Computed styles (optional)
    - Bounding box (offsetWidth, offsetHeight, getBoundingClientRect)
    - CSS path (constructed via traversal)
    - XPath (constructed via traversal)
    - Is shadow host flag
    - Is inside shadow DOM flag
    - Children array (recursive)
  - [x] Handles null/undefined gracefully

- [x] Create `Inspection/JavaScript/InspectPage.js`:
  - [x] Iterates main document root
  - [x] Identifies all iframe elements
  - [x] For each iframe, captures URL and nested root elements (non-recursive, will be traversed separately)
  - [x] Returns frame hierarchy

- [x] Create `Inspection/JavaScript/BuildCssPath.js`:
  - [x] Constructs CSS path from element to root
  - [x] Handles IDs, classes, pseudo-selectors

- [x] Create `Inspection/JavaScript/BuildXPath.js`:
  - [x] Constructs XPath from element to root
  - [x] Handles node indices, attribute predicates

- [x] Create `Inspection/JavaScript/InspectShadowDom.js`:
  - [x] Accepts element and checks for shadow root
  - [x] Returns shadow DOM structure recursively

- [x] Create `Inspection/JavaScript/GetComputedStyles.js`:
  - [x] Gets computed styles for element (subset of common ones)

- [x] Create `Inspection/JavaScript/GetAccessibilityData.js`:
  - [x] ARIA attributes, roles, labels, descriptions

- [x] Add unit tests for Phase 7:
  - [x] Test each JS script in isolation via Playwright `EvaluateAsync()`
  - [x] Test returned data shape matches expected model
  - [x] Test with various DOM structures (nested, iframe, shadow DOM)

**Acceptance Criteria for Phase 7**:
- All JS snippets execute without errors
- Returned data structures match model expectations
- Scripts handle edge cases (null elements, missing features)
- Unit tests pass

---

### Phase 8: Element Inspection - C# Implementation
**Goal**: Implement C# wrappers for inspection and reporting.

- [x] Implement inspection report models:
  - [x] `ElementNodeReport.cs` (DTO with tag, id, name, text, classes, attributes, styles, paths, bbox, children)
  - [x] `FrameReport.cs` (frame name, URL, parent URL, root nodes)
  - [x] `AccessibilityReport.cs` (ARIA attributes, role, labels)
  - [x] `BoundingBoxReport.cs` (x, y, width, height)
  - [x] `InspectionReport.cs` (root element, frames, accessibility, screenshot path, JSON export path)
  - [x] `PageInspectionReport.cs` (main root + all frame reports)
  - [x] All models JSON-serializable (use `System.Text.Json` attributes or custom serializer)

- [x] Implement `ElementInspector.cs`:
  - [x] Constructor takes `IPage`, `DiagnosticsService`, `ScreenshotService`
  - [x] `InspectAsync(IUIElement element, InspectOptions options, CancellationToken)` method
  - [x] Calls `InspectElement.js` via page.EvaluateAsync()
  - [x] Maps returned JS object to `InspectionReport` model
  - [x] If `IncludeFrames=true`: traverses each iframe found and recursively inspects
  - [x] If `IncludeShadowDom=true`: calls `InspectShadowDom.js` for shadow roots
  - [x] If `IncludeScreenshot=true`: calls `ScreenshotService.CaptureElementAsync()`
  - [x] If `ExportJson=true`: calls `InspectionSerializer.ExportJsonAsync()`
  - [x] Handles exceptions (element vanished, timeout, etc.) with `InspectionException`
  - [x] Logs inspection start/end/failure

- [x] Implement `PageInspector.cs`:
  - [x] Constructor takes `IPage`, `DiagnosticsService`, `ScreenshotService`
  - [x] `InspectAsync(PageInspectOptions options, CancellationToken)` method
  - [x] Calls `InspectPage.js` to get frame hierarchy
  - [x] For main document, calls `InspectElement.js` on root
  - [x] For each iframe: navigates context and inspects frame root (if `IncludeFrames=true`)
  - [x] Recursively traces shadow DOM if enabled
  - [x] Returns `PageInspectionReport`
  - [x] Similar logging and error handling as `ElementInspector`

- [x] Implement `InspectionSerializer.cs`:
  - [x] `ExportJsonAsync(object report, string? filename)` method
  - [x] Uses `System.Text.Json` or `Newtonsoft.Json` (per project preference)
  - [x] Saves to `BrowserOptions.InspectionExportDirectory`
  - [x] Returns full path to exported JSON

- [x] Implement helper service `DomTraversalService.cs`:
  - [x] Optional: utility for building element paths, finding ancestors, etc.

- [x] Add to `UIElement.cs`:
  - [x] Implement `InspectAsync(InspectOptions? options, CancellationToken)` method
  - [x] Calls `ElementInspector.InspectAsync()` on itself
  - [x] Returns `InspectionReport`

- [x] Add to `IPageWrapper.cs`:
  - [x] Return type for `InspectPage()` should be `IPageInspector`

- [x] Add to `PageWrapper.cs`:
  - [x] Implement `InspectPage()` -> returns `PageInspector` instance

- [x] Add integration tests for Phase 8:
  - [x] Test element inspection with and without frames
  - [x] Test shadow DOM inspection
  - [x] Test screenshot capture during inspection
  - [x] Test JSON export
  - [x] Test inspection report structure
  - [x] Test with real test HTML pages

**Acceptance Criteria for Phase 8**:
- Element inspection returns correct DOM structure
- Frame URLs and hierarchy captured
- Shadow DOM traversed when enabled
- Computed styles and accessibility data included
- CSS path and XPath calculated correctly
- Screenshots captured and path included
- JSON export produces valid, structured JSON
- Inspection on elements with descendants includes all children
- Integration tests pass

---

### Phase 9: Integration Tests & Multi-Browser Validation
**Goal**: Create end-to-end test scenarios and verify all three browser types.

- [x] Create `tests/WpfAutomation.IntegrationTests/NavigationTests.cs`:
  - [x] Test navigate to public test pages (e.g., example.com, playwright.dev)
  - [x] Test navigation failure handling
  - [x] Test retry logic with deliberate failures
  - [x] Test for all three browser types
  - [x] Test URL property after navigation

- [x] Create `tests/WpfAutomation.IntegrationTests/InteractionTests.cs`:
  - [x] Test click, fill, type on form elements
  - [x] Test checkbox/radio selection
  - [x] Test select dropdowns
  - [x] Test visibility/enabled state queries
  - [x] Test failure cases (element not found, timeout)
  - [x] Test for all three browser types

- [x] Create `tests/WpfAutomation.IntegrationTests/FrameInspectionTests.cs`:
  - [x] Create test HTML with iframes
  - [x] Test `PageInspector` captures both main and frame content
  - [x] Test frame hierarchy preserved
  - [x] Test interaction within frames

- [x] Create `tests/WpfAutomation.IntegrationTests/ShadowDomInspectionTests.cs`:
  - [x] Create test HTML with shadow DOM
  - [x] Test `ElementInspector` traverses shadow roots
  - [x] Test shadow host identification
  - [x] Test nested shadow DOM

- [x] Create test utilities:
  - [x] Test web server or fixture for serving test HTML
  - [x] Helper methods for creating common test DOM structures
  - [x] Assertion helpers for inspection reports

- [x] Verify multi-browser support:
  - [x] Run key tests against Chromium, Firefox, WebKit
  - [x] Document any browser-specific quirks

- [x] Add smoke tests:
  - [x] Basic WPF startup test
  - [x] Basic automation flow (launch, navigate, search, interact)
  - [x] Cancellation test

**Acceptance Criteria for Phase 9**:
- All navigation scenarios tested
- All interaction scenarios tested
- Frame scenarios tested
- Shadow DOM scenarios tested
- Tests pass on Chromium, Firefox, WebKit
- Smoke tests verify end-to-end workflows
- Integration test suite documentation

---

### Phase 10: Polish, Documentation & Deployment Readiness
**Goal**: Finalize code, add documentation, and prepare for use.

- [x] Code cleanup and review:
  - [x] Remove placeholder methods/TODOs
  - [x] Review error messages for clarity
  - [x] Ensure consistent naming
  - [x] Add XML doc comments to public APIs

- [x] Create or update `README.md`:
  - [x] Quick-start guide
  - [x] Fluent API usage examples
  - [x] Inspection examples
  - [x] WPF host application screenshot/walkthrough
  - [x] Building and running instructions

- [x] Create CONTRIBUTING.md:
  - [x] Code style guidelines
  - [x] Test requirements
  - [x] How to add new search methods

- [x] Create example test scenarios:
  - [x] Example: Navigate to GitHub, search for a repo, click issues
  - [x] Example: Page inspection report walkthrough

- [x] Set up build pipeline (if needed):
  - [x] Build script that compiles all projects
  - [x] Test runner script

- [x] Performance baseline:
  - [x] Measure typical action latency (click, navigate)
  - [x] Measure inspection report size
  - [x] Document expectations

- [x] Finalize `.gitignore`:
  - [x] Exclude bin, obj, .vs, .user files
  - [x] Exclude screenshot/export directories

- [x] Add optional: GitHub Actions workflow
  - [x] CI pipeline for builds and tests

**Acceptance Criteria for Phase 10**:
- All code documented
- README provides clear guidance
- Example scenarios work as documented
- Build process is repeatable
- Repository is clean and ready for collaboration

---

## Acceptance Criteria (Overall)

The solution is accepted when:

### Core Functionality
- [x] Fluent navigation works (entry point → navigate → page)
- [x] Search API locates elements (ById, ByCss, ByRole, etc.)
- [x] UIElement actions execute (click, fill, type, visibility checks)
- [x] All actions apply timeout and retry logic from BrowserOptions
- [x] Screenshots captured on failure
- [x] Cancellation stops active execution cleanly
- [x] Logs are comprehensive and real-time in WPF

### Inspection Subsystem
- [x] Element inspection returns full DOM subtree
- [x] Element inspection includes CSS classes and attributes
- [x] Element inspection includes computed styles
- [x] Element inspection generates CSS path and XPath
- [x] Element inspection detects shadow hosts and includes shadow DOM
- [x] Page inspection returns all iframe content
- [x] Page inspection maintains frame hierarchy and URLs
- [x] Inspection optional screenshot captured
- [x] Inspection JSON export valid and complete

### Error Handling
- [x] Failures wrapped with context (URL, selector, timeout, screenshot)
- [x] Exception messages are clear and actionable
- [x] All exception types documented

### WPF Application
- [x] URL input, Start/Stop buttons functional
- [x] Logs displayed in real-time
- [x] UI remains responsive during execution
- [x] Status shows current operation
- [x] Future-ready placeholder for inspection viewer

### Multi-Browser Support
- [x] Works with Chromium, Firefox, WebKit
- [x] No browser-specific leaks in API
- [x] Tests pass on all three browsers

### Architecture
- [x] WPF concerns isolated in `WpfAutomation.App`
- [x] Automation logic isolated in `WpfAutomation.Core`
- [x] All major components behind interfaces
- [x] Async-first throughout
- [x] MVVM pattern in WPF layer
- [x] No global static configuration

---

## Open Questions / Assumptions

1. **Test HTML Fixtures**: Will use a simple local HTTP server or embedded HTML strings for integration tests. Final choice deferred to implementation.

2. **JSON Library**: No preference specified between `System.Text.Json` and `Newtonsoft.Json`. Implementation to choose based on performance/feature needs.

3. **Inspection JavaScript Bundling**: JavaScript snippets will be stored as .js files and embedded as resources in the assembly for easy deployment.

4. **WPF Logging Binding**: Logs will be bound via `ObservableCollection<LogItem>` in the ViewModel. Real-time updates via `UiDispatcherService` to marshal async results to UI thread.

5. **Screenshot Storage**: Screenshots will be saved to disk in the directory specified by `BrowserOptions.ScreenshotDirectory`. Filenames will include timestamp to avoid collisions.

6. **Frame Traversal Edge Cases**: The inspection subsystem will document known limitations (e.g., cross-origin iframe content may not be inspectable without proper permissions).

7. **Accessibility Inspection**: Phase 8 will include basic ARIA attribute capture. A future phase could expand to full AXAPI integration.

8. **Mobile/Device Emulation**: Out of scope for v1 per PRD. Can be added post-v1 if needed.

