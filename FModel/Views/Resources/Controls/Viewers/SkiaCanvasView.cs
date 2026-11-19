using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FModel.Viewers.Core;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace FModel.Views.Resources.Controls.Viewers;

/// <summary>
/// Skia surface hosting an <see cref="IViewerScene"/> with pan/zoom/fit, hover and PNG export.
/// Mouse: wheel zooms (Ctrl = X only, Shift = Y only for non-uniform scenes), middle/right drag pans
/// (left drag too when the scene allows it), double-click or F fits. Right-click without dragging opens the context menu.
/// </summary>
public class SkiaCanvasView : SKElement
{
    private const double WheelZoomStep = 1.15;
    private const double DragThreshold = 3;

    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene), typeof(IViewerScene), typeof(SkiaCanvasView), new PropertyMetadata(null, OnSceneChanged));

    public static readonly DependencyProperty ViewProperty = DependencyProperty.Register(
        nameof(View), typeof(ViewTransform), typeof(SkiaCanvasView), new PropertyMetadata(null, (d, _) => ((SkiaCanvasView) d).InvalidateVisual()));

    public IViewerScene Scene
    {
        get => (IViewerScene) GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>
    /// Camera state. Owned by the view model so it survives moving the viewer to another tab.
    /// </summary>
    public ViewTransform View
    {
        get => (ViewTransform) GetValue(ViewProperty) ?? _fallbackView;
        set => SetValue(ViewProperty, value);
    }

    private readonly ViewTransform _fallbackView = new();
    private bool _fitRequested;
    private MouseButton? _panButton;
    private Point _panLast;
    private Point _pressPoint;
    private bool _dragged;
    private bool _suppressContextMenu;

    public SKSize ViewportSize => new((float) ActualWidth, (float) ActualHeight);

    public SkiaCanvasView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
        SizeChanged += (_, _) => InvalidateVisual();
    }

    public void FitToView()
    {
        _fitRequested = true;
        InvalidateVisual();
    }

    /// <summary>Renders the current scene to PNG bytes at 2× the on-screen resolution.</summary>
    public byte[] ExportPng(SceneImageExporter.Area area)
    {
        if (Scene is null || ActualWidth < 1 || ActualHeight < 1) return null;
        var bytes = SceneImageExporter.RenderPng(Scene, View, ViewportSize, ViewerThemeProvider.FromResources(this), area);
        InvalidateVisual();
        return bytes;
    }

    private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (SkiaCanvasView) d;
        if (e.OldValue is IViewerScene oldScene) oldScene.Changed -= view.OnSceneContentChanged;
        if (e.NewValue is IViewerScene newScene) newScene.Changed += view.OnSceneContentChanged;
        view.InvalidateVisual();
    }

    private void OnSceneContentChanged()
    {
        if (Dispatcher.CheckAccess()) InvalidateVisual();
        else Dispatcher.BeginInvoke(InvalidateVisual);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        var canvas = e.Surface.Canvas;
        var theme = ViewerThemeProvider.FromResources(this);

        if (Scene is null || ActualWidth < 1 || ActualHeight < 1)
        {
            canvas.Clear(theme.Background);
            return;
        }

        // Draw in DIPs; the surface is in device pixels.
        var dpiScale = (float) (e.Info.Width / ActualWidth);
        canvas.Scale(dpiScale);

        var size = ViewportSize;
        var view = View;
        view.FlipY = Scene.FlipY;
        view.Plot = Scene.GetPlotRect(size);

        if ((_fitRequested || !view.HasBeenFitted) && Scene.GetContentBounds() is { } bounds)
        {
            view.Fit(bounds, Scene.UniformScale);
            _fitRequested = false;
        }

        Scene.Render(canvas, size, view, theme);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Scene is null) return;

        var factor = e.Delta > 0 ? WheelZoomStep : 1 / WheelZoomStep;
        var fx = factor;
        var fy = factor;
        if (!Scene.UniformScale)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) fy = 1;
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) fx = 1;
        }

        var p = e.GetPosition(this);
        View.ZoomAt(p.X, p.Y, fx, fy, Scene.MinZoom, Scene.MaxZoom);
        Scene.UpdateHover(ToSk(p), View);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (Scene is null) return;

        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            FitToView();
            e.Handled = true;
            return;
        }

        var canPan = e.ChangedButton is MouseButton.Middle or MouseButton.Right ||
                     (e.ChangedButton == MouseButton.Left && Scene.PanWithLeftButton);
        if (!canPan || _panButton != null) return;

        _panButton = e.ChangedButton;
        _panLast = _pressPoint = e.GetPosition(this);
        _dragged = false;
        CaptureMouse();
        if (e.ChangedButton != MouseButton.Right) e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Scene is null) return;
        var p = e.GetPosition(this);

        if (_panButton != null)
        {
            if (!_dragged && (Math.Abs(p.X - _pressPoint.X) > DragThreshold || Math.Abs(p.Y - _pressPoint.Y) > DragThreshold))
            {
                _dragged = true;
                Cursor = Cursors.SizeAll;
            }

            if (_dragged)
            {
                View.PanByScreen(p.X - _panLast.X, p.Y - _panLast.Y);
                _panLast = p;
                Scene.UpdateHover(ToSk(p), View);
                InvalidateVisual();
            }
            return;
        }

        if (Scene.UpdateHover(ToSk(p), View))
            InvalidateVisual();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_panButton != e.ChangedButton) return;

        // A right-drag pans; only a right-click without movement opens the context menu.
        _suppressContextMenu = e.ChangedButton == MouseButton.Right && _dragged;
        _panButton = null;
        _dragged = false;
        Cursor = null;
        ReleaseMouseCapture();
    }

    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        if (_suppressContextMenu)
        {
            _suppressContextMenu = false;
            e.Handled = true;
            return;
        }
        base.OnContextMenuOpening(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_panButton == null && Scene?.UpdateHover(null, View) == true)
            InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None)
        {
            FitToView();
            e.Handled = true;
        }
    }

    private static SKPoint ToSk(Point p) => new((float) p.X, (float) p.Y);
}

/// <summary>Builds a <see cref="ViewerTheme"/> from the current AdonisUI brushes.</summary>
public static class ViewerThemeProvider
{
    public static ViewerTheme FromResources(FrameworkElement element)
    {
        var dark = ViewerTheme.Dark;
        return dark with
        {
            Background = Color(element, AdonisUI.Brushes.Layer0BackgroundBrush, dark.Background),
            PlotBackground = Color(element, AdonisUI.Brushes.Layer1BackgroundBrush, dark.PlotBackground),
            Border = Color(element, AdonisUI.Brushes.Layer1BorderBrush, dark.Border),
            Text = Color(element, AdonisUI.Brushes.ForegroundBrush, dark.Text),
            TextDim = Color(element, AdonisUI.Brushes.DisabledForegroundBrush, dark.TextDim),
            CardBackground = Color(element, AdonisUI.Brushes.Layer2BackgroundBrush, dark.CardBackground).WithAlpha(0xF2),
            CardBorder = Color(element, AdonisUI.Brushes.Layer2BorderBrush, dark.CardBorder),
            Accent = Color(element, AdonisUI.Brushes.AccentBrush, dark.Accent),
            FontFamily = element is Control c ? c.FontFamily.Source : SystemFonts.MessageFontFamily.Source,
        };
    }

    private static SKColor Color(FrameworkElement element, object key, SKColor fallback) =>
        element.TryFindResource(key) is SolidColorBrush brush ? brush.Color.ToSKColor() : fallback;
}
