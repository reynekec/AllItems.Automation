# DockPanels Implementation Plan

## Problem Statement
Create a reusable WPF UserControl that supports dockable panels with a Visual Studio 2026-like experience, then add a MainWindow test entry point that opens a new TestDockWindow for iterative validation.

Requested outcomes:
- A new dockable-panels UserControl (custom implementation from scratch)
- A MainWindow button that opens TestDockWindow
- Single-instance, non-modal TestDockWindow behavior
- UX and visual style close to VS 2026
- v1 feature scope includes:
  - Drag docking to left/right/top/bottom/center tab targets
  - Auto-hide pin/unpin
  - Floating windows
  - Tab groups and tab reordering
  - Layout save/restore
- Verification scope includes unit tests for layout/state logic plus manual UI verification

## Proposed Approach
Implement a dedicated docking subsystem in WpfAutomation.App centered around a reusable dock host UserControl and stateful docking models/services, with MVVM command wiring for launch/test workflows.

Key decisions:
- Build custom docking behavior (no third-party docking library)
- Keep WPF concerns in WpfAutomation.App and follow existing MVVM + command patterns
- Separate docking state/model logic from visuals so core behavior is unit-testable
- Introduce VS-like theme tokens and control templates scoped to docking surfaces
- Provide a dedicated TestDockWindow as a sandbox host for the new control

Planned architecture slices:
- Docking domain models: panel, group, zone, split, float host, auto-hide state, layout snapshot
- Docking services: drag orchestration, drop target resolution, layout serialization
- Docking UI: DockHost UserControl + panel/tab templates + adorners/overlays
- Host integration: TestDockWindow + MainWindow launch command + DI registration

## Phases

### Phase 1: Foundation and Docking Domain Model
- [x] Define docking model types (panel descriptor, dock region, tab group, split node, floating host, auto-hide strip item)
- [x] Define immutable layout snapshot DTOs for persistence (JSON-friendly)
- [x] Add layout-state service interfaces and in-memory implementation
- [x] Add command/event contracts for panel lifecycle (open/close/activate/pin/float/dock)
- [x] Document invariants (single active tab per group, valid split ratios, unique panel IDs)

### Phase 2: DockHost UserControl Skeleton
- [x] Create DockHost UserControl shell with root layout regions and template parts
- [x] Add bindable dependency properties for panel source, active layout, and command hooks
- [x] Implement panel rendering with tab headers/content presenters
- [x] Add splitter support for docked regions with min/max constraints
- [x] Add empty-state and fallback visuals for unassigned regions

### Phase 3: Drag-Dock Engine and Drop Target UX
- [x] Implement drag start and drag payload generation from tab headers/panels
- [x] Implement drop target resolver for edge docks and center-tab docking
- [x] Add dock-preview overlay/adorners to show insertion feedback
- [x] Implement commit logic for docking operations (left/right/top/bottom/center)
- [x] Add tab reordering within group and move-between-group behavior

### Phase 4: Auto-Hide and Floating Window Support
- [x] Implement pin/unpin state transitions and auto-hide strip rendering
- [x] Implement hover/click reveal behavior for auto-hidden panels
- [x] Create floating panel window host and ownership/lifetime handling
- [x] Implement drag from floating back to dock host
- [x] Preserve focus/activation semantics when switching between docked and floating states

### Phase 5: Layout Persistence and Restore
- [x] Implement serialization/deserialization for layout snapshot DTOs
- [x] Add versioned layout schema and migration-safe defaults
- [x] Implement save-on-change policy with throttling/debounce
- [x] Implement startup restore and fallback when layout is invalid/corrupt
- [x] Add explicit reset-to-default-layout action for testing

### Phase 6: TestDockWindow and MainWindow Integration
- [x] Create TestDockWindow and corresponding ViewModel for sandbox scenarios
- [x] Seed representative sample panels/content for validation
- [x] Add MainViewModel command to open TestDockWindow in single-instance non-modal mode
- [x] Add MainWindow button bound to the new command
- [x] Register window launcher/service dependencies in App.xaml.cs DI setup

### Phase 7: VS 2026-like Styling and Interaction Polish
- [x] Add docking-specific resource dictionary with color/spacing/typography tokens
- [x] Style tabs, headers, splitters, overlays, pins, and float chrome to match VS-like affordances
- [x] Add pointer/keyboard interaction polish (hover states, focus visuals, keyboard tab traversal)
- [x] Add high-DPI checks and responsive sizing behavior for desktop window resizing
- [x] Validate consistency with existing app styling boundaries

### Phase 8: Verification and Hardening
- [x] Add unit tests for layout transforms (dock, undock, float, pin/unpin, reorder)
- [x] Add unit tests for persistence round-trip and invalid-layout fallback
- [x] Add unit tests for single-instance TestDockWindow launch behavior via ViewModel/service seams
- [ ] Execute manual verification checklist for drag/drop targets, auto-hide, floating, and restore flows
- [x] Capture and triage UX or regression issues; apply fixes before completion

## Acceptance Criteria
1. A reusable dock host UserControl exists in WpfAutomation.App and supports dock operations for left/right/top/bottom/center targets.
2. Panels support tabbed grouping with reordering and moving tabs between groups.
3. Panels can be pinned/unpinned to auto-hide strips and reopened through strip interactions.
4. Panels can be floated into separate windows and re-docked into the main host.
5. Layout can be saved and restored across app sessions, including docked/floating/auto-hide state.
6. MainWindow contains a test button that opens TestDockWindow via MVVM command wiring.
7. TestDockWindow opening behavior is non-modal and single-instance (focus existing window if already open).
8. Docking visuals and interactions are a close approximation of Visual Studio 2026 dockable panel UX.
9. Unit tests cover core layout/state logic and persistence behavior; manual checklist confirms end-to-end UI behavior.
10. Implementation follows existing repository patterns for DI, commands, ViewModels, and WPF resource dictionaries.

## Open Questions / Assumptions
Assumptions confirmed:
- Docking implementation is custom-built from scratch (no AvalonDock or alternative docking library).
- v1 includes full requested docking scope (edge/center docking, auto-hide, floating windows, tab groups/reorder, layout persistence).
- Visual goal is close approximation rather than pixel-perfect VS 2026 cloning.
- MainWindow launcher uses MVVM command pattern.
- TestDockWindow behavior is non-modal single-instance.
- Verification requires unit tests for logic/persistence plus manual UI validation.

No unresolved blocking questions remain for implementation planning.