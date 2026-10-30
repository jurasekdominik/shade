using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Shade;

public partial class PaperFormView
{
    readonly TransientScaleHost zoomHost;
    double layoutZoom = 1;
    internal bool DeferZoomLayout;
    internal int ZoomLayoutCommitCount { get; private set; }
    internal void CommitZoomLayout()
    {
        if (layoutZoom != Zoom)
        {
            layoutZoom = Zoom; scaled.LayoutTransform = new ScaleTransform(Zoom, Zoom); ZoomLayoutCommitCount++;
        }
        zoomHost.SetRatio(1);
    }
    sealed class TransientScaleHost : Decorator
    {
        readonly Control content;
        internal TransientScaleHost(Control content) { this.content = content; Child = content; }
        readonly ScaleTransform scale = new(1, 1);
        double ratio = 1;
        internal void SetRatio(double value)
        {
            if (ratio == value) return;
            ratio = value; scale.ScaleX = scale.ScaleY = value; content.RenderTransform = scale;
            InvalidateMeasure();
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            content.Measure(Size.Infinity);
            return content.DesiredSize * ratio;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            content.Arrange(new Rect(content.DesiredSize));
            return finalSize;
        }
    }
}
