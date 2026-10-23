// Shade - shared control: the window's gear.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Transformation;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using static Shade.Look;

namespace Shade
{
    /// <summary>The window's gear: eight teeth and a hole; lit while its page is open.</summary>
    public sealed class GearButton : Control
    {
        bool hot, active;
        public event Action? Click;
        public bool Active { get => active; set { active = value; InvalidateVisual(); } }

        public GearButton()
        {
            Width = Height = 30;
            Cursor = Build.Hand;
            PointerEntered += (s, e) => { hot = true; InvalidateVisual(); };
            PointerExited += (s, e) => { hot = false; InvalidateVisual(); };
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) Click?.Invoke(); };
        }

        public override void Render(DrawingContext g)
        {
            if (hot || active) g.DrawRectangle(B(Raised), null, new Rect(Bounds.Size), 15, 15);
            Color color = active ? Accent : hot ? Look.Text : Text2;
            const int teeth = 8;
            double cx = Bounds.Width / 2, cy = Bounds.Height / 2, outer = 7.5, inner = 5.6, step = 360.0 / teeth;
            var geo = new StreamGeometry();
            using (var c = geo.Open())
            {
                bool first = true;
                for (int i = 0; i < teeth; i++)
                {
                    double a = i * step;
                    foreach (var (rad, deg) in new[] { (inner, a - 0.27 * step), (outer, a - 0.14 * step), (outer, a + 0.14 * step), (inner, a + 0.27 * step) })
                    {
                        var p = new Point(cx + rad * Math.Cos(deg * Math.PI / 180), cy + rad * Math.Sin(deg * Math.PI / 180));
                        if (first) { c.BeginFigure(p, false); first = false; } else c.LineTo(p);
                    }
                }
                c.EndFigure(true);
            }
            var pen = new Pen(B(color), 1.5, lineJoin: PenLineJoin.Round);
            g.DrawGeometry(null, pen, geo);
            g.DrawEllipse(null, pen, new Point(cx, cy), 2.4, 2.4);
        }
    }
}
