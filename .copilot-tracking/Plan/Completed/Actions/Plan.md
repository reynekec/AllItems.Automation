# Actions Sidebar Control Plan

## Problem Statement
The WPF host currently provides a basic navigation runner UI, but it does not include a Power Automate-style Actions sidebar. The product needs a reusable sidebar control that visually and behaviorally matches the Power Automate actions pane for v1 scope: searchable grouped actions, expandable categories, action selection callback, drag-and-drop initiation, and pinning support. The parent object (host ViewModel) must provide data and own mutable UI state so the control remains presentation-focused and reusable.

## Proposed Approach
Implement a reusable WPF UserControl inside WpfAutomation.App and integrate it into MainWindow/MainViewModel as the first host. Keep state ownership in MainViewModel and expose all data/commands to the control via dependency properties and bindings.

Key decisions:
- Use MVVM-first bindings and existing ICommand patterns (RelayCommand/AsyncRelayCommand style).
- Parent passes an ObservableCollection of category models; each category contains action item models.
- Sidebar supports live search filtering, category expand/collapse, action click callback, drag start gesture, and pinned actions section.
- Keep visual fidelity close to Power Automate using native WPF styles/templates and resource dictionaries.
- Add tests at ViewModel and UI integration seams consistent with existing tests in tests/WpfAutomation.Core.Tests.

## Phases

### Phase 1: Domain Models and Contracts
- [x] Add UI models for action categories and action items under WpfAutomation.App/Models (display name, icon key/path, category id/name, pinned flag, keywords/tags).
- [x] Add parent-owned state models/properties for expanded categories, search text, filtered projection, and pinned collection.
- [x] Define command contracts for action invoke, category toggle, pin/unpin, and drag initiation metadata.
- [x] Ensure model naming and nullability match existing app conventions.

### Phase 2: Sidebar UserControl Skeleton
- [x] Create reusable UserControl in WpfAutomation.App/Views (for example, ActionsSidebarControl.xaml/.cs).
- [x] Add dependency properties for categories source, search text, selected action, and command bindings.
- [x] Implement control template layout: header, search box, optional pinned section, categorized action list.
- [x] Keep code-behind minimal (visual/event wiring only), with no orchestration logic.

### Phase 3: Behavior Parity Implementation
- [x] Implement live search filtering (case-insensitive, matches title + tags/keywords).
- [x] Implement grouped categories with expand/collapse bound to parent-owned state.
- [x] Implement action click callback command that passes selected action payload to parent.
- [x] Implement drag-and-drop start behavior from action items (payload includes action id/name/category).
- [x] Implement pin/unpin interaction and pinned actions rendering at top of pane.

### Phase 4: Visual Styling and UX Fidelity
- [x] Add resource dictionary styles for Power Automate-like pane spacing, typography, hover states, and separators.
- [x] Style search input, category headers, chevron expand indicators, action row cards, and pinned indicator.
- [x] Add empty states (no results) and loading/disabled visuals where needed.
- [x] Validate responsive behavior for window resize and minimum widths used by MainWindow.

### Phase 5: Parent Integration (MainWindow/MainViewModel)
- [x] Extend MainViewModel with sidebar data source (ObservableCollection category tree) and commands.
- [x] Seed initial action catalog data in MainViewModel (or app-facing service if already present) for first integration.
- [x] Bind MainWindow to the new control and wire callback handling in MainViewModel.
- [x] Ensure existing Start/Stop and log viewer behavior remains unaffected.

### Phase 6: Validation and Tests
- [x] Add ViewModel tests for search filtering behavior, category expand state transitions, and pin/unpin logic.
- [x] Add tests for action invoke command payload integrity.
- [x] Add tests (or integration harness assertions) for drag payload composition.
- [x] Run full solution tests and verify no regressions in existing WPF integration tests.

### Phase 7: Documentation and Follow-through
- [x] Update inline XML docs/comments for new sidebar models/commands where intent is non-obvious.
- [x] Add a brief section to project docs describing parent data contract and integration steps.
- [x] Capture known parity gaps vs full Power Automate UI (if any) for future iterations.

## Acceptance Criteria
- Sidebar is implemented as a reusable UserControl and integrated in MainWindow.
- Parent ViewModel supplies categories/actions via ObservableCollection and owns mutable UI state.
- Search filters actions live without blocking UI and supports case-insensitive keyword matching.
- Categories can be expanded/collapsed, with state persisted in parent ViewModel during session.
- Clicking an action triggers parent callback command with the correct action payload.
- Drag-and-drop can be initiated from an action item with a defined payload contract.
- Users can pin/unpin actions and see pinned actions grouped at the top.
- Visual presentation is a close Power Automate-style match using native WPF resources.
- Existing Start/Stop automation workflow and log display continue to work.
- New tests for sidebar behaviors pass, and existing solution tests remain green.

## Open Questions / Assumptions
Assumptions confirmed:
- Implement as reusable UserControl in WpfAutomation.App.
- First-host integration is MainWindow/MainViewModel.
- Parent passes ObservableCollection category/action data model.
- Parent ViewModel owns mutable state (expanded categories, pinned state, search state).
- v1 parity includes live search, grouped expand/collapse, action callback, drag-and-drop initiation, and favorites/pinned actions.
- Visual target is close functional + visual match to Power Automate using native WPF styles.

Open questions for implementation-time refinement:
- Exact icon asset source and format for action items (vector geometry, font glyph, or image files).
- Preferred drag payload consumer in downstream flow canvas (if not yet implemented).
- Whether pinned order is manual or fixed (for example, alphabetical).
