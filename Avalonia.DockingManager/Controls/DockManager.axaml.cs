using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Avalonia.DockingManager.Models;
using Avalonia.DockingManager.Services;

namespace Avalonia.DockingManager.Controls;

public partial class DockManager : UserControl
{
    public static readonly StyledProperty<DockNode?> LayoutRootProperty =
        AvaloniaProperty.Register<DockManager, DockNode?>(nameof(LayoutRoot));

    public DockNode? LayoutRoot
    {
        get => GetValue(LayoutRootProperty);
        set => SetValue(LayoutRootProperty, value);
    }

    /// <summary>
    /// If true, resizing windows will only show a preview line and update layout on release (Better FPS).
    /// If false, windows will resize in real-time.
    /// </summary>
    public static bool ShowSplitterPreview { get; set; } = false;

    private ContentControl _layoutHost = null!;
    private DockDropOverlay _dropOverlay = null!;

    private DockTabGroup? _lastTarget;
    private DockZone _lastZone = DockZone.None;

    public struct DragTargetCache
    {
        public DockTabGroup Group;
        public Rect BoundsInManager;
    }

    private readonly List<DragTargetCache> _cachedDragTargets = new();

    public DockManager()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _layoutHost   = this.FindControl<ContentControl>("LayoutHost")!;
        _dropOverlay  = this.FindControl<DockDropOverlay>("DropOverlay")!;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DockDragCoordinator.Register(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DockDragCoordinator.Unregister(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutRootProperty)
        {
            _layoutHost.Content = LayoutRoot;
        }
    }

    public void PrepareDragTargets()
    {
        _cachedDragTargets.Clear();
        var allGroups = this.GetVisualDescendants().OfType<DockTabGroup>();
        foreach (var g in allGroups)
        {
            var transform = g.TransformToVisual(this);
            if (transform.HasValue)
            {
                var rect = new Rect(g.Bounds.Size).TransformToAABB(transform.Value);
                _cachedDragTargets.Add(new DragTargetCache { Group = g, BoundsInManager = rect });
            }
        }
    }

    public void UpdateDrag(Point positionInManager)
    {
        DragTargetCache? hitTarget = null;
        foreach (var target in _cachedDragTargets)
        {
            if (target.BoundsInManager.Contains(positionInManager))
            {
                hitTarget = target;
                break;
            }
        }

        _lastZone = _dropOverlay.UpdateOverlay(hitTarget?.BoundsInManager, this.Bounds, positionInManager);
        _lastTarget = hitTarget?.Group;
    }

    public void HideOverlay()
    {
        _dropOverlay.Hide();
        _lastTarget = null;
        _lastZone = DockZone.None;
    }

    public void EndDrag(DockPanelNode panel, DockTabGroupNode source, Point dropPositionInManager)
    {
        var zone = _lastZone;
        var targetGroup = _lastTarget?.DataContext as DockTabGroupNode;
        HideOverlay();

        if (panel == null || source == null) return;

        // 1. Root outer edge dock
        if (zone is DockZone.RootTop or DockZone.RootBottom or DockZone.RootLeft or DockZone.RootRight)
        {
            PerformRootDock(panel, source, zone);
            return;
        }

        // 2. Dock into targeted tab group
        if (targetGroup != null && zone != DockZone.None)
        {
            PerformDock(panel, source, targetGroup, zone);
            return;
        }

        // 3. If dropped on empty manager
        if (LayoutRoot == null)
        {
            source.Panels.Remove(panel);
            CleanupEmptyGroup(source);

            var newGroup = new DockTabGroupNode();
            newGroup.Panels.Add(panel);
            panel.Parent = newGroup;
            newGroup.ActivePanel = panel;
            LayoutRoot = newGroup;
            return;
        }

        // 4. Default fallback: dropped inside manager without a valid dock zone.
        // Keep panel safely docked inside its source group instead of creating an OS window!
        if (!source.Panels.Contains(panel))
        {
            source.Panels.Add(panel);
            panel.Parent = source;
        }
        source.ActivePanel = panel;
    }

    public void PerformRootDock(DockPanelNode panel, DockTabGroupNode source, DockZone zone)
    {
        int oldIndex = source.Panels.IndexOf(panel);
        source.Panels.Remove(panel);
        if (source.ActivePanel == panel || source.ActivePanel == null || !source.Panels.Contains(source.ActivePanel))
        {
            if (source.Panels.Count > 0)
            {
                int nextIdx = Math.Clamp(oldIndex, 0, source.Panels.Count - 1);
                source.ActivePanel = source.Panels[nextIdx];
            }
            else
            {
                source.ActivePanel = null;
            }
        }

        var newGroup = new DockTabGroupNode();
        newGroup.Panels.Add(panel);
        panel.Parent = newGroup;
        newGroup.ActivePanel = panel;

        var oldRoot = LayoutRoot;
        if (oldRoot == null)
        {
            LayoutRoot = newGroup;
            CleanupEmptyGroup(source);
            return;
        }

        bool isHorizontal = zone is DockZone.RootLeft or DockZone.RootRight;
        var newRoot = new DockGroupNode(isHorizontal);

        if (isHorizontal)
        {
            newGroup.DockSize = new GridLength(0.25, GridUnitType.Star);
            oldRoot.DockSize  = new GridLength(0.75, GridUnitType.Star);
        }
        else
        {
            newGroup.DockSize = new GridLength(0.3, GridUnitType.Star);
            oldRoot.DockSize  = new GridLength(0.7, GridUnitType.Star);
        }

        if (zone is DockZone.RootLeft or DockZone.RootTop)
        {
            newRoot.Children.Add(newGroup);
            newRoot.Children.Add(oldRoot);
        }
        else
        {
            newRoot.Children.Add(oldRoot);
            newRoot.Children.Add(newGroup);
        }

        newGroup.Parent = newRoot;
        oldRoot.Parent = newRoot;
        LayoutRoot = newRoot;

        CleanupEmptyGroup(source);
    }

