// Baseline - shared control: a square tick box.
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
using static Baseline.Look;

namespace Baseline
{
    /// <summary>A square tick box: an outline = off, filled with the accent = on, a smaller filled square = some (State null).
    /// A click toggles it (rows that toggle it themselves turn its hit test off).</summary>
    public sealed class Tick : Control
    {
        bool? state = false;
        bool hot;
        public event Action<bool> Toggled;

        public Tick()
        {
            Width = Height = 16; Cursor = Kit.Hand;
            PointerEntered += (s, e) => { hot = true; InvalidateVisual(); };
            PointerExited += (s, e) => { hot = false; InvalidateVisual(); };
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) { e.Handled = true; Toggle(); } };
        }

        /// <summary>Set by the code (no Toggled); null = some.</summary>
        public bool? State { get => state; set { state = value; InvalidateVisual(); } }
        public bool On => state == true;

        /// <summary>A click: some or none → all on, all → off.</summary>
        public void Toggle() { if (!IsEnabled) return; state = state != true; InvalidateVisual(); Toggled?.Invoke(state == true); }

        public override void Render(DrawingContext g)
        {
            var r = new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1);
            bool lit = state != false;
            var fill = hot ? AccentHi : Accent;
            if (!lit) { g.DrawRectangle(B(Surface), new Pen(B(hot ? Second : Disabled), 1), r, 4, 4); return; }
            g.DrawRectangle(B(fill), null, r, 4, 4);
            var pen = new Pen(B(OnAccent), 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            double w = Bounds.Width, h = Bounds.Height;
            if (state == true) g.DrawGeometry(null, pen, new PolylineGeometry(new[] { new Point(w * .26, h * .52), new Point(w * .43, h * .68), new Point(w * .74, h * .34) }, false));
            else g.DrawLine(pen, new Point(w * .28, h / 2), new Point(w * .72, h / 2));  // some: a dash
        }
    }
}
