using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.DockingManager.Models;

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DockingManager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<WorkspaceNode> Workspaces { get; } = new ObservableCollection<WorkspaceNode>();

    [ObservableProperty]
    private WorkspaceNode? _activeWorkspace;

    public MainWindowViewModel()
    {
        // 1. Level Workspace
        var levelWorkspace = new WorkspaceNode { Title = "Level" };
        levelWorkspace.LayoutRoot = CreateMockLayout();
        
        // 2. Material Workspace
        var materialWorkspace = new WorkspaceNode { Title = "Material" };
        
        var matLeft = new DockTabGroupNode();
        matLeft.Panels.Add(new DockPanelNode { Title = "Palette", Content = "Material Nodes..." });
        matLeft.ActivePanel = matLeft.Panels[0];

        var matCenter = new DockTabGroupNode();
        matCenter.Panels.Add(new DockPanelNode { Title = "Graph", Content = "[ Node Graph View ]" });
        matCenter.ActivePanel = matCenter.Panels[0];

        var matRoot = new DockGroupNode(isHorizontal: true);
        matRoot.Children.Add(matLeft);
        matRoot.Children.Add(matCenter);
        matLeft.Parent = matRoot;
        matCenter.Parent = matRoot;
        materialWorkspace.LayoutRoot = matRoot;

        Workspaces.Add(levelWorkspace);
        Workspaces.Add(materialWorkspace);

        ActiveWorkspace = levelWorkspace;
    }

    private DockNode CreateMockLayout()
    {
        var leftGroup = new DockTabGroupNode();
        leftGroup.DockSize = new Avalonia.Controls.GridLength(250, Avalonia.Controls.GridUnitType.Pixel);
        leftGroup.Panels.Add(new DockPanelNode { Id = "explorer", Title = "Solution Explorer", Content = "Files here..." });
        leftGroup.ActivePanel = leftGroup.Panels[0];

        var centerGroup = new DockTabGroupNode();
        centerGroup.Panels.Add(new DockPanelNode { Id = "editor", Title = "MainWindow.axaml", Content = "<Window>...</Window>" });
        centerGroup.Panels.Add(new DockPanelNode { Title = "MainWindow.axaml.cs", Content = "class MainWindow { }" });
        centerGroup.ActivePanel = centerGroup.Panels[0];

        var rightGroup = new DockTabGroupNode();
        rightGroup.Panels.Add(new DockPanelNode { Title = "Properties", Content = "Properties view" });
        rightGroup.ActivePanel = rightGroup.Panels[0];

        var bottomGroup = new DockTabGroupNode();
        bottomGroup.DockSize = new Avalonia.Controls.GridLength(200, Avalonia.Controls.GridUnitType.Pixel);
        bottomGroup.Panels.Add(new DockPanelNode { Id = "output", Title = "Output", Content = "Build started..." });
        bottomGroup.Panels.Add(new DockPanelNode { Id = "terminal", Title = "Terminal", Content = "> dotnet run" });
        bottomGroup.ActivePanel = bottomGroup.Panels[0];

        foreach(var p in leftGroup.Panels) p.Parent = leftGroup;
        foreach(var p in centerGroup.Panels) p.Parent = centerGroup;
        foreach(var p in rightGroup.Panels) p.Parent = rightGroup;
        foreach(var p in bottomGroup.Panels) p.Parent = bottomGroup;

        var topRow = new DockGroupNode(isHorizontal: true);
        topRow.Children.Add(leftGroup);
        topRow.Children.Add(centerGroup);
        topRow.Children.Add(rightGroup);
        leftGroup.Parent = topRow;
        centerGroup.Parent = topRow;
        rightGroup.Parent = topRow;

        var root = new DockGroupNode(isHorizontal: false);
        root.Children.Add(topRow);
        root.Children.Add(bottomGroup);
        topRow.Parent = root;
        bottomGroup.Parent = root;

        return root;
    }

    [RelayCommand]
    private void SaveLayout()
    {
        if (ActiveWorkspace == null) return;
        var json = Avalonia.DockingManager.Serialization.DockSerializer.Serialize(ActiveWorkspace);
        System.IO.File.WriteAllText("layout.json", json);
        Console.WriteLine("Layout saved to layout.json");
    }

    [RelayCommand]
    private void LoadLayout()
    {
        if (!System.IO.File.Exists("layout.json")) return;
        var json = System.IO.File.ReadAllText("layout.json");
        var loadedWorkspace = Avalonia.DockingManager.Serialization.DockSerializer.Deserialize(json);
        if (loadedWorkspace != null && ActiveWorkspace != null)
        {
            // Inject fake content based on IDs since content is ignored in JSON
            InjectDummyContent(loadedWorkspace.LayoutRoot);
            
            // Update the LayoutRoot of the current workspace in-place
            // This avoids destroying and recreating the TabItem, preventing UI disappearing bugs
            ActiveWorkspace.LayoutRoot = loadedWorkspace.LayoutRoot;
            
            Console.WriteLine("Layout loaded!");
        }
    }

    private void InjectDummyContent(DockNode? node)
    {
        if (node == null) return;
        if (node is DockGroupNode group)
        {
            foreach (var child in group.Children) InjectDummyContent(child);
        }
        else if (node is DockTabGroupNode tabGroup)
        {
            foreach (var panel in tabGroup.Panels) InjectDummyContent(panel);
        }
        else if (node is DockPanelNode panel)
        {
            panel.Content = $"Restored: {panel.Title}";
        }
    }
}
