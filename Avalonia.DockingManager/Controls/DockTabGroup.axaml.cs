using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.DockingManager.Models;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;

namespace Avalonia.DockingManager.Controls;

public enum DockZone
{
    Top,
    Bottom,
    Left,
    Right,
    Center,
    None
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

    public void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(null);
            _isPressed = true;
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
            var manager = this.GetVisualAncestors().OfType<DockManager>().FirstOrDefault();
            manager?.EndDrag(e.GetPosition(manager));
        }
    }

    public void OnTabPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPressed) return;

        var manager = this.GetVisualAncestors().OfType<DockManager>().FirstOrDefault();
        if (manager == null) return;

        var point = e.GetPosition(null);

        if (!_isDragging)
        {
            var diff = point - _dragStartPoint;
            if (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3)
            {
                _isDragging = true;
                if (sender is Control control && control.DataContext is DockPanelNode panelNode)
                {
                    manager.BeginDrag(panelNode, this.DataContext as DockTabGroupNode);
                }
            }
        }

        if (_isDragging)
        {
            manager.UpdateDrag(e.GetPosition(manager));
        }
    }
}