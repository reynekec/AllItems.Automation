# Canvas Automation Flow Plan

## Problem Statement
The application currently supports selecting and dragging actions from the sidebar, but it does not provide a visual flow canvas where users can compose executable automation flows. We need a canvas user control that supports draggable action nodes, collapsible/expandable containers with nested nodes, dynamic container resizing, visual connection lines, and insertion between connected nodes by dropping over a line with clear affordance (plus indicator).

The first implementation must prioritize robust authoring UX in WPF while producing a stable flow model that can later be executed by the automation orchestrator.

## Proposed Approach
Implement a graph-style authoring surface in `WpfAutomation.App` using native WPF (`Canvas` + `ItemsControl` + adorner/overlay layers), consistent with the existing MVVM and command patterns in the app.

Key decisions:
- Keep Playwright/runtime execution out of this phase, but define flow DTO/contracts now so execution mapping can be added without model refactors.
- Use a primary top-to-bottom lane as the canonical ordering model (root lane plus container-local lanes).
- Support typed containers now: Group, Loop, Condition (Condition includes branch lanes in model/UI).
- Keep edge rendering and insertion UX deterministic: line hover => plus indicator => edge split on drop.
- Add editing fidelity in this phase: undo/redo, multi-select/group move, copy/paste.
- Add JSON save/load for flow definitions.

## Phases

### Phase 1: Domain Model and Contracts
- [x] Define flow graph models under app models namespace for:
- [x] Node base metadata (`NodeId`, display label, action binding metadata, collapsed state, bounds).
- [x] Action node model referencing sidebar action identity (`ActionId`, `CategoryId`, etc.).
- [x] Container node model with typed container kinds (Group, Loop, Condition), child lane collections, and expansion state.
- [x] Edge model (`FromNodeId`, `ToNodeId`, `FromPort`, `ToPort`, optional lane metadata).
- [x] Root flow document model (schema version, canvas viewport metadata, node/edge collections, selection metadata).
- [x] Define serialization DTO boundaries for save/load JSON and explicit schema versioning.
- [x] Define mapping contract interfaces for future transformation from flow model to executable steps.
- [x] Preserve invariants similar to docking subsystem style (stable IDs, valid parent-child ownership, no orphan edges, deterministic lane order).

### Phase 2: Canvas Shell and MVVM Integration
- [x] Add a dedicated flow canvas user control in views, with supporting styles/resources.
- [x] Add corresponding canvas view model (or compose into `MainViewModel` via child VM) and command surface.
- [x] Host the canvas in the primary center pane of main workspace; retain sidebar at left.
- [x] Wire drag-drop payload acceptance for existing `UiActionDragRequest` from action browser.
- [x] Add interaction state model for hover targets, insertion previews, drag ghost metadata, and selection state.
- [x] Introduce service abstractions for hit testing, edge routing, and layout recalculation to keep code-behind minimal.

### Phase 3: Node and Container Rendering
- [x] Implement node visuals with collapse/expand affordances.
- [x] Implement container visuals for Group, Loop, and Condition with nested lane regions.
- [x] Implement container auto-resize rules:
- [x] Adding child node increases container height with spacing above/below.
- [x] Removing/moving child node shrinks container to minimum content bounds.
- [x] Child reposition updates parent bounds and ancestor bounds recursively.
- [x] Add visual states (selected, hover, drag-over valid/invalid).
- [x] Ensure layout remains responsive across min window sizes used by current main window constraints.

### Phase 4: Connections, Routing, and Insert-on-Line UX
- [x] Implement edge rendering layer that tracks node anchors and redraws on move/resize.
- [x] Establish routing strategy (orthogonal or smooth polyline) with deterministic anchor resolution.
- [x] Implement line hover detection and plus-indicator display on valid insertion targets.
- [x] On drop over edge:
- [x] Split original edge into two edges (`A -> New`, `New -> B`).
- [x] Insert node in lane order and shift downstream nodes to maintain spacing.
- [x] Recompute impacted container/root lane bounds and reroute affected edges.
- [x] Validate no dangling or duplicate edges after insertion and move operations.

