# FlowNodeInspector Plan

## Problem Statement
The flow canvas currently supports selecting nodes, but the existing `properties` dock panel is not wired to node-specific editing experiences. The goal is to introduce a dynamic, per-node property panel framework that:
- Uses custom UI per action node (not a legacy property grid).
- Preserves a consistent visual language across all node editors.
- Supports categorized property sections with label/value presentation.
- Enables rich, visual-driven behavior where needed.
- Persists node property values in the flow document model.

Scope for this plan includes editor-side architecture, typed property modeling, persistence, validation, and custom property panel UIs for all current 17 actions. Runtime execution binding is intentionally out of scope for this phase.

## Proposed Approach
Build a Node Inspector subsystem in `WpfAutomation.App` that activates when a canvas node is selected and renders a custom UserControl mapped to the selected node's `ActionId`.

Key decisions:
- Panel location: reuse the existing dock panel with `PanelId = "properties"`.
- UI strategy: one custom UserControl per action node.
- Consistency strategy: enforce a shared ResourceDictionary (`NodeInspectorStyles.xaml`) for spacing, typography, category sections, row labels/values, and validation visuals.
- Data strategy: typed parameter records per action, stored on `FlowActionNodeModel` and serialized with the flow document.
- Coverage: all 17 current actions in this phase.
- Runtime mapping: defer execution wiring to a follow-up phase.

Design intent:
- Keep property panel presentation in `WpfAutomation.App` (MVVM + XAML).
- Preserve current boundaries where action metadata is discovered from `AllItems.Automation.Browser` but editor property models stay app-focused.
- Keep persistence compatible with no migration work required now (no existing flows to migrate).

## Phases

### Phase 1: Inspector Foundations and Dock Integration
- [x] Add a `NodeInspectorView` host control and assign it to the `properties` panel in `MainWindow.xaml` via `DockPanelContentTemplateSelector`.
- [x] Add a `SelectedNodeInspector` state to `FlowCanvasViewModel` (or a dedicated inspector VM) driven by `Document.Selection.PrimaryNodeId`.
- [x] Introduce inspector contracts in app layer, for example:
  - `INodeInspectorDescriptor` (node/action identity + display metadata)
  - `INodeInspectorViewModel` (validation state, commands, dirty tracking)
  - `INodeInspectorFactory` (resolve per action id)
- [x] Ensure no selection, edge selection, and container selection states render intentional placeholder views (not blank panels).
- [x] Add diagnostics events for inspector lifecycle: node selected, inspector resolved, validation changed, save/persist failures.

### Phase 2: Typed Node Property Data Model
- [x] Extend `FlowActionNodeModel` with typed property payload support, e.g. `ActionParameters` base + concrete records.
- [x] Add an app-level parameter type set for all 17 action ids (one record per action) with sensible defaults.
- [x] Add a resolver that maps `ActionId` -> parameter record type + default instance.
- [x] Update `FlowEditingService.AddActionNode` to initialize default parameters for newly dropped action nodes.
- [x] Add immutable update helpers so property edits replace records safely and preserve undo/redo behavior.
- [x] Keep container nodes unaffected by this phase.

### Phase 3: Flow Persistence and Snapshot Mapping
- [x] Extend `FlowNodeSnapshot`/mapping to include action parameter payloads.
- [x] Update `FlowSnapshotMapper.ToSnapshot` and `FromSnapshot` to serialize/deserialize typed parameters.
- [x] Add schema version bump for document snapshots only if required by implementation details; keep open logic tolerant for missing parameter payloads.
- [x] Validate save/open round-trip for every action parameter record.
- [x] Ensure existing empty/new documents still load without inspector data.

### Phase 4: Shared Visual Consistency Layer
- [x] Create `NodeInspectorStyles.xaml` with shared tokens/styles for category containers.
- [x] Create `NodeInspectorStyles.xaml` with shared tokens/styles for category headers.
- [x] Create `NodeInspectorStyles.xaml` with shared tokens/styles for property row grids (label/value).
- [x] Create `NodeInspectorStyles.xaml` with shared tokens/styles for inline help text.
- [x] Create `NodeInspectorStyles.xaml` with shared tokens/styles for validation message banners.
- [x] Create `NodeInspectorStyles.xaml` with shared tokens/styles for read-only and value emphasis variants.
- [x] Merge these styles in inspector-related views only (avoid accidental global style bleed).
- [x] Define consistent spacing and typography scale used by all custom property UserControls.
- [x] Create reusable XAML pattern guidance (documented in code comments and naming conventions), while keeping controls custom per node.

