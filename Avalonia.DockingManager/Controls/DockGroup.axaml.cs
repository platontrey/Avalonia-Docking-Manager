using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.DockingManager.Models;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;
using Avalonia.Input;

namespace Avalonia.DockingManager.Controls;

public partial class DockGroup : UserControl
{
    private Grid _grid = null!;
    private readonly IBrush _splitterHoverBrush = new SolidColorBrush(Color.FromArgb(140, 0, 122, 204));

    public DockGroup()
    {
        InitializeComponent();
        _grid = this.FindControl<Grid>("PART_Grid")!;
        LayoutUpdated += (s, e) => SyncSizesBack();
    }

    private bool _isSyncing;

    private void SyncSizesBack()
    {
        if (_isSyncing || DataContext is not DockGroupNode node) return;
        _isSyncing = true;
        try
        {
            if (node.IsHorizontal && _grid.ColumnDefinitions.Count == node.Children.Count * 2 - 1)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    var newWidth = _grid.ColumnDefinitions[i * 2].Width;
                    if (Math.Abs(node.Children[i].DockSize.Value - newWidth.Value) > 0.001 ||
                        node.Children[i].DockSize.GridUnitType != newWidth.GridUnitType)
                    {
                        node.Children[i].DockSize = newWidth;
                    }
                }
            }
            else if (!node.IsHorizontal && _grid.RowDefinitions.Count == node.Children.Count * 2 - 1)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    var newHeight = _grid.RowDefinitions[i * 2].Height;
                    if (Math.Abs(node.Children[i].DockSize.Value - newHeight.Value) > 0.001 ||
                        node.Children[i].DockSize.GridUnitType != newHeight.GridUnitType)
                    {
                        node.Children[i].DockSize = newHeight;
                    }
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private DockGroupNode? _currentNode;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_currentNode != null)
        {
            _currentNode.Children.CollectionChanged -= Children_CollectionChanged;
        }

        if (DataContext is DockGroupNode node)
        {
            _currentNode = node;
            RebuildGrid(node);
            node.Children.CollectionChanged += Children_CollectionChanged;
        }
        else
        {
            _currentNode = null;
        }
    }

    private void Children_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_currentNode != null) RebuildGrid(_currentNode);
    }

    private bool _isRebuildPending;

    private void RebuildGrid(DockGroupNode node)
    {
        if (_isRebuildPending) return;
        _isRebuildPending = true;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _isRebuildPending = false;
            ActualRebuildGrid(node);
        }, Avalonia.Threading.DispatcherPriority.Normal);
    }

    private void ActualRebuildGrid(DockGroupNode node)
    {
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _grid.ColumnDefinitions.Clear();

        if (node.Children.Count == 0) return;

        for (int i = 0; i < node.Children.Count; i++)
        {
            var childNode = node.Children[i];

            // Render the child node using ContentControl and DataTemplates
            var contentControl = new ContentControl
            {
                Content = childNode,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            if (node.IsHorizontal)
            {
                contentControl.MinWidth = 120;
                var colDef = new ColumnDefinition { Width = childNode.DockSize, MinWidth = 120 };
                _grid.ColumnDefinitions.Add(colDef);
                Grid.SetColumn(contentControl, i * 2);
            }
            else
            {
                contentControl.MinHeight = 80;
                var rowDef = new RowDefinition { Height = childNode.DockSize, MinHeight = 80 };
                _grid.RowDefinitions.Add(rowDef);
                Grid.SetRow(contentControl, i * 2);
            }

            _grid.Children.Add(contentControl);

            // Add splitter if not last item
            if (i < node.Children.Count - 1)
            {
                int leftIdx = i;
                int rightIdx = i + 1;

                var splitter = new GridSplitter
                {
                    Background = Brushes.Transparent,
                    ResizeDirection = node.IsHorizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    ShowsPreview = DockManager.ShowSplitterPreview,
                    Cursor = node.IsHorizontal ? new Cursor(StandardCursorType.SizeWestEast) : new Cursor(StandardCursorType.SizeNorthSouth)
                };

                // Visual hover highlight on splitter
                splitter.PointerEntered += (s, e) => splitter.Background = _splitterHoverBrush;
                splitter.PointerExited  += (s, e) => splitter.Background = Brushes.Transparent;

                // Double click: reset adjacent panes to 50/50 equal proportions
                splitter.DoubleTapped += (s, e) =>
                {
                    if (DataContext is DockGroupNode gNode && leftIdx < gNode.Children.Count && rightIdx < gNode.Children.Count)
                    {
                        gNode.Children[leftIdx].DockSize  = new GridLength(1, GridUnitType.Star);
                        gNode.Children[rightIdx].DockSize = new GridLength(1, GridUnitType.Star);
                        RebuildGrid(gNode);
                    }
                };

                if (node.IsHorizontal)
                {
                    _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                    splitter.Width = 6;
                    splitter.HorizontalAlignment = HorizontalAlignment.Center;
                    splitter.VerticalAlignment = VerticalAlignment.Stretch;
                    Grid.SetColumn(splitter, i * 2 + 1);
                }
                else
                {
                    _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    splitter.Height = 6;
                    splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                    splitter.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetRow(splitter, i * 2 + 1);
                }
                _grid.Children.Add(splitter);
            }
        }
    }
}