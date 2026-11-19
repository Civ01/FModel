using FModel.Framework;
using FModel.Viewers.Core;

namespace FModel.ViewModels.Viewers;

/// <summary>
/// Base for visual viewers shown under (or instead of) a tab's JSON. Holds the camera so it survives
/// moving the viewer to a separate tab.
/// </summary>
public abstract class ViewerViewModel : ViewModel
{
    /// <summary>Short name used as the tab title suffix and in file names, e.g. "Curve".</summary>
    public abstract string Title { get; }

    public abstract IViewerScene Scene { get; }

    public ViewTransform View { get; } = new();

    private TabItem _owner;
    /// <summary>Tab currently showing this viewer.</summary>
    public TabItem Owner
    {
        get => _owner;
        set => SetProperty(ref _owner, value);
    }
}
