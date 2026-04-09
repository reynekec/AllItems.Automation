# Flow Execution Binding Notes

Current behavior is editor-first.

- `FlowCanvasViewModel.ValidateForRunCommand` maps the current `FlowDocumentModel` to `ExecutionFlowGraph` using `FlowExecutionMapper`.
- Runtime mapping validation enforces container semantics for `Loop` and `Condition` lanes before any execution handoff.
- `IFlowExecutionBridge` is the orchestrator-facing extension point for a future phase where `MainViewModel.StartCommand` will execute a composed flow graph.

Planned final binding:

1. `StartCommand` checks whether the canvas contains at least one node.
2. The canvas flow is mapped with `IFlowDocumentMapper<ExecutionFlowGraph>`.
3. The mapped graph is passed to `IFlowExecutionBridge.PrepareRunAsync`.
4. Existing navigation-only behavior remains the fallback path when no flow graph is authored.
