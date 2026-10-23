// Shade - shared control: an on/off register state as READ shows it, and the toggle that changes it.
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
    /// <summary>An on/off register's state as READ shows it: a block with ON (accent) or OFF (red) in a track tinted the same
    /// way (display only: READ shows the state, WRITE changes it).</summary>
    public sealed class StateLook : Control
    {
        internal const double Radius = 4;
        bool on;
        public bool On { get => on; set { if (on != value) { on = value; InvalidateVisual(); } } }

        public override void Render(DrawingContext g)
        {
            var box = new Rect(Bounds.Size).Deflate(0.5);
            Color state = on ? Accent : Danger;
            g.DrawRectangle(B(Blend(Surface, state, 0.16)), new Pen(B(Blend(Look.Border, state, 0.45)), 1), box, StateLook.Radius, StateLook.Radius);
            double inset = 3, w = (box.Width - 2 * inset) * 0.52, h = box.Height - 2 * inset;
            var block = new Rect(box.X + inset + (on ? box.Width - 2 * inset - w : 0), box.Y + inset, w, h);
            g.DrawRectangle(B(state), null, block, StateLook.Radius - inset, StateLook.Radius - inset);
            var t = new FormattedText(on ? "ON" : "OFF", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(UiFont, FontStyle.Normal, FontWeight.Bold), Pt(9), B(on ? OnAccent : White));
            g.DrawText(t, new Point(block.X + (block.Width - t.Width) / 2, block.Y + (block.Height - t.Height) / 2));
        }
    }

    /// <summary>An on/off register in WRITE, as wide as a field: a block with the state slides left (OFF, red) or right (ON,
    /// accent). Unknown = not read yet (grey). Pending = a yellow border: the state shown is not written yet. ShowUndo: the
    /// undo arrow on the free side.</summary>
    public sealed class StateToggle : Control
    {
        bool on, unknown = true, pending, showUndo, hot;
        public event Action<bool>? Toggled;
        public Action? Undo { get; set; }
        public static readonly DirectProperty<StateToggle, bool> OnProperty =
            AvaloniaProperty.RegisterDirect<StateToggle, bool>(nameof(On), o => o.On, (o, v) => o.On = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public static readonly DirectProperty<StateToggle, bool> UnknownProperty =
            AvaloniaProperty.RegisterDirect<StateToggle, bool>(nameof(Unknown), o => o.Unknown, (o, v) => o.Unknown = v, true);
        public static readonly DirectProperty<StateToggle, bool> PendingProperty =
            AvaloniaProperty.RegisterDirect<StateToggle, bool>(nameof(Pending), o => o.Pending, (o, v) => o.Pending = v);

        public StateToggle()
        {
            Cursor = Kit.Hand;
            PointerEntered += (s, e) => { hot = true; InvalidateVisual(); };
            PointerExited += (s, e) => { hot = false; InvalidateVisual(); };
            PointerReleased += (s, e) =>
            {
                if (e.InitialPressMouseButton != MouseButton.Left || !IsEnabled) return;
                if (showUndo && UndoSpot.Contains(e.GetPosition(this))) Undo?.Invoke(); else On = !On;
            };
            PropertyChanged += (s, e) => { if (e.Property == IsEnabledProperty) InvalidateVisual(); };
        }

        public bool Unknown { get => unknown; set { if (SetAndRaise(UnknownProperty, ref unknown, value)) InvalidateVisual(); } }
        public bool Pending { get => pending; set { if (SetAndRaise(PendingProperty, ref pending, value)) InvalidateVisual(); } }
        public bool ShowUndo { get => showUndo; set { if (showUndo != value) { showUndo = value; InvalidateVisual(); } } }

        public bool On
        {
            get => on;
            set
            {
                bool was = unknown;
                Unknown = false;
                if (value == on) { if (was) InvalidateVisual(); return; }
                SetAndRaise(OnProperty, ref on, value);
                InvalidateVisual();
                Toggled?.Invoke(on);
            }
        }

        /// <summary>The undo icon's spot: at the outer end of the free side, away from the block (ON: left, OFF: right).</summary>
        Rect UndoSpot => on ? new Rect(6, 0, 24, Bounds.Height) : new Rect(Bounds.Width - 30, 0, 24, Bounds.Height);

        public override void Render(DrawingContext g)
        {
            var box = new Rect(Bounds.Size).Deflate(0.5);
            if (unknown)
            {
                g.DrawRectangle(B(Surface), new Pen(B(pending ? Warn : Look.Border), 1), box, StateLook.Radius, StateLook.Radius);
                Look.Draw(g, "unknown", Look.Ui(8.5), Faint, new Rect(Bounds.Size), TextAlignment.Center, false);
                return;
            }
            Color state = on ? Accent : Danger;
            g.DrawRectangle(B(Blend(Surface, state, 0.16)), new Pen(B(pending ? Warn : Blend(Look.Border, state, 0.45)), 1), box, StateLook.Radius, StateLook.Radius);
            double inset = 3, w = (box.Width - 2 * inset) * 0.52, h = box.Height - 2 * inset;
            var block = new Rect(box.X + inset + (on ? box.Width - 2 * inset - w : 0), box.Y + inset, w, h);
            g.DrawRectangle(B(IsEnabled ? state : Alpha(state, 0.4)), null, block, StateLook.Radius - inset, StateLook.Radius - inset);
            Look.Draw(g, on ? "ON" : "OFF", Look.Ui(9, 700), on ? OnAccent : White, block, TextAlignment.Center, false);
            if (showUndo)
            {
                var spot = UndoSpot;
                using (g.PushTransform(Matrix.CreateTranslation(spot.X + (spot.Width - 16) / 2 - 2, (Bounds.Height - 16) / 2 - 1)))
                using (g.PushTransform(Matrix.CreateScale(16 / 24.0, 16 / 24.0)))
                    g.DrawGeometry(null, new Pen(B(hot ? AccentHi : Accent), 1.8 * 24 / 16, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Field.UndoGeometry);
            }
        }
    }
}
