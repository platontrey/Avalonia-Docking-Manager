# Avalonia Docking Manager

A highly customizable, MVVM-friendly Docking Manager for **Avalonia UI**, heavily inspired by the window management systems of **Unreal Engine 5** and **JetBrains IDEs**. 

## Features

- 🏗 **MVVM First**: The entire docking layout is represented by an observable tree of view-models. The UI is completely decoupled from the layout state.
- 🗂 **Workspaces (Major Windows)**: Support for multiple independent layout workspaces (like "Level" and "Material" tabs in UE5). Switching a workspace swaps the entire docking tree instantly.
- 🪟 **Tear-off Windows**: Drag a tab outside the main window to instantly detach it into its own floating Avalonia Window, complete with its own child `DockManager`.
- 🧩 **Advanced Docking Zones**: Drag a tab over any existing panel to see a docking overlay. Drop it in the Center to create tabs, or on the Top/Right/Bottom/Left to automatically split the area.
- 📏 **Automatic Splitters**: Grid splitters are automatically generated and injected into the visual tree between panels, allowing users to seamlessly resize docked sections.
- 🚀 **High Performance Drag & Drop**: Uses custom pointer capture and bounding-box caching to maintain 1000+ FPS during complex drag operations.
- 🎨 **Fully Stylable**: Built using standard Avalonia `DataTemplates` and `ControlThemes`. You have complete control over how the tabs, borders, and window panels look.

## Quick Start

### 1. Add the Library
Reference the `Avalonia.DockingManager` class library or NuGet package in your main Avalonia project.

### 2. Define the Layout in your ViewModel
Create the layout structure using the provided node types:

```csharp
using Avalonia.DockingManager.Models;

public class MainWindowViewModel 
{
    public DockNode LayoutRoot { get; set; }

    public MainWindowViewModel()
    {
        // Create a root tab group
        var rootGroup = new DockTabGroupNode();
        
        // Add a panel
        rootGroup.Panels.Add(new DockPanelNode 
        { 
            Title = "Solution Explorer", 
            Content = "Your UI control or View-Model here" 
        });
        
        rootGroup.ActivePanel = rootGroup.Panels[0];

        // Assign to root
        LayoutRoot = rootGroup;
    }
}
```

### 3. Render the Dock Manager
In your main window's XAML, bind the `DockManager` to your `LayoutRoot`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:dock="clr-namespace:Avalonia.DockingManager.Controls;assembly=Avalonia.DockingManager"
        x:Class="YourApp.MainWindow">
        
    <dock:DockManager LayoutRoot="{Binding LayoutRoot}" />

</Window>
```

For more detailed technical information on how to interact with the visual tree, create custom zones, or override styles, please see [DOCUMENTATION.md](DOCUMENTATION.md).
