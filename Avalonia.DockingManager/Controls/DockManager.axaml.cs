using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Avalonia.DockingManager.Models;

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

    private ContentControl _layoutHost;
    private Canvas _overlayCanvas;
    private Rectangle _highlightRect;

    private DockPanelNode? _draggedPanel;
    private DockTabGroupNode? _sourceGroup;
    private bool _isDragging;
    private DockTabGroup? _lastTarget;
    private DockZone _lastZone;

    public DockManager()
    {
        InitializeComponent();
        _layoutHost = this.FindControl<ContentControl>("LayoutHost")!;
        _overlayCanvas = this.FindControl<Canvas>("OverlayCanvas")!;

        _highlightRect = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(100, 0, 122, 204)),
            IsVisible = false
        };
        _overlayCanvas.Children.Add(_highlightRect);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutRootProperty)
        {
            _layoutHost.Content = LayoutRoot;
        }
    }

    private struct DragTargetCache
    {
        public DockTabGroup Group;
        public Rect BoundsInManager;
    }
    
    private System.Collections.Generic.List<DragTargetCache> _cachedDragTargets = new();

    public void ShowOverlay(Rect rectInManager, DockZone zone)
    {
        _highlightRect.IsVisible = true;
        
        double x = rectInManager.X;
        double y = rectInManager.Y;
        double w = rectInManager.Width;
        double h = rectInManager.Height;

        switch (zone)
        {
            case DockZone.Top:
                h /= 2;
                break;
            case DockZone.Bottom:
                y += h / 2;
                h /= 2;
                break;
            case DockZone.Left:
                w /= 2;
                break;
            case DockZone.Right:
                x += w / 2;
                w /= 2;
                break;
            case DockZone.Center:
                // Full rect
                break;
        }

        Canvas.SetLeft(_highlightRect, x);
        Canvas.SetTop(_highlightRect, y);
        _highlightRect.Width = w;
        _highlightRect.Height = h;
    }

    public void HideOverlay()
    {
        _highlightRect.IsVisible = false;
    }

    public void BeginDrag(DockPanelNode panel, DockTabGroupNode source)
    {
        _draggedPanel = panel;
        _sourceGroup = source;
        _isDragging = true;

        // OPTIMIZATION: Cache visual descendants and their bounds ONCE when drag begins.
        // This avoids costly visual tree traversal and matrix transforms 60+ times per second.
        _cachedDragTargets.Clear();
        var allGroups = this.GetVisualDescendants().OfType<DockTabGroup>();
        foreach(var g in allGroups)
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
        if (!_isDragging) return;

        DragTargetCache? hitTarget = null;
        foreach(var target in _cachedDragTargets)
        {
            if (target.BoundsInManager.Contains(positionInManager))
            {
                hitTarget = target;
                break;
            }
        }
        
        if (hitTarget != null)
        {
            // Calculate relative position within the target bounds
            double relX = positionInManager.X - hitTarget.Value.BoundsInManager.X;
            double relY = positionInManager.Y - hitTarget.Value.BoundsInManager.Y;
            var posInTarget = new Point(relX, relY);

            var zone = GetZone(posInTarget, hitTarget.Value.Group.Bounds.Size);
            ShowOverlay(hitTarget.Value.BoundsInManager, zone);
            
            _lastTarget = hitTarget.Value.Group;
            _lastZone = zone;
        }
        else 
        {
            HideOverlay();
            _lastTarget = null;
        }
    }

    public void EndDrag(Point dropPositionInManager)
    {
        if (_isDragging && _draggedPanel != null && _sourceGroup != null)
        {
            if (_lastTarget != null)
            {
                var targetNode = _lastTarget.DataContext as DockTabGroupNode;
                PerformDock(_draggedPanel, _sourceGroup, targetNode, _lastZone);
            }
            else
            {
                TearOff(_draggedPanel, _sourceGroup, dropPositionInManager);
            }
        }
        _isDragging = false;
        HideOverlay();
        _lastTarget = null;
        _draggedPanel = null;
        _sourceGroup = null;
    }

    private void TearOff(DockPanelNode panel, DockTabGroupNode source, Point dropPosition)
    {
        // Remove from source
        source.Panels.Remove(panel);
        CleanupEmptyGroup(source);

        var screenPoint = this.PointToScreen(dropPosition);

        var floatingWindow = new Window
        {
            Title = panel.Title,
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = screenPoint,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E"))
        };

        var newRoot = new DockTabGroupNode();
        newRoot.Panels.Add(panel);
        newRoot.ActivePanel = panel;

        var childManager = new DockManager
        {
            LayoutRoot = newRoot
        };

        floatingWindow.Content = childManager;
        floatingWindow.Show();
    }

    private DockTabGroup? FindTargetTabGroup(Point p)
    {
        var allGroups = this.GetVisualDescendants().OfType<DockTabGroup>();
        foreach(var g in allGroups)
        {
            var transform = this.TransformToVisual(g);
            if (transform.HasValue)
            {
                var pInG = p.Transform(transform.Value);
                if (new Rect(g.Bounds.Size).Contains(pInG))
                {
                    return g;
                }
            }
        }
        return null;
    }

    private DockZone GetZone(Point p, Size bounds)
    {
        double normalizedX = p.X / bounds.Width;
        double normalizedY = p.Y / bounds.Height;

        if (normalizedX > 0.25 && normalizedX < 0.75 && normalizedY > 0.25 && normalizedY < 0.75)
            return DockZone.Center;

        double distTop = normalizedY;
        double distBottom = 1.0 - normalizedY;
        double distLeft = normalizedX;
        double distRight = 1.0 - normalizedX;

        double min = Math.Min(Math.Min(distTop, distBottom), Math.Min(distLeft, distRight));

        if (min == distTop) return DockZone.Top;
        if (min == distBottom) return DockZone.Bottom;
        if (min == distLeft) return DockZone.Left;
        return DockZone.Right;
    }

    public void PerformDock(DockPanelNode panel, DockTabGroupNode source, DockTabGroupNode target, DockZone zone)
    {
        if (panel == null || source == null || target == null) return;
        if (source == target && zone == DockZone.Center) return; // Dropping on same group center does nothing

        // Remove from source
        source.Panels.Remove(panel);

        if (zone == DockZone.Center)
        {
            target.Panels.Add(panel);
            target.ActivePanel = panel;
        }
        else
        {
            var parentGroup = target.Parent as DockGroupNode;
            if (parentGroup == null)
            {
                if (LayoutRoot == target)
                {
                    bool isHorizontal = zone == DockZone.Left || zone == DockZone.Right;
                    var newRoot = new DockGroupNode(isHorizontal);
                    var newGroup = new DockTabGroupNode();
                    newGroup.Panels.Add(panel);
                    newGroup.ActivePanel = panel;

                    if (zone == DockZone.Left || zone == DockZone.Top)
                    {
                        newRoot.Children.Add(newGroup);
                        newRoot.Children.Add(target);
                    }
                    else
                    {
                        newRoot.Children.Add(target);
                        newRoot.Children.Add(newGroup);
                    }
                    target.Parent = newRoot;
                    newGroup.Parent = newRoot;
                    LayoutRoot = newRoot;
                }
            }
            else
            {
                bool needHorizontal = zone == DockZone.Left || zone == DockZone.Right;
                var newGroup = new DockTabGroupNode();
                newGroup.Panels.Add(panel);
                newGroup.ActivePanel = panel;

                int index = parentGroup.Children.IndexOf(target);

                if (parentGroup.IsHorizontal == needHorizontal)
                {
                    if (zone == DockZone.Left || zone == DockZone.Top)
                        parentGroup.Children.Insert(index, newGroup);
                    else
                        parentGroup.Children.Insert(index + 1, newGroup);
                    newGroup.Parent = parentGroup;
                }
                else
                {
                    var wrapper = new DockGroupNode(needHorizontal);
                    parentGroup.Children[index] = wrapper;
                    wrapper.Parent = parentGroup;

                    if (zone == DockZone.Left || zone == DockZone.Top)
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
                    target.Parent = wrapper;
                }
            }
        }
        
        CleanupEmptyGroup(source);
    }

    private void CleanupEmptyGroup(DockTabGroupNode source)
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
                    }
                    else if (LayoutRoot == srcParent)
                    {
                        LayoutRoot = remaining;
                        remaining.Parent = null;
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