### Phase 5: Editing Operations and Interaction Fidelity
- [x] Implement drag-and-drop between root lane and container lanes.
- [x] Implement multi-select rectangle and group move behavior.
- [x] Implement copy/paste of selected nodes and eligible edge subsets with new IDs.
- [x] Implement delete behavior with graph repair rules (configurable by node/container type).
- [x] Implement undo/redo command stack for drag, insert, delete, move-in/out-container, and paste operations.
- [x] Define keyboard shortcuts and command bindings for copy/paste/delete/undo/redo.

### Phase 6: Persistence and Load/Restore
- [x] Implement JSON persistence service for flow documents in app layer.
- [x] Support Save As / Open workflow and validation errors surfaced in UI logs/status.
- [x] Validate schema version on load and provide forward-compatibility guardrails.
- [x] Restore canvas viewport, collapse states, selection (optional), and deterministic lane order.

### Phase 7: Runtime Handoff Preparation
- [x] Implement mapper from UI flow document to execution-ready intermediate representation (no actual run yet).
- [x] Validate typed containers (Loop/Condition) produce well-formed intermediate graph semantics.
- [x] Add orchestrator-facing contract extension points without changing existing navigation runner behavior.
- [x] Document how final execution phase will bind Start command to the composed flow.

### Phase 8: Testing and Quality Gates
- [x] Add unit tests for graph invariants, insertion-on-line behavior, container resizing, and edge split logic.
- [x] Add view-model tests for command behavior, selection state transitions, and undo/redo stack correctness.
- [x] Add WPF integration tests for drag from action sidebar into canvas, line hover plus indicator, and nested container insertion.
- [x] Add persistence round-trip tests (save/load equivalence and schema handling).
- [x] Add performance sanity checks for medium graphs (for example 100-200 nodes) verifying interaction latency stays acceptable.

## Acceptance Criteria
1. Users can drag actions from sidebar and drop onto the canvas root lane to create nodes.
2. Users can create and interact with typed containers (Group, Loop, Condition), including nested action nodes.
3. Containers auto-resize as children are inserted/reordered/removed while preserving spacing and readability.
4. Nodes and containers are collapsible/expandable and retain state through save/load.
5. Nodes are visually connected by lines that remain attached during move/resize operations.
6. Hovering a draggable action over a valid connection line shows a plus insertion indicator.
7. Dropping on a connection line inserts the node between connected nodes and updates lane layout accordingly.
8. Multi-select, group move, copy/paste, and undo/redo work reliably for core editing actions.
9. Flow documents can be saved to and loaded from JSON with schema version validation.
10. A stable mapping contract exists from authored flow model to execution-ready representation for future orchestrator integration.
11. Automated tests cover graph logic, UI command/view-model behavior, and key interaction paths.

## Open Questions / Assumptions
### Resolved Assumptions
- Scope is editor-first (canvas authoring) and does not execute the graph yet.
- Flow model must be execution-ready for a later runtime phase.
- Implementation uses native WPF graph rendering (no external diagram library).
- Primary layout is top-to-bottom lane insertion semantics.
- Container types included now: Group, Loop, Condition.
- Mandatory editing capabilities now: undo/redo, multi-select/group move, copy/paste.
- Canvas will be hosted in the main workspace center pane.
- Persistence included now via JSON save/load.

### Remaining Open Questions (non-blocking for this plan)
- Exact visual language for node ports/anchors and edge style (orthogonal vs curved) to align with product design preferences.
- Condition container branch UX details (explicit true/false lanes vs configurable lane labels).
- Preferred storage location defaults for flow JSON files (project-relative vs user documents).
- Target interaction performance threshold definition for large graphs (formal SLA vs best-effort benchmark).

## Implementation Notes for Alignment with Existing Codebase
- Reuse existing command and MVVM patterns found in `MainViewModel` and command classes.
- Integrate with existing sidebar drag payload (`UiActionDragRequest`) rather than introducing a parallel payload shape.
- Keep new orchestration contracts additive and avoid breaking current `IAutomationOrchestrator` usage.
- Follow existing test project patterns in `tests/WpfAutomation.Core.Tests` for fast deterministic coverage and add integration-focused interaction tests where required.
