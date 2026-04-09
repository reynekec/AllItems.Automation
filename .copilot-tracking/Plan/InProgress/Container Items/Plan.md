# Container Items Plan

## Problem Statement
Container node visuals currently show lane summaries, but drag-drop behavior still treats new action drops as root-canvas insertions. Users need container-aware drop behavior so dropping into a container actually inserts the node into that container's lane and links child nodes with the same sequential connector behavior used on the main canvas.

Requested outcomes:
- Explicit container node types can receive dropped nodes.
- Dropping onto blank lane space appends to the end of that lane sequence.
- Dropping between two existing lane nodes inserts between them.
- Adding a second child node in a container shows a connector line between siblings, consistent with normal canvas behavior.
- Connector linking remains container-local (no cross-container auto-links).
- Nested containers are supported recursively.
- Execution order follows each container lane's local sequence.

## Proposed Approach
Reuse existing flow-lane architecture instead of introducing new abstractions:
- Detect container/lane drop targets in the view layer (`FlowCanvasView`) during drag-over and drop.
- Extend `FlowCanvasViewModel.HandleDrop` to accept resolved lane context (target lane + optional target edge) in addition to point-based root insertion.
- Extend `FlowEditingService.AddActionNode` to support lane-aware insertion:
  - Root lane keeps current behavior.
  - Container lane insertion reuses current edge split behavior when dropping on an edge.
  - Blank area insertion appends in lane order and creates predecessor->new edge.
- Preserve `FlowEdgeLaneMetadataModel` so edge routing/rendering can keep lines scoped to owning container lanes.
- Continue using `MoveNodesToLane` for moving existing selected nodes into target lanes (canvas -> container), and ensure edge/link repair is lane-aware.
- Validate through focused unit tests in `CanvasFlowTests` and related flow validator/runtime tests.

Key decisions (confirmed):
- Scope: only explicit container node types accept child nodes.
- Insertion behavior: same as root canvas (blank area => append; between nodes => insert).
- Connector scope: sibling links only within the same container lane.
- Drop sources: support both new palette/toolbox drops and moving existing canvas nodes into a container.
- Nesting: allow nested containers recursively.
- Runtime: container child ordering affects execution the same way root ordering does.

## Phases

### Phase 1: Target Resolution for Container Drops
- [x] Add lane hit-testing support in `FlowCanvasView` to resolve whether pointer is over:
  - [x] Root canvas.
  - [x] A container lane body.
  - [x] A specific lane edge insertion point.
- [x] Update drag-over behavior to compute lane-local insertion preview using existing hover indicator concepts.
- [x] Ensure drop effects (`Copy`/`Move`) reflect valid target acceptance:
  - [x] Accept drops over explicit container lanes.
  - [x] Reject non-container internals where child insertion is invalid.
- [x] Keep root-canvas drop behavior unchanged when no lane target is resolved.

### Phase 2: ViewModel Drop Contract Extension
- [x] Introduce a drop context model passed from view to `FlowCanvasViewModel.HandleDrop` containing:
  - [x] Drop point.
  - [x] Target lane id (optional; root when null or explicit root id).
  - [x] Target edge id for insert-between behavior (optional).
  - [x] Optional target container id for diagnostics.
- [x] Preserve duplicate-drop suppression by including lane/context identity in snapshot comparison.
- [x] Update hover/preview state management so lane edge previews clear correctly on leave and on completed drop.
- [x] Add diagnostics entries for container-targeted drops (target container/lane/edge).

### Phase 3: Lane-Aware Node Insertion in Editing Service
- [x] Extend `IFlowEditingService.AddActionNode` and implementation to accept lane-aware insertion context.
- [x] Implement lane append behavior:
  - [x] Insert node id into target lane `NodeIds` at end.
  - [x] Create edge from prior sibling to new node when prior exists.
  - [x] Attach `LaneMetadata` (`SourceLaneId`, `TargetLaneId`, `OwningContainerNodeId`).
