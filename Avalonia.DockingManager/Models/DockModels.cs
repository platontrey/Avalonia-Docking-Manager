using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.DockingManager.Models;

[JsonDerivedType(typeof(DockGroupNode), typeDiscriminator: "group")]
[JsonDerivedType(typeof(DockTabGroupNode), typeDiscriminator: "tabGroup")]
[JsonDerivedType(typeof(DockPanelNode), typeDiscriminator: "panel")]
public abstract partial class DockNode : ObservableObject
{
    // Base class for all nodes in the docking layout tree.
    [ObservableProperty]
    [property: JsonIgnore]
    private DockNode? _parent;

    [ObservableProperty]
    private Avalonia.Controls.GridLength _dockSize = new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star);
}

public partial class DockGroupNode : DockNode
{
    [ObservableProperty]
    private bool _isHorizontal;

    public ObservableCollection<DockNode> Children { get; } = new ObservableCollection<DockNode>();

    public DockGroupNode(bool isHorizontal)
    {
        IsHorizontal = isHorizontal;
    }
}

public partial class DockTabGroupNode : DockNode
{
    public ObservableCollection<DockPanelNode> Panels { get; } = new ObservableCollection<DockPanelNode>();

    [ObservableProperty]
    private DockPanelNode? _activePanel;

    public DockTabGroupNode()
    {
    }
}

public partial class DockPanelNode : DockNode
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = "New Tab";

    [ObservableProperty]
    [property: JsonIgnore]
    private object? _content;
}

public partial class WorkspaceNode : ObservableObject
{
    [ObservableProperty]
    private string _title = "Workspace";

    [ObservableProperty]
    private DockNode? _layoutRoot;
}
