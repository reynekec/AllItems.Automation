# WpfAutomation Integration Tests

This project validates end-to-end automation behavior against real Playwright browser sessions.

## Coverage

- Navigation success/failure/retry scenarios
- Interaction scenarios (click, fill, type, checkbox/radio/select)
- Frame inspection behavior
- Shadow DOM inspection behavior
- Smoke coverage for app startup surface, base automation flow, and cancellation

## Multi-browser

Core navigation and interaction paths are executed for Chromium, Firefox, and WebKit.
If a browser binary is unavailable in the environment, the related test exits early.

## Running

Use the repository-level validation command:

```powershell
dotnet build WpfAutomation.sln
dotnet test WpfAutomation.sln --no-build
```
