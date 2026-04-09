# Docking Invariants

Phase 1 invariants for the docking subsystem:

- Every panel has a globally unique, stable `PanelId`.
- A panel can belong to at most one tab group at a time.
- Each tab group has exactly one active panel when it contains one or more panels.
- Split node `Ratio` is normalized to the closed interval [0.1, 0.9].
- A split node is either a leaf (`GroupId` set) or a branch (both child IDs set), never both.
- Floating host IDs are unique and each floating host maps to exactly one tab group.
- Auto-hide strip order values are unique within a strip placement.
- Layout snapshots are immutable and versioned through `SchemaVersion`.
