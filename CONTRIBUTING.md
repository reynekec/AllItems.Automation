# Contributing

## Code Style Guidelines

- Follow existing project naming and folder conventions.
- Keep WPF-specific concerns in `AllItems.Automation.Browser.App`.
- Keep Playwright-facing automation in `AllItems.Automation.Browser.Core`.
- Prefer async-first APIs and propagate `CancellationToken` where supported.
- Use wrapped domain exceptions with context (action, url, selector, timeout, screenshot path).
- Keep diagnostics meaningful and consistent (`Info`, `Warn`, `Error`).

## Test Requirements

Before submitting changes:

1. Build the solution:
```powershell
dotnet build WpfAutomation.sln
```
2. Run all tests:
```powershell
dotnet test WpfAutomation.sln --no-build
```
3. If adding inspection/search behavior, include integration coverage under:
- `tests/AllItems.Automation.Core.Tests`
- `tests/AllItems.Automation.IntegrationTests`

## How To Add New Search Methods

1. Add the method contract to `AllItems.Automation.Browser.Core/Abstractions/ISearchContext.cs`.
2. Implement the method in `AllItems.Automation.Browser.Core/Search/SearchContext.cs`.
3. Ensure selector descriptions are diagnostic-friendly.
4. Add unit tests to `tests/AllItems.Automation.Core.Tests/SearchAndElementTests.cs`.
5. Add integration tests in `tests/AllItems.Automation.IntegrationTests/InteractionTests.cs` when applicable.

## Pull Request Notes

- Keep changes scoped to a single phase/feature when possible.
- Avoid unrelated formatting churn.
- Update docs when public APIs or workflows change.
