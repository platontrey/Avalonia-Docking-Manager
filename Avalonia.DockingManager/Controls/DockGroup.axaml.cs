using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.DockingManager.Models;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;

namespace Avalonia.DockingManager.Controls;

public partial class DockGroup : UserControl
{
    private Grid _grid;

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
                    node.Children[i].DockSize = _grid.ColumnDefinitions[i * 2].Width;
                }
            }
            else if (!node.IsHorizontal && _grid.RowDefinitions.Count == node.Children.Count * 2 - 1)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    node.Children[i].DockSize = _grid.RowDefinitions[i * 2].Height;
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

    protected override void OnDataContextChanged(System.EventArgs e)
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

    private void Children_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
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
                var colDef = new ColumnDefinition();
                colDef.Bind(ColumnDefinition.WidthProperty, new Binding(nameof(DockNode.DockSize)) { Source = childNode, Mode = BindingMode.TwoWay });
                _grid.ColumnDefinitions.Add(colDef);
                Grid.SetColumn(contentControl, i * 2);
            }
            else
            {
                var rowDef = new RowDefinition();
                rowDef.Bind(RowDefinition.HeightProperty, new Binding(nameof(DockNode.DockSize)) { Source = childNode, Mode = BindingMode.TwoWay });
                _grid.RowDefinitions.Add(rowDef);
                Grid.SetRow(contentControl, i * 2);
            }

            _grid.Children.Add(contentControl);

            // Add splitter if not last item
            if (i < node.Children.Count - 1)
            {
                var splitter = new GridSplitter
                {
                    Background = new SolidColorBrush(Color.Parse("#333333")),
                    ResizeDirection = node.IsHorizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    ShowsPreview = DockManager.ShowSplitterPreview
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