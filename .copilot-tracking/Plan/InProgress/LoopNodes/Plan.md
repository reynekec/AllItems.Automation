# LoopNodes Plan

## Problem Statement
The flow designer currently supports generic container concepts (`Group`, `Loop`, `Condition`) and action-node inspectors, but it does not provide dedicated control-flow container nodes for `For`, `ForEach`, and `While`. The user needs these as container nodes with editable properties, action catalog discoverability, and runtime behavior.

This work must fit the existing architecture:
- Action catalog metadata comes from `IAutomationAction.Metadata` in `AllItems.Automation.Browser`.
- Node parameter typing/defaults are centralized in `FlowActionParameterResolver`.
- Inspector wiring is centralized in `DefaultNodeInspectorFactory` and `ActionInspectorViewModels`.
- Flow runtime readiness currently validates/maps via `FlowExecutionMapper` and does not yet run full flow graphs.

## Proposed Approach
Implement `For`, `ForEach`, and `While` as first-class control-flow container nodes, replacing the generic loop entry in UI workflows.

Key decisions (confirmed):
- Node modeling: Distinct node types in behavior terms (represented as distinct container kinds and parameter records), not just relabeled generic loop.
- Catalog location: New `Control Flow` category.
- Fields:
  - `For`: `Start`, `End`, `Step`.
  - `ForEach`: `ItemsExpression`, `ItemVariable`.
  - `While`: `ConditionExpression`, `MaxIterations`.
- Runtime scope: Include runtime loop semantics now.
- Safety defaults: Global cap `1000` iterations + per-node override.
- Expression handling: Built-in simple evaluator (literals and basic comparisons only).
- Legacy UI: Replace generic `Loop` add-command with `For`, `ForEach`, `While` entries.
- Testing depth: Unit tests only for this phase.

## Phases

### Phase 1: Extend Flow Domain Model for Control-Flow Containers
- [x] Add dedicated container kinds to `FlowContainerKind` for `For`, `ForEach`, `While`.
- [x] Add/adjust lane defaults in `FlowEditingService` for each new kind (single loop body lane where appropriate).
- [x] Define typed parameter records for loop containers in `FlowActionParameters.cs`:
  - [x] `ForContainerParameters` (`Start`, `End`, `Step`, optional `MaxIterationsOverride`).
  - [x] `ForEachContainerParameters` (`ItemsExpression`, `ItemVariable`, optional `MaxIterationsOverride`).
  - [x] `WhileContainerParameters` (`ConditionExpression`, `MaxIterations`).
- [x] Decide and codify where container parameters live in flow nodes:
  - [x] Preferred: add `ContainerParameters` to `FlowContainerNodeModel` with typed base.
  - [x] Ensure JSON snapshot mapping and persistence preserve these records.
- [x] Update flow validation rules (`FlowDocumentValidator`) for:
  - [x] Valid loop lane presence.
  - [x] Parameter shape sanity (e.g., non-zero `Step`, positive max iteration values).

### Phase 2: Add Control-Flow Entries to Action Catalog and Drop Pipeline
- [x] Introduce catalog actions for `for-loop`, `for-each-loop`, `while-loop` in `AllItems.Automation.Browser` with:
  - [x] `CategoryId = control-flow`, `CategoryName = Control Flow`.
  - [x] `IsContainer = true`.
  - [x] Stable sort orders and keywords.
- [x] Update catalog tests (`ActionCatalogBuilderTests`) to include the new category/action counts.
- [x] Update `FlowEditingService.CreateNodeFromDropRequest` mapping so drag-drop of each new action creates the right container kind and default parameters.
- [x] Remove/replace generic loop add entry in `FlowCanvasViewModel` command surface:
  - [x] Replace `AddLoopContainerCommand` usage with explicit `AddForContainerCommand`, `AddForEachContainerCommand`, `AddWhileContainerCommand`.
  - [x] Keep `Group` and `Condition` behavior unchanged.

### Phase 3: Add Container Inspector Support (UI + ViewModels)
- [x] Extend node inspector state so container nodes can resolve dedicated inspectors instead of only "container planned" placeholder.
- [x] Add container inspector view models (parallel to existing action inspector patterns) for:
  - [x] `For` container fields.
  - [x] `ForEach` container fields.
  - [x] `While` container fields.
- [x] Add corresponding XAML inspector views under `Views/NodeInspectors/Actions` (or container inspector folder if introduced).
- [x] Wire view-model-to-view mappings and factory resolution.
- [x] Ensure inspector edits persist through existing mutation and undo/redo pathways.

### Phase 4: Runtime Mapping + Execution Semantics for Loops
- [x] Extend execution mapping contract to carry container loop parameters and node subtype identity.
- [x] Implement a flow runtime executor service for mapped graphs (new service, app-layer orchestration boundary respected):
  - [x] Executes child lane nodes in sequence.
  - [x] `For`: iterate by start/end/step semantics.
  - [x] `ForEach`: iterate over parsed list values from simple evaluator.
  - [x] `While`: evaluate condition expression each pass.
- [x] Enforce safeguards:
  - [x] Global max iterations (`1000`).
  - [x] Per-node cap override/limit.
  - [x] Cancellation token checks every iteration and before child execution.
- [x] Emit diagnostics at loop start/end/iteration/cancel/failure in existing logging style.
- [x] Fail with contextual exceptions when evaluation/parsing/limit checks fail.

### Phase 5: Unit Tests and Regression Coverage
- [x] Add unit tests for model + persistence:
  - [x] Round-trip of new container kinds and parameter records.
  - [x] Validation failures for invalid loop configs.
- [x] Add unit tests for catalog behavior:
  - [x] `Control Flow` category appears with three actions.
  - [x] `IsContainer` flags and ordering are correct.
- [x] Add unit tests for inspector behavior:
  - [x] Selecting each loop container resolves the correct inspector.
  - [x] Editing inspector fields mutates document and supports undo/redo.
- [x] Add unit tests for runtime semantics:
  - [x] `For` iteration count and boundaries.
  - [x] `ForEach` item parsing and item-variable binding behavior.
  - [x] `While` condition re-evaluation and max-iteration stopping.
  - [x] Cancellation and global cap enforcement.

## Acceptance Criteria
1. Users can add `For`, `ForEach`, and `While` as container nodes from a new `Control Flow` action category.
2. The generic loop UI entry is replaced by explicit `For`, `ForEach`, and `While` entries.
3. Each new container has a dedicated inspector with the agreed fields and persists edits through save/load and undo/redo.
4. Flow validation catches invalid loop configurations (e.g., zero step, invalid max iterations).
5. Runtime mapping includes enough metadata/parameters to execute loop semantics.
6. Loop runtime execution supports `For`, `ForEach`, and `While` with cancellation checks and iteration safety caps.
7. New unit tests pass for catalog, model/persistence, inspector, and runtime semantics.

## Open Questions / Assumptions
- Assumption: No existing production documents rely on legacy generic `Loop` nodes, so migration is not required for this phase.
- Assumption: Simple expression evaluator scope is intentionally limited (literals/basic comparisons only) and can be expanded later.
- Assumption: Unit-test-only scope is acceptable for this delivery; integration/UI automation tests can be a follow-up plan.
