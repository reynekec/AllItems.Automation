# Action Catalog Framework Plan

## Problem Statement

`MainViewModel.SeedActionCatalog()` currently hardcodes browser action metadata (IDs, display names, categories, keywords) directly into the WPF ViewModel. This creates three problems:

1. **No execution contract** — actions are display-only records with no relationship to the code that would actually run the automation.
2. **No extensibility seam** — adding new actions requires editing `MainViewModel`; there is no way to ship actions in a separate assembly without touching the app.
3. **No grouping governance** — categories are ad-hoc strings repeated across every action object.

The goal of this plan is to introduce a first-class action catalog framework that:
- Defines a clean `IAutomationAction` contract in `WpfAutomation.Core` so action logic and metadata live together.
- Creates a new `AllItems.Automation.Browser` project containing real, grouped browser automation actions.
- Replaces the hardcoded seed with a reflection-based `ActionCatalogBuilder` that auto-discovers actions from referenced assemblies.
- Keeps the `ActionBrowser` UI feed unchanged — `UiActionCategory` / `UiActionItem` remain the WPF display model.

---

## Proposed Approach

### Key Design Decisions

| Concern | Decision |
|---|---|
| Where `IAutomationAction` lives | `WpfAutomation.Core/Abstractions` — shared by all action projects |
| Action metadata | `ActionMetadata` record on `IAutomationAction.Metadata`; self-described per class |
| Discovery mechanism | Reflection scan of provided assemblies at app startup |
| Catalog builder location | `WpfAutomation.App/Services/ActionCatalogBuilder.cs` (maps Core types → WPF display models) |
| Action execution scope | **Out of scope for this plan** — selection-only at v1; execution plumbing is a subsequent plan |
| First action project | `AllItems.Automation.Browser` — 4 groups: Browser, Navigation, Elements, Assertions |
| Future projects | Add project reference + the scan automatically discovers their actions |

### Layering

```
AllItems.Automation.Browser  ──references──►  WpfAutomation.Core  (IAutomationAction / ActionMetadata)
         │
         └── referenced by ──►  WpfAutomation.App
                                      │
                                ActionCatalogBuilder  (reflection scan → UiActionCategory tree)
                                      │
                                MainViewModel  (replaces SeedActionCatalog())
```

---

## Phases

### Phase 1: IAutomationAction Contract in WpfAutomation.Core

**Goal**: Add the interface and metadata descriptor that all action projects will implement.

- [x] Create `WpfAutomation.Core/Abstractions/Actions/` subfolder.
- [x] Add `ActionMetadata.cs` — immutable record with:
  - `ActionId` (string, unique slug e.g. "navigate-to-url")
  - `DisplayName` (string, shown in ActionBrowser)
  - `CategoryId` (string, slug e.g. "navigation")
  - `CategoryName` (string, display label e.g. "Navigation")
  - `IconKeyOrPath` (string, icon resource key or path)
  - `Keywords` (IReadOnlyList\<string\>, for sidebar search)
  - `SortOrder` (int, controls ordering within a category; default 0)
- [x] Add `IAutomationAction.cs` interface with a single `ActionMetadata Metadata { get; }` property.
- [x] Ensure both types are in namespace `WpfAutomation.Core.Abstractions.Actions`.
- [x] Verify `WpfAutomation.Core` still builds without errors.

**Acceptance Criteria**:
- `ActionMetadata` and `IAutomationAction` compile in `WpfAutomation.Core`.
- No existing files in `WpfAutomation.Core` are changed except adding new files.

---

### Phase 2: AllItems.Automation.Browser Project Scaffold

**Goal**: Create the new action project with its four category groups.

- [x] Add `AllItems.Automation.Browser` class library project targeting `net9.0`.
- [x] Add project reference to `WpfAutomation.Core`.
- [x] Add project to `WpfAutomation.sln` and `WpfAutomation.slnx`.
- [x] Create folder structure:
  - `AllItems.Automation.Browser/Actions/Browser/`
  - `AllItems.Automation.Browser/Actions/Navigation/`
  - `AllItems.Automation.Browser/Actions/Elements/`
  - `AllItems.Automation.Browser/Actions/Assertions/`
