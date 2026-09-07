using System;
using Avalonia.Controls;
using Avalonia.DockingManager.Models;

namespace Avalonia.DockingManager;

/// <summary>
/// Fluent builder and declarative layout DSL for Avalonia.DockingManager.
/// Simplifies creation of rows, columns, and tab groups without boilerplate.
/// </summary>
public static class DockLayout
{
    /// <summary>
    /// Creates a horizontal split group containing the specified child nodes.
    /// </summary>
    public static DockGroupNode Row(params DockNode[] children)
    {
        var group = new DockGroupNode(true);
        foreach (var child in children)
        {
            group.Children.Add(child);
        }
        return group;
    }

    /// <summary>
    /// Creates a vertical split group containing the specified child nodes.
    /// </summary>
    public static DockGroupNode Column(params DockNode[] children)
    {
        var group = new DockGroupNode(false);
        foreach (var child in children)
        {
            group.Children.Add(child);
        }
        return group;
    }

    /// <summary>
    /// Creates a tab group containing the specified panels, activating the first panel.
    /// </summary>
    public static DockTabGroupNode Tabs(params DockPanelNode[] panels)
    {
        var group = new DockTabGroupNode();
        foreach (var panel in panels)
        {
            group.Panels.Add(panel);
        }
        if (panels.Length > 0)
        {
            group.ActivePanel = panels[0];
        }
        return group;
    }

    /// <summary>
    /// Creates a tab group with a specified relative star size containing the specified panels.
    /// </summary>
    public static DockTabGroupNode Tabs(double starSize, params DockPanelNode[] panels)
    {
        var group = Tabs(panels);
        group.DockSize = new GridLength(starSize, GridUnitType.Star);
        return group;
    }

    /// <summary>
    /// Fluently sets the relative star size on any dock node.
    /// </summary>
    public static T WithSize<T>(this T node, double starSize) where T : DockNode
    {
        node.DockSize = new GridLength(starSize, GridUnitType.Star);
        return node;
    }

    /// <summary>
    /// Fluently adds a panel to a tab group and optionally selects it as the active panel.
    /// </summary>
    public static DockTabGroupNode Add(this DockTabGroupNode group, DockPanelNode panel, bool makeActive = true)
    {
        group.Panels.Add(panel);
        if (makeActive)
        {
            group.ActivePanel = panel;
        }
        return group;
    }
}
