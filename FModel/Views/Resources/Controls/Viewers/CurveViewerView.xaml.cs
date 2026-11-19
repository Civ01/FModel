using System.Windows;
using System.Windows.Controls;
using FModel.ViewModels.Viewers;
using FModel.Viewers.Core;

namespace FModel.Views.Resources.Controls.Viewers;

public partial class CurveViewerView : UserControl
{
    public CurveViewerView()
    {
        InitializeComponent();
    }

    private void OnFitClick(object sender, RoutedEventArgs e) => CurveCanvas.FitToView();

    private void OnExportViewportClick(object sender, RoutedEventArgs e) => Export(SceneImageExporter.Area.Viewport);

    private void OnExportContentClick(object sender, RoutedEventArgs e) => Export(SceneImageExporter.Area.Content);

    private void Export(SceneImageExporter.Area area)
    {
        if (DataContext is not ViewerViewModel viewer || viewer.Owner is null) return;
        var png = CurveCanvas.ExportPng(area);
        if (png is null) return;
        viewer.Owner.SaveViewerImage(png, viewer.Title, true);
    }
}