    public void PerformDock(DockPanelNode panel, DockTabGroupNode source, DockTabGroupNode target, DockZone zone)
    {
        if (panel == null || source == null || target == null) return;
        if (source == target && (zone == DockZone.Center || source.Panels.Count <= 1)) return;

        // Remove from source group
        int oldIndex = source.Panels.IndexOf(panel);
        source.Panels.Remove(panel);
        if (source.ActivePanel == panel || source.ActivePanel == null || !source.Panels.Contains(source.ActivePanel))
        {
            if (source.Panels.Count > 0)
            {
                int nextIdx = Math.Clamp(oldIndex, 0, source.Panels.Count - 1);
                source.ActivePanel = source.Panels[nextIdx];
            }
            else
            {
                source.ActivePanel = null;
            }
        }

        if (zone == DockZone.Center)
        {
            target.Panels.Add(panel);
            panel.Parent = target;
            target.ActivePanel = panel;
        }
        else
        {
            var parentGroup = target.Parent as DockGroupNode;
            bool needHorizontal = zone is DockZone.Left or DockZone.Right;

            var newGroup = new DockTabGroupNode();
            newGroup.Panels.Add(panel);
            panel.Parent = newGroup;
            newGroup.ActivePanel = panel;

            if (parentGroup == null)
            {
                if (LayoutRoot == target)
                {
                    var newRoot = new DockGroupNode(needHorizontal);
                    newGroup.DockSize = new GridLength(0.5, GridUnitType.Star);
                    target.DockSize   = new GridLength(0.5, GridUnitType.Star);

                    if (zone is DockZone.Left or DockZone.Top)
                    {
                        newRoot.Children.Add(newGroup);
                        newRoot.Children.Add(target);
                    }
                    else
                    {
                        newRoot.Children.Add(target);
                        newRoot.Children.Add(newGroup);
                    }
                    target.Parent   = newRoot;
                    newGroup.Parent = newRoot;
                    LayoutRoot      = newRoot;
                }
            }
            else
            {
                int index = parentGroup.Children.IndexOf(target);

                if (parentGroup.IsHorizontal == needHorizontal)
                {
                    newGroup.DockSize = new GridLength(0.5, GridUnitType.Star);
                    target.DockSize   = new GridLength(0.5, GridUnitType.Star);

                    if (zone is DockZone.Left or DockZone.Top)
                        parentGroup.Children.Insert(index, newGroup);
                    else
                        parentGroup.Children.Insert(index + 1, newGroup);

                    newGroup.Parent = parentGroup;
                }
                else
                {
                    var wrapper = new DockGroupNode(needHorizontal);
                    wrapper.DockSize = target.DockSize;
                    parentGroup.Children[index] = wrapper;
                    wrapper.Parent = parentGroup;

                    newGroup.DockSize = new GridLength(0.5, GridUnitType.Star);
                    target.DockSize   = new GridLength(0.5, GridUnitType.Star);

                    if (zone is DockZone.Left or DockZone.Top)
                    {
                        wrapper.Children.Add(newGroup);
                        wrapper.Children.Add(target);
                    }
                    else
                    {
                        wrapper.Children.Add(target);
                        wrapper.Children.Add(newGroup);
                    }
                    newGroup.Parent = wrapper;
                    target.Parent   = wrapper;
                }
            }
        }

        CleanupEmptyGroup(source);
    }

    public void TearOff(DockPanelNode panel, DockTabGroupNode source, PixelPoint screenPosition)
    {
        if (!panel.CanFloat) return;

        int oldIndex = source.Panels.IndexOf(panel);
        source.Panels.Remove(panel);
        if (source.ActivePanel == panel || source.ActivePanel == null || !source.Panels.Contains(source.ActivePanel))
        {
            if (source.Panels.Count > 0)
            {
                int nextIdx = Math.Clamp(oldIndex, 0, source.Panels.Count - 1);
                source.ActivePanel = source.Panels[nextIdx];
            }
            else
            {
                source.ActivePanel = null;
            }
        }
        CleanupEmptyGroup(source);

        var floatingWindow = DockFloatingWindow.Create(panel, screenPosition);
        floatingWindow.Show();
    }

    public void CleanupEmptyGroup(DockTabGroupNode source)
    {
        if (source.Panels.Count == 0)
        {
            if (source.Parent is DockGroupNode srcParent)
            {
                srcParent.Children.Remove(source);
                if (srcParent.Children.Count == 1)
                {
                    var remaining = srcParent.Children[0];
                    if (srcParent.Parent is DockGroupNode grandParent)
                    {
                        int idx = grandParent.Children.IndexOf(srcParent);
                        grandParent.Children[idx] = remaining;
                        remaining.Parent = grandParent;
                        remaining.DockSize = srcParent.DockSize;
                    }
                    else if (LayoutRoot == srcParent)
                    {
                        LayoutRoot = remaining;
                        remaining.Parent = null;
                    }
                }
                else if (srcParent.Children.Count == 0)
                {
                    if (srcParent.Parent is DockGroupNode grandParent)
                    {
                        grandParent.Children.Remove(srcParent);
                    }
                    else if (LayoutRoot == srcParent)
                    {
                        LayoutRoot = null;
                    }
                }
            }
            else if (LayoutRoot == source)
            {
                LayoutRoot = null;
            }
        }
    }
}