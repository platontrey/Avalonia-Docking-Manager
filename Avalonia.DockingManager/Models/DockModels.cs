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

    public DockGroupNode() : this(true)
    {
    }

    public DockGroupNode(bool isHorizontal)
    {
        IsHorizontal = isHorizontal;
        Children.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (DockNode item in e.NewItems)
                {
                    item.Parent = this;
                }
            }
            if (e.OldItems != null)
            {
                foreach (DockNode item in e.OldItems)
                {
                    if (item.Parent == this)
                        item.Parent = null;
                }
            }
        };
    }
}

public partial class DockTabGroupNode : DockNode
{
    public ObservableCollection<DockPanelNode> Panels { get; } = new ObservableCollection<DockPanelNode>();

    [ObservableProperty]
    private DockPanelNode? _activePanel;

    public DockTabGroupNode()
    {
        Panels.CollectionChanged += OnPanelsCollectionChanged;
    }

    private void OnPanelsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (DockPanelNode item in e.NewItems)
            {
                item.Parent = this;
            }
        }
        if (e.OldItems != null)
        {
            foreach (DockPanelNode item in e.OldItems)
            {
                if (item.Parent == this)
                    item.Parent = null;
            }
        }

        if (Panels.Count > 0)
        {
            if (ActivePanel == null || !Panels.Contains(ActivePanel))
            {
                int targetIdx = 0;
                if (e.OldStartingIndex >= 0)
                {
                    targetIdx = System.Math.Clamp(e.OldStartingIndex, 0, Panels.Count - 1);
                }
                else
                {
                    targetIdx = System.Math.Clamp(Panels.Count - 1, 0, Panels.Count - 1);
                }

                ActivePanel = Panels[targetIdx];
            }
        }
        else
        {
            ActivePanel = null;
        }

        SyncActiveStates();
    }

    partial void OnActivePanelChanged(DockPanelNode? value)
    {
        if (value == null && Panels.Count > 0)
        {
            ActivePanel = Panels[Panels.Count - 1];
            return;
        }

        SyncActiveStates();
    }

    /// <summary>
    /// Mirrors <see cref="ActivePanel"/> onto every panel. The view keeps all
    /// panels realised and toggles visibility instead of swapping content, so a
    /// panel hosting a NativeControlHost (the Vulkan viewport) is never
    /// destroyed and recreated by a tab switch.
    /// </summary>
    private void SyncActiveStates()
    {
        foreach (var panel in Panels)
        {
            panel.IsActive = ReferenceEquals(panel, ActivePanel);
        }
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

    /// <summary>Set by the owning <see cref="DockTabGroupNode"/>; drives visibility.</summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private bool _isActive;

    /// <summary>If false, the close button is hidden and the tab cannot be closed.</summary>
    [ObservableProperty]
    private bool _canClose = true;

    /// <summary>If true, the tab can be detached into a floating window.</summary>
    [ObservableProperty]
    private bool _canFloat = true;

    /// <summary>If true, the tab can be dragged to re-dock.</summary>
    [ObservableProperty]
    private bool _canDrag = true;
}

public partial class WorkspaceNode : ObservableObject
{
    [ObservableProperty]
    private string _title = "Workspace";

    /// <summary>A pinned workspace cannot be closed by the user.</summary>
    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private DockNode? _layoutRoot;

    [ObservableProperty]
    private bool _isActive;
}
