// Shade - shared control: just a glyph in the accent, brighter on hover, faint when there's nothing to do (Dimmed).
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
    /// <summary>Just a glyph in the accent, brighter on hover, faint when there's nothing to do (Dimmed): the table, undo and
    /// export icons.</summary>
    public sealed class IconButton : Border, IRestyle
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new IconPeer(this);
        internal void Invoke() { if (IsEffectivelyEnabled) Click?.Invoke(); }
        readonly Avalonia.Controls.Shapes.Path path;
        bool hot, dimmed;
        public event Action? Click;

        public IconButton(string data, double size = 18)
        {
            path = data == Undo || data == Export ? Build.Icon24(Geometry.Parse(data), size, data == Undo ? 1.8 : 1.7)  // on their 24 grid
                : new Avalonia.Controls.Shapes.Path
                {
                    Data = Geometry.Parse(data),
                    StrokeThickness = 1.7,
                    StrokeLineCap = PenLineCap.Round,
                    StrokeJoin = PenLineJoin.Round,
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            Child = path;
            Background = Brushes.Transparent;
            Cursor = Build.Hand; Activate(this, Invoke);
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) => { hot = false; Restyle(); };
            PropertyChanged += (s, e) => { if (e.Property == IsEnabledProperty || e.Property == IsEffectivelyEnabledProperty) Restyle(); };
            Restyle();
        }

        public bool Dimmed { get => dimmed; set { dimmed = value; Restyle(); } }
        public void Restyle() => path.Stroke = B(dimmed || !IsEffectivelyEnabled ? Disabled : hot ? AccentHi : Accent);

        public const string Table = "M3,6 L3.01,6 M8,6 L21,6 M3,12 L3.01,12 M8,12 L21,12 M3,18 L3.01,18 M8,18 L21,18";
        public const string Undo = "M9,14 L5,10 L9,6 M5,10 L16,10 A4,4 0 0 1 16,18 L15,18";
        public const string Export = "M14,3 L7,3 L5,5 L5,19 L7,21 L17,21 L19,19 L19,8 L14,3 L14,8 L19,8 M12,10.5 L12,17 M9.2,14.4 L12,17.2 L14.8,14.4";
    }
}
