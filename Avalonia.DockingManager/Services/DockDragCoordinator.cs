using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.DockingManager.Controls;
using Avalonia.DockingManager.Models;

namespace Avalonia.DockingManager.Services;

public static class DockDragCoordinator
{
    private static readonly List<DockManager> s_Managers = new();

    public static IReadOnlyList<DockManager> ActiveManagers => s_Managers;

    public static bool IsDragging { get; private set; }
    public static DockPanelNode? DraggedPanel { get; private set; }
    public static DockTabGroupNode? SourceGroup { get; private set; }
    public static DockManager? SourceManager { get; private set; }

    private static DockManager? s_CurrentTargetManager;

    public static void Register(DockManager manager)
    {
        if (!s_Managers.Contains(manager))
            s_Managers.Add(manager);
    }

    public static void Unregister(DockManager manager)
    {
        s_Managers.Remove(manager);
        if (s_CurrentTargetManager == manager)
            s_CurrentTargetManager = null;
    }

    public static void StartDrag(DockPanelNode panel, DockTabGroupNode sourceGroup, DockManager sourceManager)
    {
        if (!panel.CanDrag) return;

        IsDragging = true;
        DraggedPanel = panel;
        SourceGroup = sourceGroup;
        SourceManager = sourceManager;
        s_CurrentTargetManager = null;

        // Cache drag target bounds on all registered managers
        foreach (var dm in s_Managers)
        {
            dm.PrepareDragTargets();
        }
    }

    public static bool TryGetLocalPoint(DockManager dm, PixelPoint screenPos, TopLevel? originTopLevel, Point? originWindowPos, out Point localPoint)
    {
        localPoint = default;
        var topLevel = TopLevel.GetTopLevel(dm);
        if (topLevel == null) return false;

        try
        {
            // If the manager resides in the same window where the drag started,
            // avoid X11 screen point roundtrip to prevent window border offset artifacts.
            if (originTopLevel != null && originWindowPos.HasValue && topLevel == originTopLevel)
            {
                var directTransform = topLevel.TransformToVisual(dm);
                if (!directTransform.HasValue) return false;

                localPoint = originWindowPos.Value.Transform(directTransform.Value);
                return true;
            }

            // Cross-window drag: convert screen point to target window client coordinates
            var windowPoint = topLevel.PointToClient(screenPos);
            var transform = topLevel.TransformToVisual(dm);
            if (!transform.HasValue) return false;

            localPoint = windowPoint.Transform(transform.Value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void UpdateDrag(PixelPoint screenPos, TopLevel? originTopLevel = null, Point? originWindowPos = null)
    {
        if (!IsDragging) return;

        DockManager? hitManager = null;
        Point hitLocalPoint = default;

        // Find which manager the pointer is currently over
        foreach (var dm in s_Managers)
        {
            if (!dm.IsEffectivelyVisible) continue;

            if (TryGetLocalPoint(dm, screenPos, originTopLevel, originWindowPos, out var localPos))
            {
                if (new Rect(dm.Bounds.Size).Contains(localPos))
                {
                    hitManager = dm;
                    hitLocalPoint = localPos;
                    break;
                }
            }
        }

        if (s_CurrentTargetManager != hitManager)
        {
            s_CurrentTargetManager?.HideOverlay();
            s_CurrentTargetManager = hitManager;
        }

        if (hitManager != null)
        {
            hitManager.UpdateDrag(hitLocalPoint);
        }
    }

    public static void EndDrag(PixelPoint screenPos, TopLevel? originTopLevel = null, Point? originWindowPos = null)
    {
        if (!IsDragging || DraggedPanel == null || SourceGroup == null || SourceManager == null)
        {
            CancelDrag();
            return;
        }

        var panel = DraggedPanel;
        var srcGroup = SourceGroup;
        var srcManager = SourceManager;

        // Find hit manager
        DockManager? hitManager = s_CurrentTargetManager;
        Point hitLocalPoint = default;

        if (hitManager != null)
        {
            if (!TryGetLocalPoint(hitManager, screenPos, originTopLevel, originWindowPos, out hitLocalPoint))
            {
                hitManager = null;
            }
        }

        // Reset coordinator state
        IsDragging = false;
        DraggedPanel = null;
        SourceGroup = null;
        SourceManager = null;
        s_CurrentTargetManager = null;

        // Clean up overlay on any manager that is NOT the hit manager
        foreach (var dm in s_Managers)
        {
            if (dm != hitManager)
            {
                dm.HideOverlay();
            }
        }

        if (hitManager != null)
        {
            // Note: hitManager.EndDrag will read the active zone and target BEFORE calling its own HideOverlay()
            hitManager.EndDrag(panel, srcGroup, hitLocalPoint);
        }
        else
        {
            // Dropped outside registered dock managers.
            // Check if pointer is still inside the originating top-level window.
            var srcTopLevel = originTopLevel ?? TopLevel.GetTopLevel(srcManager);
            bool isOutsideWindow = true;
            if (srcTopLevel != null)
            {
                try
                {
                    Point windowPt = originWindowPos ?? srcTopLevel.PointToClient(screenPos);
                    if (new Rect(srcTopLevel.Bounds.Size).Contains(windowPt))
                    {
                        isOutsideWindow = false;
                    }
                }
                catch { }
            }

            if (isOutsideWindow && panel.CanFloat)
            {
                // Deliberately dropped onto empty desktop outside all application windows
                srcManager.TearOff(panel, srcGroup, screenPos);
            }
            else
            {
                // Released inside application window or floating disabled: keep it safely in source group
                if (!srcGroup.Panels.Contains(panel))
                {
                    srcGroup.Panels.Add(panel);
                    panel.Parent = srcGroup;
                }
                srcGroup.ActivePanel = panel;
            }
        }
    }

    public static void CancelDrag()
    {
        IsDragging = false;
        DraggedPanel = null;
        SourceGroup = null;
        SourceManager = null;
        s_CurrentTargetManager = null;

        foreach (var dm in s_Managers)
        {
            dm.HideOverlay();
        }
    }
}
