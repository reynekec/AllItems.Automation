# Status Bar Plan

## Problem Statement

The WPF host currently exposes a single trailing status text in the main window instead of a reusable status bar system. The goal is to introduce a VS Code-style status bar for the WPF application that supports icon and text presentation, tooltips, clickable items, and design-time placement on the left or right side. The result should be reusable across the app, visually close to VS Code, and aligned with the repository's existing MVVM, dependency injection, and resource dictionary patterns.

## Proposed Approach

Build a reusable status bar subsystem inside `WpfAutomation.App` rather than a one-off `MainWindow` footer. Model each item as a view-model-friendly descriptor/state object with stable identity, placement (`Left` or `Right`), display text, optional icon glyph/token, tooltip, enabled/visible state, and command binding. Render the bar through a dedicated reusable control or view that projects two ordered item collections, following the same left/right collection pattern already used by the docking subsystem.

Styling should be centralized in XAML resources so the control can closely mimic VS Code's visual language without scattering brushes and templates across windows. Composition should follow existing application patterns:

- Dependency injection in `App.xaml.cs`
- `INotifyPropertyChanged`-based models/view models
- `ObservableCollection<T>` for UI-bound collections
- `ICommand` via existing `RelayCommand` / `AsyncRelayCommand`
- Optional persistence and state services shaped similarly to the existing docking layout services if placement/order state needs to expand later

Because the confirmed scope is design-time left/right placement only, the initial implementation should not include end-user drag/drop, context-menu relocation, or persisted runtime customization. The plan should still leave the model extensible enough that item ordering and placement can evolve later without replacing the control contract.

## Phases

### Phase 1: Define Status Bar Domain Model

- [x] Identify the minimum reusable API for a status bar item: stable ID, text, icon, tooltip, command, command parameter, placement, visibility, enabled state, and optional priority/order.
- [x] Introduce a placement enum or equivalent type for `Left` / `Right` alignment.
- [x] Decide whether item state lives as immutable descriptors, mutable state objects, or a descriptor-plus-state split based on how the docking models and main view models currently handle UI state.
- [x] Define ordering semantics so multiple items on the same side render predictably.
- [x] Specify how icon data is represented in WPF: glyph string, geometry, image source, or a small app-local icon abstraction.
- [x] Document how clickable items map to existing `ICommand` usage patterns.

### Phase 2: Build Reusable Status Bar UI Surface

- [x] Add a dedicated reusable status bar view/control under `WpfAutomation.App` rather than embedding ad hoc markup in `MainWindow.xaml`.
- [x] Expose dependency properties or bindable inputs for the full item source and any derived left/right projections needed by the view.
- [x] Implement a layout that mirrors VS Code structure: full-width bottom bar, left-aligned item strip, flexible center spacer, right-aligned item strip.
- [x] Use item templates that support icon-only, text-only, and icon-plus-text items without duplicating markup.
- [x] Ensure each item supports tooltip display, hover state, pressed state, disabled state, and keyboard-focus visuals.
- [x] Keep the control compatible with existing MVVM binding patterns and avoid code-behind orchestration except for control-only concerns.

### Phase 3: Apply VS Code-Style Visual Design

- [x] Add centralized colors, brushes, typography, spacing, and button/item styles in a resource dictionary, following the same style organization pattern already used in `Docking/DockingStyles.xaml`.
- [x] Define a visual treatment close to VS Code: compact height, colored background strip, high-contrast foregrounds, hover affordances, and crisp separators/padding.
- [x] Standardize icon sizing, text spacing, and hit-target sizing so short status items remain readable and clickable.
- [x] Ensure the design works in the current application shell without breaking existing margins, resizing behavior, or minimum window sizes.
- [x] Validate tooltip timing and appearance for icon-only items where the tooltip carries the full meaning.

### Phase 4: Integrate with Main Window and View Models

- [x] Replace the current single `Status` text footer in `MainWindow.xaml` with the reusable status bar surface.
- [x] Extend `MainViewModel` to expose a status bar item collection or a dedicated status bar view model instead of only a raw status string.
- [x] Map current run-state signals into richer status items, for example a primary left-side status item and optional right-side contextual items.
- [x] Decide which existing commands or app signals should surface as clickable status bar actions in the first iteration.
- [x] Register any new services/view models in `App.xaml.cs` using the same DI approach already present for `MainViewModel`, docking services, and UI services.
- [x] Keep existing automation flow behavior unchanged while only upgrading presentation and command surfacing.

### Phase 5: Test Coverage and Verification

- [x] Add or update unit tests around the view model/item projection logic so left/right placement, ordering, visibility, and enabled-state transitions are verifiable without UI automation.
- [x] Extend WPF-facing tests to validate that status updates still occur during run-state transitions after replacing the raw `Status` footer model.
- [x] Add tests for command invocation from status bar items where logic is view-model-driven.
- [x] Verify icon/text/tooltip combinations do not regress the no-action and disabled-action cases.
- [x] Perform a manual UI verification pass in the host window to confirm visual fidelity, click behavior, resizing, and alignment at typical window widths.

### Phase 6: Hardening and Follow-Through

- [x] Review whether status bar resources belong in `App.xaml` merged dictionaries or a localized control-level dictionary based on reuse expectations.
- [x] Confirm naming conventions and folder placement remain consistent with current `Views`, `Models`, `Commands`, and `Docking` organization.
- [x] Update any relevant documentation or tracking notes if the new subsystem becomes a first-class reusable UI surface.
- [x] Capture deferred enhancements explicitly instead of mixing them into the first implementation, such as runtime relocation, overflow behavior, compact mode, theming variants, or persisted customization.

## Acceptance Criteria

- [x] The WPF app has a reusable status bar component rather than a `MainWindow`-only footer text block.
- [x] Status bar items can display icon, text, or both.
- [x] Status bar items support tooltips and clickable commands.
- [x] Items render on the left or right side based on design-time configuration from code/view models.
- [x] The status bar visually resembles VS Code closely enough to be recognizably inspired by it.
- [x] Existing run-state updates remain visible through the new status bar presentation.
- [x] The solution follows existing repository patterns for MVVM, DI registration, commands, observable collections, and centralized XAML styling.
- [x] Automated coverage exists for the non-visual logic that drives item placement, ordering, and command/state behavior.

## Open Questions / Assumptions

Assumptions confirmed for this plan:

- [x] Left/right placement is design-time only in the first implementation; end users do not move items at runtime.
- [x] No persistence is needed for status bar placement in the first implementation because placement is not user-customizable.
- [x] The status bar should be reusable across the WPF app, not limited to `MainWindow`.
- [x] The desired outcome is a close visual mimic of VS Code's status bar rather than only functional similarity.

Open design questions to resolve during implementation, not before starting:

- [x] Which icon source should be standardized for the app: text glyphs, vector geometries, or packaged image assets.
- [x] Whether the first iteration should expose a single collection with placement metadata or separate left/right collections at the public binding boundary.
- [x] Which initial clickable actions belong in the bar beyond the current run-state indicator.
- [x] Whether a dedicated `StatusBarViewModel` improves separation enough to justify it over extending `MainViewModel` directly.