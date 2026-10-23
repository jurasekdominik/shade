// Shade - shared control: a square tick box.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using static Shade.Look;

namespace Shade
{
    /// <summary>A square tick box: an outline = off, filled with the accent = on, a smaller filled square = some (State null).
    /// A click toggles it (rows that toggle it themselves turn its hit test off).</summary>
    public sealed class Tick : Control
    {
        bool? state = false;
        public static readonly DirectProperty<Tick, bool?> StateProperty =
            AvaloniaProperty.RegisterDirect<Tick, bool?>(nameof(State), o => o.State, (o, v) => o.State = v, false, Avalonia.Data.BindingMode.TwoWay);
        bool hot;
        public event Action<bool>? Toggled;

        public Tick()
        {
            Width = Height = 16; Cursor = Build.Hand;
            PointerEntered += (s, e) => { hot = true; InvalidateVisual(); };
            PointerExited += (s, e) => { hot = false; InvalidateVisual(); };
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) { e.Handled = true; Toggle(); } };
        }

        /// <summary>Set by the code (no Toggled); null = some.</summary>
        public bool? State { get => state; set { SetAndRaise(StateProperty, ref state, value); InvalidateVisual(); } }
        public bool On => state == true;

        /// <summary>A click: some or none → all on, all → off.</summary>
        public void Toggle() { if (!IsEnabled) return; State = state != true; Toggled?.Invoke(state == true); }

        public override void Render(DrawingContext g)
        {
            var r = new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1);
            bool lit = state != false;
            var fill = hot ? AccentHi : Accent;
            if (!lit) { g.DrawRectangle(B(Raised), new Pen(B(hot ? Text2 : Disabled), 1), r, 4, 4); return; }
            g.DrawRectangle(B(fill), null, r, 4, 4);
            var pen = new Pen(B(OnAccent), 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            double w = Bounds.Width, h = Bounds.Height;
            if (state == true) g.DrawGeometry(null, pen, new PolylineGeometry(new[] { new Point(w * .26, h * .52), new Point(w * .43, h * .68), new Point(w * .74, h * .34) }, false));
            else g.DrawLine(pen, new Point(w * .28, h / 2), new Point(w * .72, h / 2));  // some: a dash
        }
    }
}
