# Dock Panels Manual Verification Checklist

Use TestDockWindow from MainWindow > Dock Lab.

## Drag and Dock
- Drag a center tab to the left target and confirm it docks left.
- Drag a left tab to the right target and confirm it moves right.
- Drag a tab to the center target and confirm it becomes a center tab.
- Reorder tabs within the same tab strip and confirm order persists.
- Move a tab from one dock group to another and confirm it activates in the target group.

## Auto-Hide
- Pin a docked panel off and confirm it moves to the matching auto-hide strip.
- Hover the strip button and confirm the panel restores into the dock area.
- Re-pin the restored panel and confirm it no longer appears in the strip.

## Floating
- Float a panel and confirm a separate owned window opens.
- Drag from the floating window back into the dock host and confirm it re-docks.
- Close a floating window and confirm the panel returns to its last docked zone.

## Persistence
- Dock panels into non-default positions.
- Auto-hide at least one panel.
- Float at least one panel.
- Close and reopen the app.
- Confirm the docked, auto-hidden, and floating state is restored.
- Click Reset Layout and confirm the default centered layout returns.

## Launcher
- Click Dock Lab twice and confirm only one TestDockWindow instance exists.
- Minimize TestDockWindow, click Dock Lab again, and confirm it restores and focuses.
