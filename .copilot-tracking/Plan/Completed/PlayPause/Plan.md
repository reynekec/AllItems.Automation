# PlayPause - Medium Complexity Plan

## Problem Statement

The current runner UX supports Start, Debug, and Stop (cancel), but does not support true pause/resume semantics.

Desired behavior for v1:
- The main Run control behaves as a 3-state toggle:
  - Idle: Run
  - Running: Pause
  - Paused: Continue playing
- Pause should be cooperative and checkpoint-based (between safe execution boundaries), not a forced interruption of in-flight browser calls.
- Resume should continue from the current flow position without restarting the run.
- Stop remains a separate hard cancel operation.
- Debug runs follow the same pause/resume behavior as normal runs.
- Scope is in-memory only for v1 (no persisted resume checkpoints across app restart).

Why this matters:
- Improves control and trust during long automation flows.
- Reduces accidental full cancellations when users only need temporary interruption.
- Keeps complexity medium by using cooperative runtime gates.

## Proposed Approach

Implement a cooperative execution controller that can gate progression at runtime checkpoints and resume without rebuilding execution state.

Key decisions:
- Use cooperative pause checkpoints only at safe boundaries:
  - Before each executable flow action
  - At loop iteration boundaries
  - At runtime polling loops where cancellation checks already exist
- Do not attempt hard preemptive pause inside an in-flight Playwright API call.
- Keep Stop semantics unchanged: cancellation + active session close.
- Introduce explicit paused run state in UI state model.
- Make main toolbar Run button dynamic for icon, tooltip, and action behavior.
- Keep implementation aligned with existing MVVM command and diagnostics patterns.

Design outline:
- Add a run control primitive (pause gate) with methods to request pause, resume, and wait when paused.
- Integrate gate checks into flow runtime and execution bridge checkpoints.
- Extend MainViewModel command logic from start-only to a run/pause/resume state machine.
- Bind toolbar icon/tooltip text to state-driven properties.
- Add diagnostics events for pause requested, paused, resumed, and continue playing.

## Phases

### Phase 1 - State Model and Command Surface
- [x] Add a Paused value to UiRunState.
- [x] Add MainViewModel state flags and computed properties for:
  - IsPaused
  - CanPause
  - CanResume
  - Run button icon glyph
  - Run button tooltip text (Continue playing in paused state)
- [x] Refactor StartCommand behavior into state-aware run/pause/resume action handling.
- [x] Keep StopCommand independent and available for hard cancel while running or paused.
- [x] Ensure command CanExecute transitions update correctly on every state transition.

### Phase 2 - Cooperative Pause Controller
- [x] Introduce a shared execution control object for active run lifecycle (in-memory only).
- [x] Define contract for pause/resume gates, e.g.:
  - RequestPause
  - Resume
  - WaitIfPausedAsync(cancellationToken)
- [x] Ensure pause wait is cancellation-aware so Stop unblocks paused runs immediately.
- [x] Ensure controller is reset/disposed correctly at run completion/failure/cancel.

### Phase 3 - Runtime Integration Checkpoints
- [x] Integrate pause gate checkpoints in flow runtime and execution bridge:
  - Before each action execution
  - At each loop iteration boundary
  - In polling loops that already check cancellation
- [x] Preserve current cancellation behavior and exception flow.
- [x] Ensure resume continues from current index/iteration without rerunning prior completed steps.
- [x] Ensure no session teardown occurs on pause.

### Phase 4 - UI Binding and Runner UX
- [x] Update FlowCanvas toolbar binding so middle button dynamically shows:
  - Play icon and Run tooltip in idle/completed states
  - Pause icon and Pause tooltip when running
  - Play icon and Continue playing tooltip when paused
- [x] Keep debug button behavior aligned with the same runtime control model.
- [x] Update status text and status bar badge for Paused state.
- [x] Add diagnostics log entries for pause lifecycle transitions.

### Phase 5 - Test Coverage and Regression Safety
- [x] Update/extend MainViewModel tests for:
  - Run -> Pause transition
  - Pause -> Continue playing transition
  - Stop while paused
  - Correct button/state/status transitions
- [x] Add runtime tests for checkpoint pause behavior:
  - Pauses at action boundary
  - Resumes from next boundary
  - Stop cancels while waiting in paused gate
- [x] Add bridge-level tests validating no browser session close on pause and normal close on stop.
- [x] Verify existing run, debug, and cancel tests continue to pass.

### Phase 6 - Hardening and Rollout
- [x] Validate diagnostics consistency and user-facing status messaging.
- [x] Validate behavior under long-running loops and polling waits.
- [x] Confirm no deadlocks or stuck states in pause/resume transitions.
- [x] Document operational limitations:
  - pause is cooperative checkpoint-based
  - in-flight Playwright calls complete before pause takes effect

## Acceptance Criteria

1. Main Run control supports 3-state behavior:
- Idle state click starts execution.
- Running state click requests pause.
- Paused state click resumes and displays Continue playing semantics.

2. Pause semantics:
- Execution pauses at the next safe checkpoint and does not advance further until resumed.
- Browser session remains active while paused.

3. Resume semantics:
- Resume continues from current flow position, not from the beginning.
- Status and diagnostics reflect resumed execution.

4. Stop semantics:
- Stop cancels execution immediately (including when paused) and closes active session(s).
- Final run state is Cancelled unless a failure occurred first.

5. Debug parity:
- Debug runs support the same pause/resume behavior and UI state transitions as normal runs.

6. UX details:
- Running state uses pause icon.
- Paused state uses play icon and Continue playing tooltip/text.

7. Non-functional:
- No deadlocks under repeated pause/resume cycles.
- Existing run/cancel behaviors remain stable.
- All relevant tests pass.

## Open Questions / Assumptions

Resolved assumptions for this plan:
- Pause is checkpoint-based (not hard preemptive interruption).
- Main button is a 3-state Run/Pause/Continue playing control.
- Stop remains separate hard cancel.
- Debug follows same pause/resume behavior.
- v1 is in-memory only (no persisted resume checkpoints).

No unresolved open questions remain for v1 scope.