### Phase 5: Custom UserControls for All Action Nodes
- [x] Implement a custom inspector UserControl + ViewModel for `open-browser`.
- [x] Implement a custom inspector UserControl + ViewModel for `new-page`.
- [x] Implement a custom inspector UserControl + ViewModel for `close-browser`.
- [x] Implement a custom inspector UserControl + ViewModel for `navigate-to-url`.
- [x] Implement a custom inspector UserControl + ViewModel for `go-back`.
- [x] Implement a custom inspector UserControl + ViewModel for `go-forward`.
- [x] Implement a custom inspector UserControl + ViewModel for `reload-page`.
- [x] Implement a custom inspector UserControl + ViewModel for `wait-for-url`.
- [x] Implement a custom inspector UserControl + ViewModel for `click-element`.
- [x] Implement a custom inspector UserControl + ViewModel for `fill-input`.
- [x] Implement a custom inspector UserControl + ViewModel for `hover-element`.
- [x] Implement a custom inspector UserControl + ViewModel for `press-key`.
- [x] Implement a custom inspector UserControl + ViewModel for `select-option`.
- [x] Implement a custom inspector UserControl + ViewModel for `expect-enabled`.
- [x] Implement a custom inspector UserControl + ViewModel for `expect-hidden`.
- [x] Implement a custom inspector UserControl + ViewModel for `expect-text`.
- [x] Implement a custom inspector UserControl + ViewModel for `expect-visible`.
- [x] Group properties into explicit categories per panel (for example: Target, Timing, Options, Assertions).
- [x] Support visual behaviors per panel (conditional fields, warning hints, compact selectors).
- [x] Keep all panels aligned to shared style tokens for consistent look and interaction rhythm.
- [x] Register each panel in `INodeInspectorFactory` with deterministic fallback for unknown action ids.

### Phase 6: Validation, Commands, and Editing UX
- [x] Add field-level and panel-level validation rules to inspector VMs.
- [x] Show validation in-panel (message regions + row-level cues) using shared styles.
- [x] Define property commit model (immediate update vs apply command) and wire to flow document updates.
- [x] Ensure inspector edits participate in undo/redo stacks through existing `FlowCanvasViewModel` history mechanisms.
- [x] Add cancel/reset-to-default behavior at node level.

### Phase 7: Tests and Quality Gates
- [x] Add unit tests for parameter defaulting and action id -> parameter mapping.
- [x] Add unit tests for `FlowEditingService` node creation with initialized parameters.
- [x] Add persistence tests for serialization/deserialization of all 17 parameter records.
- [x] Add view model tests for validation behavior and update propagation.
- [x] Add integration tests to confirm the `properties` dock panel updates correctly when canvas selection changes.
- [x] Add smoke tests for unknown action fallback and no-selection placeholder.

### Phase 8: Rollout Readiness and Follow-up Hooks
- [x] Add lightweight developer guidance in code comments and naming conventions for adding new action inspectors.
- [x] Add extension points for future runtime execution binding (without implementing it in this phase).
- [x] Verify no regression in action browser drag/drop, selection, and save/open workflows.
- [x] Final pass on UX consistency across all 17 custom panels.

## Acceptance Criteria
1. Selecting an action node always displays a dedicated custom property panel in the existing `properties` dock panel.
2. All 17 current action nodes have custom inspector UIs with categorized sections and label/value rows.
3. Inspector visual structure is consistent across all panels via shared `NodeInspectorStyles.xaml` tokens/styles.
4. New action nodes are initialized with typed default parameter records.
5. Property edits persist in flow save/open round-trips with no data loss.
6. Validation feedback is visible and actionable in each inspector panel.
7. Inspector edits integrate with existing undo/redo behavior.
8. Unknown/unmapped actions and no-selection states show explicit fallback UIs instead of blank content.
9. Runtime execution behavior remains unchanged in this phase.

## Open Questions / Assumptions
Assumptions confirmed for this plan:
- The existing `properties` dock panel is the host surface for node inspectors.
- One fully custom UserControl per action is acceptable and preferred.
- Consistency is enforced through shared XAML styles/resource dictionary, not a shared base control.
- Typed parameter records are stored on action nodes in the flow document model.
- This phase includes all 17 current actions.
- This phase includes validation and persistence, but excludes runtime execution binding.
- No migration workflow is required because there are no existing saved flows to migrate.

Open questions for implementation kickoff:
- Decide immediate-commit vs explicit-apply interaction model per panel (recommended: immediate commit with undo support).
- Confirm whether any action should expose advanced sections collapsed by default.