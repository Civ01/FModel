using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace FModel.Views.Resources.Controls.Viewers;

/// <summary>
/// Hosts the tab's document (its first child) and an optional viewer below it:
/// no viewer = document only; viewer = document on top, splitter, viewer below; viewer-only = viewer fills the panel.
/// The viewer height is exposed for persisting in user settings.
/// </summary>
public class ViewerSplitPanel : Grid
{
    private const double SplitterHeight = 5;
    private const double MinViewerHeight = 120;
    private const double MinDocumentHeight = 60;

    public static readonly DependencyProperty ViewerProperty = DependencyProperty.Register(
        nameof(Viewer), typeof(object), typeof(ViewerSplitPanel), new PropertyMetadata(null, OnStateChanged));

    public static readonly DependencyProperty IsViewerOnlyProperty = DependencyProperty.Register(
        nameof(IsViewerOnly), typeof(bool), typeof(ViewerSplitPanel), new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty ViewerHeightProperty = DependencyProperty.Register(
        nameof(ViewerHeight), typeof(double), typeof(ViewerSplitPanel),
        new FrameworkPropertyMetadata(320d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStateChanged));

    public object Viewer
    {
        get => GetValue(ViewerProperty);
        set => SetValue(ViewerProperty, value);
    }

    public bool IsViewerOnly
    {
        get => (bool) GetValue(IsViewerOnlyProperty);
        set => SetValue(IsViewerOnlyProperty, value);
    }

    public double ViewerHeight
    {
        get => (double) GetValue(ViewerHeightProperty);
        set => SetValue(ViewerHeightProperty, value);
    }

    private readonly RowDefinition _documentRow = new();
    private readonly RowDefinition _splitterRow = new();
    private readonly RowDefinition _viewerRow = new();
    private readonly GridSplitter _splitter;
    private readonly ContentPresenter _presenter;
    private bool _updating;

    public ViewerSplitPanel()
    {
        RowDefinitions.Add(_documentRow);
        RowDefinitions.Add(_splitterRow);
        RowDefinitions.Add(_viewerRow);

        _splitter = new GridSplitter
        {
            Height = SplitterHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            // KeyboardIncrement must be > 0 (GridSplitter.IsValidDelta); disable keyboard resizing by not taking focus instead.
            Focusable = false,
            ShowsPreview = false
        };
        _splitter.SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer3BackgroundBrush);
        _splitter.DragCompleted += OnSplitterDragCompleted;
        SetRow(_splitter, 1);

        _presenter = new ContentPresenter();
        _presenter.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(Viewer)) { Source = this });
        SetRow(_presenter, 2);
    }

    protected override void OnInitialized(System.EventArgs e)
    {
        base.OnInitialized(e);
        // Children declared in XAML (the document editor) go in the top row.
        foreach (UIElement child in Children)
            SetRow(child, 0);

        Children.Add(_splitter);
        Children.Add(_presenter);
        UpdateState();
    }

    private UIElement Document => Children.Count > 0 && Children[0] != _splitter && Children[0] != _presenter ? Children[0] : null;

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ViewerSplitPanel) d).UpdateState();

    private void UpdateState()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var hasViewer = Viewer != null;
            var document = Document;

            if (!hasViewer)
            {
                SetRows(new GridLength(1, GridUnitType.Star), 0, new GridLength(0), 0, MinDocumentHeight);
                SetVisibility(document, true);
                _splitter.Visibility = Visibility.Collapsed;
                _presenter.Visibility = Visibility.Collapsed;
            }
            else if (IsViewerOnly)
            {
                SetRows(new GridLength(0), 0, new GridLength(1, GridUnitType.Star), 0, 0);
                SetVisibility(document, false);
                _splitter.Visibility = Visibility.Collapsed;
                _presenter.Visibility = Visibility.Visible;
            }
            else
            {
                SetRows(new GridLength(1, GridUnitType.Star), SplitterHeight, new GridLength(System.Math.Max(MinViewerHeight, ViewerHeight)), MinViewerHeight, MinDocumentHeight);
                SetVisibility(document, true);
                _splitter.Visibility = Visibility.Visible;
                _presenter.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void SetRows(GridLength document, double splitter, GridLength viewer, double viewerMin, double documentMin)
    {
        _documentRow.Height = document;
        _documentRow.MinHeight = documentMin;
        _splitterRow.Height = new GridLength(splitter);
        _viewerRow.Height = viewer;
        _viewerRow.MinHeight = viewerMin;
    }

    private static void SetVisibility(UIElement element, bool visible)
    {
        if (element != null) element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        // The splitter turns the rows into pixel/star sizes; keep the viewer as a fixed pixel height and persist it.
        var height = _viewerRow.ActualHeight;
        ViewerHeight = height;
        UpdateState(); // re-applies star/pixel sizing even when the height didn't change
    }
}
