using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using FModel.Framework;
using FModel.Viewers.Core;
using FModel.Viewers.Curves;

namespace FModel.ViewModels.Viewers;

public class CurveViewerViewModel : ViewerViewModel
{
    private readonly CurveScene _scene;

    public override string Title => "Curve";
    public override IViewerScene Scene => _scene;

    public CurveAsset Asset { get; }
    public IReadOnlyList<CurveChannelItem> Channels { get; }

    /// <summary>Channel toggles only make sense with more than one channel.</summary>
    public bool HasChannelToggles => Channels.Count > 1;

    public string Summary
    {
        get
        {
            var kind = Asset.Kind switch
            {
                CurveAssetKind.Float => "Float curve",
                CurveAssetKind.Vector => "Vector curve",
                CurveAssetKind.LinearColor => "Linear color curve",
                _ => "Curve"
            };
            var keys = Asset.Channels.Sum(c => c.Keys.Count);
            return $"{kind} · {keys} key{(keys == 1 ? "" : "s")}";
        }
    }

    public CurveViewerViewModel(CurveAsset asset)
    {
        Asset = asset;
        _scene = new CurveScene(asset);
        Channels = asset.Channels.Select(c => new CurveChannelItem(c, _scene)).ToList();
    }
}

public class CurveChannelItem : ViewModel
{
    private readonly CurveChannel _channel;
    private readonly CurveScene _scene;

    public string Name => _channel.Name;
    public Brush Brush { get; }
    public string ToolTip => _channel.HasKeys
        ? $"{_channel.Keys.Count} key{(_channel.Keys.Count == 1 ? "" : "s")}, {_channel.FirstKeyTime:0.###} → {_channel.LastKeyTime:0.###}\nPre: {Extrap(_channel.Curve.PreInfinityExtrap)} · Post: {Extrap(_channel.Curve.PostInfinityExtrap)}"
        : "No keys";

    public bool IsVisible
    {
        get => _channel.IsVisible;
        set
        {
            if (_channel.IsVisible == value) return;
            _channel.IsVisible = value;
            RaisePropertyChanged(nameof(IsVisible));
            _scene.NotifyChannelsChanged();
        }
    }

    public CurveChannelItem(CurveChannel channel, CurveScene scene)
    {
        _channel = channel;
        _scene = scene;
        var brush = new SolidColorBrush(Color.FromArgb(channel.Color.Alpha, channel.Color.Red, channel.Color.Green, channel.Color.Blue));
        brush.Freeze();
        Brush = brush;
    }

    private static string Extrap(CUE4Parse.UE4.Objects.Engine.Curves.ERichCurveExtrapolation e) => e.ToString().Replace("RCCE_", "");
}