- [x] Implement `IAutomationAction` for each action below. Each class file lives in is appropriate folder. All classes are `public sealed`.

**Browser group** (`CategoryId = "browser"`, `CategoryName = "Browser"`):
| ActionId | DisplayName | Keywords |
|---|---|---|
| `open-browser` | Open browser | launch, start, chromium, firefox, webkit |
| `close-browser` | Close browser | quit, exit, dispose |
| `new-page` | Create page | tab, new, page |

**Navigation group** (`CategoryId = "navigation"`, `CategoryName = "Navigation"`):
| ActionId | DisplayName | Keywords |
|---|---|---|
| `navigate-to-url` | Navigate to URL | goto, url, visit, open |
| `wait-for-url` | Wait for URL | wait, route, match |
| `go-back` | Go back | back, history, previous |
| `go-forward` | Go forward | forward, history, next |
| `reload-page` | Reload page | refresh, reload |

**Elements group** (`CategoryId = "elements"`, `CategoryName = "Elements"`):
| ActionId | DisplayName | Keywords |
|---|---|---|
| `click-element` | Click element | click, tap, press |
| `fill-input` | Fill input | type, input, text, enter |
| `select-option` | Select option | dropdown, select, option, choose |
| `hover-element` | Hover element | hover, over, mouse |
| `press-key` | Press key | keyboard, key, shortcut |

**Assertions group** (`CategoryId = "assertions"`, `CategoryName = "Assertions"`):
| ActionId | DisplayName | Keywords |
|---|---|---|
| `expect-text` | Expect text | text, contains, assert, verify |
| `expect-visible` | Expect visible | visible, present, assert, verify |
| `expect-hidden` | Expect hidden | hidden, absent, not visible |
| `expect-enabled` | Expect enabled | enabled, active, interactive |

- [x] Verify `AllItems.Automation.Browser` builds without errors.

**Acceptance Criteria**:
- 16 action classes exist, each implementing `IAutomationAction`.
- Each class returns a fully populated `ActionMetadata` from `Metadata`.
- Project builds cleanly with no warnings.

---

### Phase 3: ActionCatalogBuilder in WpfAutomation.App

**Goal**: Replace the hardcoded `SeedActionCatalog()` with a reflection-based discovery service.

- [x] Add `WpfAutomation.App/Services/IActionCatalogBuilder.cs` interface:
  ```csharp
  public interface IActionCatalogBuilder
  {
      IReadOnlyList<UiActionCategory> Build(IEnumerable<Assembly> assemblies);
  }
  ```
- [x] Add `WpfAutomation.App/Services/ActionCatalogBuilder.cs` implementation:
  - Scans each provided `Assembly` for all non-abstract types implementing `IAutomationAction`.
  - Instantiates each type (parameterless constructor) and reads `Metadata`.
  - Groups by `CategoryId` / `CategoryName`, ordered by `SortOrder` within each group.
  - Groups themselves are ordered by the minimum `SortOrder` of their first member, then alphabetically.
  - Returns `IReadOnlyList<UiActionCategory>` using the existing WPF display models.
  - Uses `IReadOnlyList<string>` for keywords (aligns with `UiActionItem.Keywords`).
- [x] Add `using` for `System.Reflection` and `WpfAutomation.Core.Abstractions.Actions`.
- [x] Register `IActionCatalogBuilder` → `ActionCatalogBuilder` in the DI container inside `App.xaml.cs`.

**Acceptance Criteria**:
- `ActionCatalogBuilder.Build()` returns correctly grouped `UiActionCategory` objects when given the `AllItems.Automation.Browser` assembly.
- No hardcoded action data in the builder — metadata comes entirely from reflection.
- Builder handles assemblies with zero `IAutomationAction` implementations gracefully (returns empty groups, no exception).

---

### Phase 4: Wire Up MainViewModel

**Goal**: Remove the hardcoded seed and replace it with the catalog builder.

