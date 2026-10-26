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
    public sealed class Tick : CheckBox
    {
        protected override Type StyleKeyOverride => typeof(Tick);
        public Tick() { Width = Height = M(16); Cursor = Build.Hand; FocusRing(this); }
        protected override void Toggle()
        {
            if (IsChecked == null && !IsThreeState) SetCurrentValue(IsCheckedProperty, true);
            else base.Toggle();
        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsCheckedProperty || change.Property == IsPointerOverProperty || change.Property == IsEffectivelyEnabledProperty) InvalidateVisual();
        }

        public override void Render(DrawingContext g)
        {
            using var opacity = g.PushOpacity(IsEffectivelyEnabled ? 1 : 0.4);
            var r = new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1);
            bool lit = IsChecked != false;
            var fill = IsPointerOver ? AccentHi : Accent;
            if (!lit) { g.DrawRectangle(B(Raised), new Pen(B(IsPointerOver ? Text2 : Disabled), 1), r, 4, 4); return; }
            g.DrawRectangle(B(fill), null, r, 4, 4);
            var pen = new Pen(B(OnAccent), 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            double w = Bounds.Width, h = Bounds.Height;
            if (IsChecked == true) g.DrawGeometry(null, pen, new PolylineGeometry(new[] { new Point(w * .26, h * .52), new Point(w * .43, h * .68), new Point(w * .74, h * .34) }, false));
            else g.DrawLine(pen, new Point(w * .28, h / 2), new Point(w * .72, h / 2));  // some: a dash
        }
    }
}
