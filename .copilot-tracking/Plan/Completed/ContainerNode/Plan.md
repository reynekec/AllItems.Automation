# Container-Aware Canvas Node Drop Plan

## Problem Statement
The Action Browser can drag actions onto the canvas, but the drop payload and flow creation path currently treat every dropped item as a basic action node. The canvas needs to know, at drop time, whether an item is a container so it can create a container-capable node immediately (with expand/collapse behavior), instead of requiring a later conversion or manual setup.

## Proposed Approach
Propagate a single source-of-truth container flag from action metadata through the full UI flow:
- Add container capability metadata to action definitions.
- Surface it in Action Browser item models.
- Include it in drag payload (`UiActionDragRequest`).
- Branch drop handling in flow editing so container items create `FlowContainerNodeModel` with `IsExpanded = true` and `IsCollapsed = false`.
- Keep non-container items as `FlowActionNodeModel` with no expand/collapse affordance.
- Persist and restore container capability and expanded/collapsed state through existing flow snapshot mapping.

Key decisions confirmed:
- Source of truth: Action Browser item metadata.
- Container default drop state: Expanded.
- Non-container UX: No expand/collapse affordance.
- Persistence: Save both container capability and expanded/collapsed state.
- Backward compatibility: Missing metadata defaults to non-container.
- Test scope: Unit + integration tests.

## Phases

### Phase 1: Extend Metadata Contracts End-to-End
- [x] Add `IsContainer` (or equivalent container capability flag) to core action metadata contract in `WpfAutomation.Core` (`ActionMetadata`).
- [x] Update concrete action definitions in `AllItems.Automation.Browser` to set container capability explicitly (default false unless intentionally container-capable).
- [x] Add corresponding property to `UiActionItem` and map it in `ActionCatalogBuilder`.
- [x] Add corresponding property to `UiActionDragRequest` and populate it in `ActionBrowser` drag creation.
- [x] Keep backward compatibility by treating missing/null container capability as non-container.

### Phase 2: Canvas Drop and Node Creation Behavior
- [x] Update `FlowCanvasViewModel.HandleDrop` and/or `IFlowEditingService.AddActionNode` flow so drop behavior branches by `request.IsContainer`.
- [x] For non-container drops: preserve existing action-node path.
- [x] For container drops: create a `FlowContainerNodeModel` at drop position with container defaults:
- [x] `IsExpanded = true`
- [x] `IsCollapsed = false`
- [x] Ensure dropped container nodes participate in existing lane/root ordering and edge insertion behavior.
- [x] Ensure duplicate-drop suppression logic still works with container payloads.

### Phase 3: UI Rendering and Interaction Consistency
- [x] Verify dropped container nodes render via existing `FlowContainerNodeModel` template in `FlowCanvasView.xaml`.
- [x] Verify expand/collapse command wiring (`ToggleCollapseCommand`) works immediately for newly dropped containers.
- [x] Verify non-container nodes continue rendering as `FlowActionNodeModel` with no collapse button.
- [x] Confirm no regressions to manual toolbar-created containers (`Group`, `Loop`, `Condition`).

### Phase 4: Persistence and Mapping Validation
- [x] Confirm `FlowSnapshotMapper.ToSnapshot` and `FromSnapshot` preserve container/node type and expanded/collapsed state for dropped container-origin nodes.
- [x] If needed, evolve flow schema/versioning and ensure forward/backward guards remain valid.
- [x] Add/adjust persistence tests to verify round-trip fidelity for container-created-from-drag nodes.

### Phase 5: Test Coverage
- [x] Update `ActionCatalogBuilderTests` to validate container metadata propagation into `UiActionItem`.
- [x] Update/create tests in `CanvasFlowTests` for:
- [x] Drop non-container => creates `FlowActionNodeModel`.
- [x] Drop container => creates `FlowContainerNodeModel`.
- [x] Container drop initial state => expanded/collapsed defaults are correct.
- [x] Expand/collapse works immediately after drop.
- [x] Add/extend integration-level coverage (existing Phase/interaction suites) to validate Action Browser drag payload includes container capability and canvas behavior matches.

### Phase 6: Hardening and Rollout
- [x] Run targeted test suites first (`CanvasFlowTests`, `ActionCatalogBuilderTests`, relevant integration tests).
- [x] Run full core/app test pass for regression safety.
- [x] Validate common user flow manually: drag known container action from Action Browser and confirm immediate expand/collapse affordance.
- [x] Document new metadata requirement for action authors (container-capable actions must set metadata flag).

## Acceptance Criteria
1. Dragging a non-container action to canvas creates an action node and does not show expand/collapse affordance.
2. Dragging a container action to canvas creates a container node on drop with expand/collapse immediately available.
3. New container node starts expanded by default.
4. Toggling collapse/expand works on first drop without additional edits.
5. Saved flow documents preserve container node type and expanded/collapsed state; reopening restores the same behavior.
6. Existing actions with no explicit container metadata are treated as non-container and continue to work.
7. Unit and integration tests covering metadata propagation + drop behavior pass.

## Open Questions / Assumptions
- Assumption: Existing `FlowContainerNodeModel` visual/command behavior is sufficient for newly dropped container-origin nodes; no new container subtype UI is required.
- Assumption: Initial rollout can set all current actions to non-container until specific container-capable actions are identified.
- Assumption: Container capability is boolean for now (no separate container-kind selection from Action Browser in this phase).
