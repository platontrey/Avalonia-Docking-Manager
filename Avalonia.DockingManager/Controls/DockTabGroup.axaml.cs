using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.DockingManager.Models;
using Avalonia.DockingManager.Services;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Avalonia.DockingManager.Controls;

public enum DockZone
{
    None,
    Center,
    Top,
    Bottom,
    Left,
    Right,
    RootTop,
    RootBottom,
    RootLeft,
    RootRight,
    TabReorder
}

public partial class DockTabGroup : UserControl
{
    public DockTabGroup()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private Point _dragStartPoint;
    private bool _isDragging;
    private bool _isPressed;
    private bool _isInStripReorder;
    private DockPanelNode? _pressedPanel;

    public void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var panel = (sender as Control)?.DataContext as DockPanelNode;

        // 1. Middle mouse button: quick close tab if permitted
        if (point.Properties.IsMiddleButtonPressed && panel != null)
        {
            if (panel.CanClose && DataContext is DockTabGroupNode tabGroup)
            {
                CloseTab(panel, tabGroup);
                e.Handled = true;
                return;
            }
        }

        // 2. Left mouse button: prepare drag
        if (point.Properties.IsLeftButtonPressed && panel != null)
        {
            _dragStartPoint = e.GetPosition(null);
            _isPressed = true;
            _isInStripReorder = false;
            _pressedPanel = panel;
            e.Pointer.Capture(sender as Control);
        }
    }

    public void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPressed = false;
        e.Pointer.Capture(null);

        if (_isDragging)
        {
            _isDragging = false;

            if (_isInStripReorder)
            {
                // Tab reorder completed cleanly in-place
                DockDragCoordinator.CancelDrag();
            }
            else
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var windowPoint = e.GetPosition(topLevel);
                    var screenPoint = topLevel.PointToScreen(windowPoint);
                    DockDragCoordinator.EndDrag(screenPoint, topLevel, windowPoint);
                }
                else
                {
                    DockDragCoordinator.CancelDrag();
                }
            }
        }

        _isInStripReorder = false;
        _pressedPanel = null;
    }

    public void OnTabPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPressed || _pressedPanel == null) return;

        var manager = this.GetVisualAncestors().OfType<DockManager>().FirstOrDefault();
        if (manager == null) return;

        var currentPoint = e.GetPosition(null);
        var diff = currentPoint - _dragStartPoint;

        if (!_isDragging)
        {
            // Require 10px movement so ordinary clicks and tab selections don't trigger drag
            if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10)
            {
                _isDragging = true;
                if (DataContext is DockTabGroupNode sourceGroup)
                {
                    DockDragCoordinator.StartDrag(_pressedPanel, sourceGroup, manager);
                }
            }
        }

        if (_isDragging)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var windowPoint = e.GetPosition(topLevel);
                var screenPoint = topLevel.PointToScreen(windowPoint);

                var posInGroup = e.GetPosition(this);
                // Tab strip is roughly the top 36px of the group.
                // If cursor is inside tab strip: horizontal tab reorder mode!
                if (posInGroup.Y >= 0 && posInGroup.Y < 36 && DataContext is DockTabGroupNode tabGroup)
                {
                    _isInStripReorder = true;
                    if (tabGroup.Panels.Count > 1)
                    {
                        TryReorderTabs(posInGroup.X, tabGroup, _pressedPanel);
                    }
                    // Hide any active dock overlay while reordering inside the strip
                    manager.HideOverlay();
                }
                else
                {
                    // Pointer left the tab strip: full docking mode!
                    _isInStripReorder = false;
                    DockDragCoordinator.UpdateDrag(screenPoint, topLevel, windowPoint);
                }
            }
        }
    }

    private void TryReorderTabs(double mouseX, DockTabGroupNode tabGroup, DockPanelNode draggingPanel)
    {
        int currentIndex = tabGroup.Panels.IndexOf(draggingPanel);
        if (currentIndex < 0) return;

        // Estimate average tab width (min 80, based on panel strip width)
        double estimatedTabWidth = Math.Max(80.0, Bounds.Width / Math.Max(1, tabGroup.Panels.Count));
        int targetIndex = Math.Clamp((int)(mouseX / estimatedTabWidth), 0, tabGroup.Panels.Count - 1);

        if (targetIndex != currentIndex)
        {
            tabGroup.Panels.Move(currentIndex, targetIndex);
            tabGroup.ActivePanel = draggingPanel;
        }
    }

    public void OnCloseTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is DockPanelNode panel && DataContext is DockTabGroupNode tabGroup)
        {
            CloseTab(panel, tabGroup);
        }
    }

    private void CloseTab(DockPanelNode panel, DockTabGroupNode tabGroup)
    {
        if (!panel.CanClose) return;

        int oldIndex = tabGroup.Panels.IndexOf(panel);
        tabGroup.Panels.Remove(panel);
        if (tabGroup.ActivePanel == panel || tabGroup.ActivePanel == null || !tabGroup.Panels.Contains(tabGroup.ActivePanel))
        {
            if (tabGroup.Panels.Count > 0)
            {
                int nextIdx = Math.Clamp(oldIndex, 0, tabGroup.Panels.Count - 1);
                tabGroup.ActivePanel = tabGroup.Panels[nextIdx];
            }
            else
            {
                tabGroup.ActivePanel = null;
            }
        }

        var manager = this.GetVisualAncestors().OfType<DockManager>().FirstOrDefault();
        manager?.CleanupEmptyGroup(tabGroup);
    }

    // ── Context Menu Actions ────────────────────────────────

    private void OnContextMenuFloat(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.CommandParameter is DockPanelNode panel && DataContext is DockTabGroupNode tabGroup)
        {
            if (!panel.CanFloat) return;

            var manager = this.GetVisualAncestors().OfType<DockManager>().FirstOrDefault();
            var topLevel = TopLevel.GetTopLevel(this);
            var screenPos = topLevel != null ? topLevel.PointToScreen(new Point(100, 100)) : new PixelPoint(200, 200);

            manager?.TearOff(panel, tabGroup, screenPos);
        }
    }

    private void OnContextMenuClose(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.CommandParameter is DockPanelNode panel && DataContext is DockTabGroupNode tabGroup)
        {
            CloseTab(panel, tabGroup);
        }
    }

    private void OnContextMenuCloseOthers(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.CommandParameter is DockPanelNode activePanel && DataContext is DockTabGroupNode tabGroup)
        {
            var toRemove = tabGroup.Panels.Where(p => p != activePanel && p.CanClose).ToList();
            foreach (var p in toRemove)
            {
                tabGroup.Panels.Remove(p);
            }
            tabGroup.ActivePanel = activePanel;

            var manager = this.GetVisualAncestors().OfType<DockManager>().FirstOrDefault();
            manager?.CleanupEmptyGroup(tabGroup);
        }
    }

    private void OnContextMenuCloseRight(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.CommandParameter is DockPanelNode activePanel && DataContext is DockTabGroupNode tabGroup)
        {
            int idx = tabGroup.Panels.IndexOf(activePanel);
            if (idx >= 0 && idx < tabGroup.Panels.Count - 1)
            {
                var toRemove = tabGroup.Panels.Skip(idx + 1).Where(p => p.CanClose).ToList();
                foreach (var p in toRemove)
                {
                    tabGroup.Panels.Remove(p);
                }
                tabGroup.ActivePanel = activePanel;

                var manager = this.GetVisualAncestors().OfType<DockManager>().FirstOrDefault();
                manager?.CleanupEmptyGroup(tabGroup);
            }
        }
    }
}