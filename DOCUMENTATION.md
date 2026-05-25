# Technical Documentation

This document covers the internal architecture and usage guidelines for the Avalonia Docking Manager.

## 1. Architecture Overview

The system strictly adheres to the **Model-View-ViewModel (MVVM)** pattern. 
Instead of manipulating Avalonia UI controls directly (which often causes "Visual already has a logical parent" exceptions), the library manipulates pure C# data models. Avalonia's data binding engine then reacts to these models and dynamically updates the visual tree via `DataTemplates`.

### Node Hierarchy (`Models/DockModels.cs`)

The entire layout is a tree where nodes inherit from the base `DockNode` class.

*   `WorkspaceNode`: Represents a global "Major Window" (e.g., Level Editor, Material Editor). Contains a `Title` and a unique `LayoutRoot` (a `DockNode` tree).
*   `DockNode` (Abstract)
    *   **`DockPanelNode`**: The actual leaf node. Represents a single "Window" or "View". It holds a `Title` (string) and `Content` (object).
    *   **`DockTabGroupNode`**: Represents a visual tab control. It contains a collection of `DockPanelNode`s (the tabs) and tracks the `ActivePanel`.
    *   **`DockGroupNode`**: Represents a layout split (either Horizontal or Vertical). It contains a collection of child `DockNode`s (which can be TabGroups, or other nested DockGroups).

## 2. Dynamic Rendering

The `DockManager` control takes a `LayoutRoot` (which is a `DockNode`).

*   If the node is a `DockTabGroupNode`, it renders a `DockTabGroup` UserControl.
*   If the node is a `DockGroupNode`, it dynamically generates a `Grid` with row/column definitions corresponding to the number of children. It also automatically interleaves `GridSplitter` controls between the child nodes to allow resizing.

## 3. Drag and Drop Implementation

Native OS drag-and-drop (`DragDrop.DoDragDrop`) is highly dependent on the host operating system and can be difficult to style consistently (e.g., drawing a custom transparent overlay window across multiple monitors).

### Custom Pointer Capture & Bounding-Box Caching
To solve this, this library implements a **custom drag-and-drop system** using pointer capture, heavily optimized to maintain 1000+ FPS:
1.  **Start (`BeginDrag`)**: When a user presses the mouse on a tab header, `e.Pointer.Capture(control)` is called. At this exact moment, the `DockManager` traverses the visual tree **once** to find all valid drop targets (`DockTabGroup`s) and caches their absolute coordinates (Bounding Boxes) into a flat list.
2.  **Move (`UpdateDrag`)**: During `PointerMoved`, instead of traversing the UI tree again, the system performs a simple mathematical check (`Rect.Contains(mousePos)`) against the cached bounding boxes. This guarantees zero memory allocations and no UI lag.
3.  **Calculate Zone**: It calculates a `DockZone` (Top, Right, Bottom, Left, Center) based on the relative position of the mouse inside the target's bounding box.
4.  **Finish (`EndDrag`)**: On `PointerReleased`, it mathematically updates the MVVM node tree (e.g., removing the panel from the source array, creating a new `DockGroupNode` if a split is required, and adding the panel to the new target).

## 4. Tear-off (Floating Windows)

If a user drags a tab and releases it outside of any valid docking zone (or outside the main window), the system triggers a "Tear-off" event.

1.  The `DockPanelNode` is removed from its original parent tree.
2.  A new Avalonia `Window` is instantiated.
3.  A new instance of `DockManager` is placed inside that window.
4.  The detached `DockPanelNode` becomes the `LayoutRoot` of the new window.

## 5. Theming & Styling

Because the views are completely separated from the state, you can completely overhaul the design.

### Modifying the Tab Design
Open `Controls/DockTabGroup.axaml`. The header design is defined inside the `<TabControl.ItemTemplate>`. 
You can replace the `<Border>` and `<TextBlock>` with your own custom designs, SVGs, or buttons (e.g., adding a "Close [x]" button).

### Modifying the Window / Manager Background
The default background color for the manager and the floating tear-off windows is currently hardcoded to `#1E1E1E` (Dark Mode). To support Dynamic Themes (Light/Dark mode switching), you should replace these hex values with Avalonia DynamicResources:

```xml
<!-- Before -->
<Border Background="#1E1E1E" />

<!-- After -->
<Border Background="{DynamicResource SystemRegionBrush}" />
```

## 6. Performance Optimization

When panels contain heavy controls (like 3D viewports or complex node graphs), resizing the windows via splitters in real-time can cause severe FPS drops.

To guarantee maximum performance, the library supports a "Preview" resizing mode where a lightweight guide line is drawn during drag, and the heavy layout is recalculated only when the user releases the mouse.

This is enabled by default. You can toggle it programmatically:

```csharp
// Maximize FPS: show a grey preview line during resize (default)
Avalonia.DockingManager.Controls.DockManager.ShowSplitterPreview = true;

// Maximize Aesthetics: resize windows in real-time (can lag with heavy content)
Avalonia.DockingManager.Controls.DockManager.ShowSplitterPreview = false;
```
