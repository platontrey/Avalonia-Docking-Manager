using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.DockingManager.Models;

namespace Avalonia.DockingManager.Serialization;

public class GridLengthJsonConverter : JsonConverter<GridLength>
{
    public override GridLength Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (string.IsNullOrEmpty(str))
            return new GridLength(1, GridUnitType.Star);

        if (str.EndsWith("*"))
        {
            if (str == "*") return new GridLength(1, GridUnitType.Star);
            if (double.TryParse(str.TrimEnd('*'), out double val))
                return new GridLength(val, GridUnitType.Star);
        }
        else if (str.ToLower().Equals("auto"))
        {
            return new GridLength(0, GridUnitType.Auto);
        }
        else
        {
            if (double.TryParse(str, out double val))
                return new GridLength(val, GridUnitType.Pixel);
        }

        return new GridLength(1, GridUnitType.Star);
    }

    public override void Write(Utf8JsonWriter writer, GridLength value, JsonSerializerOptions options)
    {
        if (value.IsStar)
        {
            writer.WriteStringValue($"{value.Value}*");
        }
        else if (value.IsAuto)
        {
            writer.WriteStringValue("Auto");
        }
        else
        {
            writer.WriteStringValue(value.Value.ToString());
        }
    }
}

public static class DockSerializer
{
    private static JsonSerializerOptions GetOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        options.Converters.Add(new GridLengthJsonConverter());
        return options;
    }

    public static string Serialize(WorkspaceNode workspace)
    {
        return JsonSerializer.Serialize(workspace, GetOptions());
    }

    public static WorkspaceNode? Deserialize(string json)
    {
        var workspace = JsonSerializer.Deserialize<WorkspaceNode>(json, GetOptions());
        if (workspace != null && workspace.LayoutRoot != null)
        {
            // Restore parent references manually because we ignored them to avoid cycles
            RestoreParents(workspace.LayoutRoot, null);
        }
        return workspace;
    }

    private static void RestoreParents(DockNode node, DockNode? parent)
    {
        node.Parent = parent;
        
        if (node is DockGroupNode groupNode)
        {
            foreach (var child in groupNode.Children)
            {
                RestoreParents(child, groupNode);
            }
        }
        else if (node is DockTabGroupNode tabGroup)
        {
            foreach (var panel in tabGroup.Panels)
            {
                RestoreParents(panel, tabGroup);
            }

            // In JSON, ActivePanel is deserialized as a duplicate object. 
            // We must find the matching instance in Panels and point to it.
            if (tabGroup.ActivePanel != null)
            {
                var match = System.Linq.Enumerable.FirstOrDefault(tabGroup.Panels, p => p.Id == tabGroup.ActivePanel.Id && p.Title == tabGroup.ActivePanel.Title);
                if (match != null)
                {
                    tabGroup.ActivePanel = match;
                }
            }
            else if (tabGroup.Panels.Count > 0)
            {
                tabGroup.ActivePanel = tabGroup.Panels[0];
            }
        }
    }
}