- [x] Implement lane edge-split behavior:
  - [x] Split target edge into predecessor->new and new->successor.
  - [x] Preserve lane metadata and owning container metadata.
  - [x] Insert node id at correct index in lane order.
- [x] Ensure inserted node gets `ParentContainerNodeId` matching lane owner.
- [x] Keep root-lane insertion logic unchanged and backward compatible.
- [x] Re-run `LayoutContainers` / recalc so container bounds grow/shrink consistently after child changes.

### Phase 4: Move Existing Nodes Into Containers
- [x] Wire drag/move workflow to call `MoveNodesToLane` with container lane target for selected node moves.
- [x] Ensure root and source lane membership are updated atomically when moving into container lanes.
- [x] Add/adjust edge repair behavior for move operations:
  - [x] Remove invalid old lane links.
  - [x] Build valid sequential links in target lane based on insert location.
  - [x] Keep edge metadata lane-scoped.
- [x] Confirm move semantics for nested container targets (container-in-container) are supported.

### Phase 5: Connector Rendering and Routing Consistency
- [x] Verify `RefreshEdgeVisuals` and routing include lane metadata edges exactly once.
- [x] Ensure container sibling connectors render with same polyline style as root connectors.
- [x] Prevent accidental cross-container line rendering by validating metadata ownership during edge creation.
- [x] Confirm hit testing for insert preview works on container-local connector segments.

### Phase 6: Validation, Persistence, and Runtime Semantics
- [x] Update `FlowDocumentValidator` rules for lane-local sequencing:
  - [x] Child node `ParentContainerNodeId` must match owning lane container.
  - [x] Lane metadata container ownership must reference valid container node.
- [x] Verify `FlowPersistenceService` round-trips lane metadata and parent references unchanged.
- [x] Validate runtime mapping/execution honors container lane order identically to root order.
- [x] Add guard checks for nested container execution ordering and lane integrity.

### Phase 7: Test Coverage
- [x] Add/extend unit tests in `CanvasFlowTests` for:
  - [x] Drop into empty container lane appends as first child.
  - [x] Drop second node into same lane creates sibling connector edge.
  - [x] Drop on lane edge inserts between siblings and preserves order.
  - [x] Drop outside lane still uses root behavior.
  - [x] Nested container drop works and links remain local.
- [x] Add tests for moving existing node from root into container lane.
- [x] Add validator tests for incorrect parent/lane metadata combinations.
- [x] Add persistence round-trip checks for container-child lane metadata edges.
- [x] Add execution mapper/runtime tests for container-local sequence order.

## Acceptance Criteria
1. Dropping a new node over a valid container lane places the node in that lane (not in root lane).
2. Dropping into blank space within a lane appends the node to lane end and updates lane order.
3. Dropping on a connector between two lane siblings inserts the node between them.
4. After adding at least two children in a lane, a visible connector line is rendered between them using existing canvas connector style.
5. Connector edges created for container children carry lane metadata with correct owning container and do not auto-link across container boundaries.
6. Moving an existing root node into a container lane updates parent ownership, lane membership, and valid lane-local connectors.
7. Nested container drops are supported, persisted, and validated.
8. Runtime ordering for container children follows lane sequence, and existing root behavior remains unchanged.
9. New and updated unit tests for drop, move, metadata, persistence, and sequencing pass.

## Open Questions / Assumptions
- Assumption: Container lanes shown in `FlowContainerNodeModel.ChildLanes` are the only valid child insertion targets.
- Assumption: Condition containers continue using separate true/false lanes; this plan applies same insertion logic per targeted lane without auto-branch inference.
- Assumption: Existing edge styling in `FlowCanvasView.xaml` remains unchanged; only data/routing behavior is updated.
- Assumption: UI affordance changes are limited to current hover/insert indicators, without adding new custom adorner layers.
- Assumption: Existing drag selection/move gesture model is retained; only target resolution and lane-aware mutations change.
