using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.DockingManager.Models;

namespace Avalonia.DockingManager.Controls;

public partial class DockFloatingWindow : Window
{
    private DockManager _dockManager = null!;

    public DockManager Manager => _dockManager;

    public DockFloatingWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _dockManager = this.FindControl<DockManager>("PART_DockManager")!;

        _dockManager.PropertyChanged += (s, e) =>
        {
            if (e.Property == DockManager.LayoutRootProperty)
            {
                if (_dockManager.LayoutRoot == null)
                {
                    // No panels left in this window — close it automatically
                    this.Close();
                }
            }
        };
    }

    public static DockFloatingWindow Create(DockPanelNode panel, PixelPoint screenPosition)
    {
        var window = new DockFloatingWindow
        {
            Title = panel.Title,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = screenPosition
        };

        var newRoot = new DockTabGroupNode();
        newRoot.Panels.Add(panel);
        panel.Parent = newRoot;
        newRoot.ActivePanel = panel;

        window.Manager.LayoutRoot = newRoot;
        return window;
    }
}
