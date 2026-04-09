# Copilot Instructions

## Current repository state

- This repository is currently planning-first. `README.md` only contains the repository name, and `WPF_Playwright_PRD.md` contains the substantive product, architecture, and structure guidance.
- Before proposing implementation details, re-scan the repository for the planned `src/` and `tests/` trees instead of assuming the PRD has already been scaffolded into code.

## Build, test, and lint

- No runnable build, test, or lint commands are defined in the current repository state. There is no checked-in `.sln`, `.csproj`, test project, or lint configuration yet.
- When solution files are added, inspect the actual project and test files first, then update this document with real commands, including the repository's single-test workflow.

## High-level architecture

- The intended solution is a .NET 10 WPF host application plus a reusable automation core library that wraps `Microsoft.Playwright` behind a fluent, domain-style API.
- The planned top-level split is:
  - `WpfAutomation.App`: WPF UI, view models, commands, app-facing services, and UI state.
  - `WpfAutomation.Core`: browser lifecycle, page abstraction, search, UI element actions, inspection, diagnostics, configuration, exceptions, and report models.
  - `tests`: core unit tests plus integration tests for navigation, interaction, frame inspection, and shadow DOM inspection.
- The key application boundary is `AutomationOrchestrator`: the WPF layer issues commands through MVVM, and the orchestrator bridges those commands into browser sessions and automation flows in the core library.
- Core browser automation should stay behind abstractions such as `IBrowserLauncher`, `IPageWrapper`, `ISearchContext`, `IUIElement`, `IPageInspector`, and `IElementInspector` rather than leaking raw Playwright types across the app.
- Inspection is a first-class subsystem, not an add-on. It is expected to traverse the main DOM, iframes, and shadow DOM, then return structured report objects that can be shown in WPF and optionally exported to JSON.
- Diagnostics are cross-cutting. Navigation, search, interaction, inspection, cancellation, and failures are all expected to flow through centralized logging and screenshot capture behavior.

## Key conventions

- Follow the fluent usage style described in the PRD: open a browser, navigate to a page, search from the page, then act on an `IUIElement`. Keep the public API task-based and chain-friendly.
- Keep WPF-specific concerns inside `WpfAutomation.App`. Keep Playwright-facing automation logic inside `WpfAutomation.Core`.
- Use MVVM for the WPF host. Avoid code-behind orchestration except for minimal view wiring.
- Treat the codebase as async-first: use `async`/`await` throughout automation flows, keep the WPF UI responsive, and preserve cancellation support across long-running operations.
- Keep configuration session-scoped through objects like `BrowserOptions`; do not introduce global static configuration for automation behavior.
- Use wrapped exceptions with enriched context. Failures should carry the action name, URL, selector or logical search description, timeout, screenshot path when available, and inner exception details.
- Log every major automation step: browser launch, navigation, search, element action, inspection start/end, failure, and cancellation.
- Keep inspection JavaScript isolated under `Inspection/JavaScript`; keep report types DTO-style and serializable.
- Preserve frame and shadow DOM support in both implementation and tests. The PRD treats iframe traversal, shadow DOM traversal, and multi-browser support (`Chromium`, `Firefox`, `WebKit`) as core behavior, not optional extras.