- [x] Add `<ProjectReference>` from `WpfAutomation.App` to `AllItems.Automation.Browser`.
- [x] Inject `IActionCatalogBuilder` into `MainViewModel` constructor.
- [x] Replace the call to `SeedActionCatalog()` with:
  ```csharp
  var assemblies = new[] { typeof(OpenBrowserAction).Assembly };
  var categories = _actionCatalogBuilder.Build(assemblies);
  foreach (var category in categories)
      _actionCatalog.Add(category);
  ```
- [x] Delete the private `SeedActionCatalog()` method from `MainViewModel`.
- [x] Update DI registration in `App.xaml.cs` to pass `IActionCatalogBuilder` to `MainViewModel`.
- [x] Build and run: confirm ActionBrowser displays the 4 groups with all 16 actions.

**Acceptance Criteria**:
- `MainViewModel` contains no hardcoded action strings.
- The ActionBrowser sidebar shows all 4 groups (Browser, Navigation, Elements, Assertions) at runtime.
- The 3 previously hardcoded actions (open-browser, create-page, navigate-to-url, wait-for-url, click-element, fill-input) are still reachable under their correct groups.
- Existing WPF Start/Stop automation workflow remains unaffected.

---

### Phase 5: Tests

**Goal**: Cover the catalog builder and action metadata integrity.

- [x] Add unit tests in `tests/WpfAutomation.Core.Tests` (or a new test file) for:
  - `ActionCatalogBuilder.Build([BrowserAssembly])` returns exactly 4 categories.
  - Each category contains the expected action count.
  - No category contains an action with a null or empty `ActionId`, `DisplayName`, or `CategoryId`.
  - Calling `Build([])` returns an empty list without throwing.
  - Calling `Build([BrowserAssembly, BrowserAssembly])` deduplicates (or documents the behavior).
- [x] Add a smoke test that verifies all 16 `IAutomationAction` implementations in `AllItems.Automation.Browser` have unique `ActionId` values across the assembly.
- [x] Run full solution tests — no regressions.

**Acceptance Criteria**:
- All new tests pass.
- No existing tests are broken.

---

## Acceptance Criteria (Overall)

| # | Criterion |
|---|---|
| AC-1 | `IAutomationAction` and `ActionMetadata` exist in `WpfAutomation.Core.Abstractions.Actions`. |
| AC-2 | `AllItems.Automation.Browser` compiles as a standalone class library referencing only `WpfAutomation.Core`. |
| AC-3 | All 16 browser actions are implemented and discoverable via reflection. |
| AC-4 | `ActionCatalogBuilder` discovers and groups actions from any assembly containing `IAutomationAction` implementations. |
| AC-5 | `MainViewModel` contains no hardcoded action data. |
| AC-6 | The ActionBrowser UI displays 4 groups with correct actions and search works as before. |
| AC-7 | Adding a future action project only requires: (a) create a class implementing `IAutomationAction`, (b) add project reference, (c) include the assembly in the `Build()` call. |
| AC-8 | All unit tests pass; no regressions in existing integration tests. |

---

## Open Questions / Assumptions

### Confirmed Assumptions
- `IAutomationAction` and `ActionMetadata` live in `WpfAutomation.Core/Abstractions/Actions/`.
- Discovery is via reflection — parameterless constructors are required on all action classes (enforced by convention, not compile check).
- Action execution (running the actual Playwright calls) is **out of scope** — clicking an action populates selection only.
- First action project is `AllItems.Automation.Browser` with 4 groups: Browser, Navigation, Elements, Assertions.
- The existing `UiActionCategory` / `UiActionItem` WPF display models are unchanged.
- `WpfAutomation.App` references `AllItems.Automation.Browser` at compile time; the assembly list passed to `Build()` is explicit (not a plugin/MEF scan).
- Target framework for `AllItems.Automation.Browser` is `net9.0` (matching rest of solution).

### Open Items for Implementation
- Whether `ActionCatalogBuilder` should catch and log instantiation failures (e.g., action class lacking parameterless constructor) rather than throw.
- Icon key conventions — current `UiActionItem.IconKeyOrPath` is a string key; the resource dictionary may not have entries for all 16 actions. Icons can be left as key stubs for now.
- Whether `SortOrder` should be an attribute or specified inline on the `ActionMetadata` record property. Inline on `ActionMetadata` is simplest.
