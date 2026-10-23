// Baseline - the look: the colours and their accent shades, the fonts and the node mark.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Layout;

namespace Baseline
{
    public static class Look
    {
        public static Color Hex(string h) => Color.Parse(h);
        public static string HexOf(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        public static Color Alpha(Color c, double a) => Color.FromArgb((byte)Math.Round(255 * Math.Max(0, Math.Min(1, a))), c.R, c.G, c.B);
        public static Color Blend(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        public static IBrush B(Color c) => new SolidColorBrush(c);

        // Shared neutral tokens stay fixed; accent shades follow the user's choice (SetAccent).
        public static readonly Color Bg = Hex("#151519"), Text = Hex("#EDEDF0"), GithubGreen = Hex("#0FBF3E"), White = Colors.White;
        public static readonly Color Surface = Hex("#1B1B20"), Surface2 = Hex("#29292F"),
            Border = Hex("#35353D"), Faint = Hex("#9999A7"), Disabled = Blend(Hex("#151519"), Hex("#9999A7"), 0.5),
            Second = Hex("#B1B1BC"), Success = Hex("#83CEA5");
        /// <summary>A field's or dropdown's border on hover and while its menu is open (menus themselves use Border).</summary>
        public static readonly Color MenuEdge = Blend(Border, Second, 0.35);
        public const string DefaultAccent = "#8C7AE6";
        /// <summary>The app's name: dialog titles and the default window title.</summary>
        public static string AppName = "App";
        public static Color Accent, AccentText, OnAccent, AccentHi, AccentLo, AccentLine, Soft, SoftHi, SoftLine, CurrentRow,
            Danger, DangerHi, DangerMuted, Override, Warn, Up;
        static Look() { SetAccent(Hex(DefaultAccent)); }

        /// <summary>The colour roles' rules (DESIGN_SYSTEM.md §2): a 15 % tint over Raised, a hover 10 % lighter, text on a solid colour.</summary>
        public static Color Tint(Color c, double amount = 0.15) => Blend(Surface, c, amount);
        public static Color Hover(Color c) => Blend(c, White, 0.10);

        /// <summary>The accent and everything made from it by the rules; the status colours are fixed (Danger, Warn, Info = Override,
        /// Success; Up is Success).</summary>
        public static void SetAccent(Color a)
        {
            Accent = Color.FromRgb(a.R, a.G, a.B);
            AccentText = Accent;
            for (int i = 0; i < 20 && Contrast(AccentText, Surface) < 4.5; i++) AccentText = Blend(AccentText, White, 0.08);
            OnAccent = OnColor(Accent);
            AccentHi = Hover(Accent); AccentLo = Blend(Accent, Bg, 0.10); AccentLine = Blend(Bg, Accent, 0.50);
            Soft = Tint(Accent); SoftHi = Tint(Accent, 0.25); SoftLine = Blend(Border, Accent, 0.60);
            CurrentRow = Tint(Accent, 0.06);
            Danger = Hex("#E0685C"); DangerHi = Hover(Danger); DangerMuted = Tint(Danger);
            Warn = Hex("#FFB142");
            Override = Hex("#6FA8DC");
            Up = Success;
            icons.Clear();
        }

        /// <summary>The status colour an accent is too close to (hue within 20°, both saturated), or null.</summary>
        public static string NearStatus(Color c)
        {
            var (hue, sat) = HueSat(c);
            if (sat < 0.3) return null;
            foreach (var (name, s) in new[] { ("Success", Success), ("Warning", Hex("#FFB142")), ("Danger", Hex("#E0685C")), ("Info", Hex("#6FA8DC")) })
            {
                var (h2, _) = HueSat(s);
                double d = Math.Abs(hue - h2); if (Math.Min(d, 360 - d) < 20) return name;
            }
            return null;
        }

        /// <summary>Every accent-made shade, in a fixed order: what the window was painted with, to swap for the new ones.</summary>
        public static Color[] Shades() => new[] { Accent, AccentHi, AccentLo, AccentLine, Soft, SoftHi, SoftLine, CurrentRow, OnAccent, Danger, DangerHi, DangerMuted, Override, Warn, Up, AccentText };

        /// <summary>A new accent everywhere: what paints at render time follows by itself; colours a control
        /// stored in a brush (texts, fills, borders, runs, a button's tint) are swapped for the same shade of the new accent.
        /// from: Shades() when the windows were painted.</summary>
        public static void Recolor(IEnumerable<Window> windows, Color[] from)
        {
            var to = Shades();
            var map = new Dictionary<uint, Color>();
            for (int i = 0; i < from.Length; i++) { uint k = Rgb(from[i]); if (!map.ContainsKey(k)) map[k] = to[i]; }
            Color Remap(Color c) => map.TryGetValue(Rgb(c), out var n) ? Color.FromArgb(c.A, n.R, n.G, n.B) : c;
            IBrush Brush(IBrush b) => b is ISolidColorBrush sb && map.ContainsKey(Rgb(sb.Color)) ? new SolidColorBrush(Remap(sb.Color), sb.Opacity) : b;
            var props = new AvaloniaProperty<IBrush>[]
            {
                TemplatedControl.ForegroundProperty, TemplatedControl.BackgroundProperty, TemplatedControl.BorderBrushProperty,
                Avalonia.Controls.Border.BackgroundProperty, Avalonia.Controls.Border.BorderBrushProperty, Panel.BackgroundProperty,
                TextBlock.ForegroundProperty, TextBlock.BackgroundProperty, Avalonia.Controls.Shapes.Shape.FillProperty, Avalonia.Controls.Shapes.Shape.StrokeProperty,
                TextBox.CaretBrushProperty, TextBox.SelectionBrushProperty, TextBox.SelectionForegroundBrushProperty,
            };
            void Walk(Visual l)
            {
                if (l is AvaloniaObject o)
                    foreach (var p in props)
                        if (o.IsSet(p) && o.GetValue(p) is IBrush b) { var n = Brush(b); if (n != b) o.SetValue(p, n); }
                if (l is TextBlock t && t.Inlines != null)
                    foreach (var r in t.Inlines) if (r.Foreground is IBrush rb) r.Foreground = Brush(rb);
                if (l is Btn btn && btn.Live is Color lc) btn.Live = Remap(lc);
                // controls that build their fills from accent blends (buttons, capsules, chips, tabs, fields) paint them again
                l.GetType().GetMethod("Restyle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null)?.Invoke(l, null);
                l.InvalidateVisual();
                foreach (var k in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(l)) Walk(k);  // visual: rows draw child switches outside the logical tree
            }
            foreach (var w in windows) { w.Icon = Icon(Accent); Walk(w); }
        }

        static uint Rgb(Color c) => (uint)(c.R << 16 | c.G << 8 | c.B);

        /// <summary>HSL hue (0-360) and saturation, as System.Drawing.Color.GetHue / GetSaturation.</summary>
        static (double hue, double sat) HueSat(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            if (d == 0) return (0, 0);
            double l = (max + min) / 2, sat = l <= 0.5 ? d / (max + min) : d / (2 - max - min);
            double hue = r == max ? (g - b) / d : g == max ? 2 + (b - r) / d : 4 + (r - g) / d;
            hue *= 60;
            return (hue < 0 ? hue + 360 : hue, sat);
        }

        /// <summary>Text colour on a fill of c: dark or white, whichever reads better.</summary>
        public static Color OnColor(Color c) => Contrast(c, Bg) >= Contrast(c, White) ? Bg : White;

        /// <summary>WCAG contrast ratio of two colours (1 to 21).</summary>
        public static double Contrast(Color a, Color b)
        {
            double L(Color c)
            {
                double Ch(int v) { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
                return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
            }
            double p = L(a), q = L(b);
            return (Math.Max(p, q) + 0.05) / (Math.Min(p, q) + 0.05);
        }

        /// <summary>The app-wide styles on top of Fluent's dark theme.</summary>
        public static void Apply(Application app)
        {
            app.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
            app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            // the fields draw their own frame: the TextBoxes inside them show none, in any state
            foreach (var state in new[] { null, ":pointerover", ":focus", ":disabled" })
            {
                var s = new Avalonia.Styling.Style(x => { var t = x.OfType<TextBox>().Class("plain"); return (state == null ? t : t.Class(state)).Template().OfType<Border>().Name("PART_BorderElement"); });
                s.Setters.Add(new Avalonia.Styling.Setter(Avalonia.Controls.Border.BackgroundProperty, Brushes.Transparent));
                s.Setters.Add(new Avalonia.Styling.Setter(Avalonia.Controls.Border.BorderThicknessProperty, new Thickness(0)));
                app.Styles.Add(s);
            }
            // a framed text box keeps its own fill when hovered or typed in (Fluent turns it black on focus)
            foreach (var state in new[] { ":pointerover", ":focus" })
            {
                var s = new Avalonia.Styling.Style(x => x.OfType<TextBox>().Not(y => y.Class("plain")).Class(state).Template().OfType<Border>().Name("PART_BorderElement"));
                s.Setters.Add(new Avalonia.Styling.Setter(Avalonia.Controls.Border.BackgroundProperty, new Avalonia.Data.TemplateBinding(TemplatedControl.BackgroundProperty)));
                app.Styles.Add(s);
            }
            // tooltips: Raised fill, the separator line, body text (Fluent's grey otherwise)
            var tip = new Avalonia.Styling.Style(x => x.OfType<ToolTip>());
            foreach (var (p, v) in new (AvaloniaProperty, object)[] { (TemplatedControl.BackgroundProperty, B(Surface2)), (TemplatedControl.ForegroundProperty, B(Text)),
                         (TemplatedControl.BorderBrushProperty, B(MenuEdge)), (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                         (TemplatedControl.CornerRadiusProperty, new CornerRadius(4)), (TemplatedControl.PaddingProperty, new Thickness(8, 5)), (TemplatedControl.FontSizeProperty, Label) })
                tip.Setters.Add(new Avalonia.Styling.Setter(p, v));
            app.Styles.Add(tip);
            app.Resources[typeof(ScrollBar)] = ThinScrollBar();
        }

        /// <summary>Every scroll bar: a thin rounded thumb on no track, 8 px, always that size (Fluent's widens over the text on
        /// hover). Lists keep their content 14 px clear of it.</summary>
        static ControlTheme ThinScrollBar()
        {
            var theme = new ControlTheme(typeof(ScrollBar));
            theme.Setters.Add(new Setter(TemplatedControl.TemplateProperty, new Avalonia.Controls.Templates.FuncControlTemplate<ScrollBar>((sb, ns) =>
            {
                var face = new Border { CornerRadius = new CornerRadius(4), Background = B(Disabled) };
                var thumb = new Thumb { Template = new Avalonia.Controls.Templates.FuncControlTemplate<Thumb>((t, n) => face) };
                thumb.PointerEntered += (o, e) => face.Background = B(Faint);
                thumb.PointerExited += (o, e) => face.Background = B(Disabled);
                var track = new Track { Name = "PART_Track", Thumb = thumb, Margin = new Thickness(1), IsDirectionReversed = sb.Orientation == Avalonia.Layout.Orientation.Vertical };  // top = 0, as Fluent's
                ns.Register("PART_Track", track);
                track[!Track.MinimumProperty] = sb[!RangeBase.MinimumProperty];
                track[!Track.MaximumProperty] = sb[!RangeBase.MaximumProperty];
                // one way down, and a drag goes back as a current value: a local value on the bar would cut its ScrollViewer's binding,
                // and from then on the wheel moved the content but not the thumb
                track[!Track.ValueProperty] = sb[!RangeBase.ValueProperty];
                track.PropertyChanged += (o, e) => { if (e.Property == Track.ValueProperty && !Equals(sb.Value, track.Value)) sb.SetCurrentValue(RangeBase.ValueProperty, track.Value); };
                track[!Track.ViewportSizeProperty] = sb[!ScrollBar.ViewportSizeProperty];
                track[!Track.OrientationProperty] = sb[!ScrollBar.OrientationProperty];
                return new Border { Background = Brushes.Transparent, Child = track };
            })));
            var vertical = new Style(x => x.Nesting().Class(":vertical"));
            vertical.Setters.Add(new Setter(Layoutable.WidthProperty, 8.0));
            var horizontal = new Style(x => x.Nesting().Class(":horizontal"));
            horizontal.Setters.Add(new Setter(Layoutable.HeightProperty, 8.0));
            theme.Children.Add(vertical); theme.Children.Add(horizontal);
            return theme;
        }

        // ---- fonts -----------------------------------------------------------------------------
        public static readonly FontFamily UiFont = new FontFamily("avares://Baseline/Assets/Inter#Inter"),
            Mono = new FontFamily("avares://Baseline/Assets#Cascadia Code");
        /// <summary>Cascadia Code without its ligatures ("->", "==" drawn as one sign).</summary>
        public static readonly FontFeatureCollection NoLigatures = new FontFeatureCollection { FontFeature.Parse("-calt"), FontFeature.Parse("-liga") };
        /// <summary>UI text is sized in points: the same size in Avalonia's 96-dpi pixels.</summary>
        public static double Pt(double pt) => pt * 96 / 72;

        /// <summary>The type scale in px (docs/DESIGN_SYSTEM.md §3).</summary>
        public const double Caption = 11, Label = 12, Body = 13, Section = 14, Title = 18;
        static readonly double[] Scale = { Caption, Label, Body, Section, Title };
        /// <summary>A size given in points, on the scale: the nearest step (the larger on a tie); display sizes over 20 px stay.</summary>
        public static double Size(double pt)
        {
            double px = Pt(pt);
            return px > 20.5 ? px : Scale.OrderBy(s => Math.Abs(s - px)).ThenByDescending(s => s).First();
        }
        public static FontWeight Weight(int w) => w >= 700 ? FontWeight.Bold : w >= 600 ? FontWeight.SemiBold : w >= 500 ? FontWeight.Medium : FontWeight.Normal;

        // ---- painting (the drawn rows: the site table) ---------------------------
        public readonly struct Font
        {
            public readonly Typeface Face; public readonly double Size;
            public Font(Typeface f, double size) { Face = f; Size = size; }
        }

        public static Font Ui(double pt, int weight = 400) => new Font(new Typeface(UiFont, FontStyle.Normal, Weight(weight)), Size(pt));
        public static Font MonoF(double px, bool bold = false) => new Font(new Typeface(Mono, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), px);

        static Avalonia.Media.TextFormatting.TextLayout Layout(string s, Font f, Color c, double width, TextAlignment align, bool ellipsis) =>
            new Avalonia.Media.TextFormatting.TextLayout(s ?? "", f.Face, f.Size, B(c), align, TextWrapping.NoWrap,
                ellipsis ? TextTrimming.CharacterEllipsis : TextTrimming.None, maxWidth: Math.Max(0, width));

        /// <summary>One line of text in r, centred vertically; "..." when it doesn't fit (ellipsis). Returns its width.</summary>
        public static double Draw(DrawingContext g, string s, Font f, Color c, Rect r, TextAlignment align = TextAlignment.Left, bool ellipsis = true)
        {
            using (var t = Layout(s, f, c, r.Width, align, ellipsis))
            {
                using (g.PushClip(new Rect(r.X, r.Y - 4, Math.Max(0, r.Width) + 2, r.Height + 8))) t.Draw(g, new Point(r.X, r.Y + (r.Height - t.Height) / 2));
                return t.WidthIncludingTrailingWhitespace;
            }
        }

        public static double Measure(string s, Font f)
        {
            using (var t = Layout(s, f, Text, double.PositiveInfinity, TextAlignment.Left, false)) return t.WidthIncludingTrailingWhitespace;
        }

        /// <summary>A status dot; live (working, or holding a connection): a ring grows out of it and fades, every 1.6 s.</summary>
        public static void Dot(DrawingContext g, Point c, double r, Color col, bool live)
        {
            if (live)
            {
                double t = Environment.TickCount64 % 1600 / 1600.0, e = 1 - (1 - t) * (1 - t);  // eased out: quick, then lingering
                g.DrawEllipse(B(Alpha(col, 0.45 * (1 - t))), null, c, r + 5 * e, r + 5 * e);
            }
            g.DrawEllipse(B(col), null, c, r, r);
        }

        /// <summary>How well something went, 0..1: green when all of it, through yellow (about 75 %) to red (half or less).
        /// A fixed traffic-light scale, not the accent: it has to read the same whatever colour the app is.</summary>
        public static Color Health(double share)
        {
            Color red = Hex("#E0685C"), yellow = Hex("#F2D64B"), green = Hex("#3FD07A");
            if (share >= 0.995) return green;
            double t = Math.Max(0, Math.Min(1, (share - 0.5) / 0.5));
            return t < 0.5 ? Blend(red, yellow, t * 2) : Blend(yellow, green, (t - 0.5) * 2);
        }

        public static void FillRound(DrawingContext g, Rect r, double rad, Color fill, Color? border = null) =>
            g.DrawRectangle(B(fill), border is Color b ? new Pen(B(b), 1) : null, border != null ? r.Deflate(0.5) : r, rad, rad);

        // ---- the mark ----------------------------------------------------------------------------
        public const int Frames = 12;  // one loop of the "working" animation: each node gets 4 frames
        static readonly Point Hub = new Point(.54, .5);
        static readonly Point[] Nodes = { new Point(.20, .5), new Point(.79, .22), new Point(.79, .78) };

        /// <summary>Three nodes joined at a hub (connected devices). phase 0..1 = working: the nodes take turns - a
        /// packet runs out from the hub (when packets, big sizes only), the node lights up.</summary>
        public static void DrawNodes(DrawingContext g, Rect box, Color c, double phase = -1, bool packets = false)
        {
            double n = Math.Min(box.Width, box.Height), ox = box.X + (box.Width - n) / 2, oy = box.Y + (box.Height - n) / 2;
            Point P(Point p) => new Point(ox + p.X * n, oy + p.Y * n);
            double r = .135 * n, w = .095 * n;
            bool still = phase < 0;
            int active = still ? -1 : (int)(phase * 3) % 3;
            double local = still ? 0 : phase * 3 % 1;
            Color dim = still ? c : Alpha(c, .72), hi = Blend(c, White, .45);
            var hub = P(Hub);
            var pen = new Pen(B(still ? c : Alpha(c, .86)), w, lineCap: PenLineCap.Round);
            foreach (var p in Nodes) g.DrawLine(pen, hub, P(p));
            for (int i = 0; i < 3; i++)
            {
                double glow = i == active && local > .5 ? Math.Sin((local - .5) * Math.PI) : 0;
                double rr = r * (1 + .14 * glow);
                g.DrawEllipse(B(glow > 0 ? Blend(c, hi, glow) : dim), null, P(Nodes[i]), rr, rr);
            }
            if (packets && !still && local <= .55)
            {
                var to = P(Nodes[active]);
                double t = Math.Min(1, local / .5), pr = w * .42;
                g.DrawEllipse(B(hi), null, new Point(hub.X + (to.X - hub.X) * t, hub.Y + (to.Y - hub.Y) * t), pr, pr);
            }
        }

        static readonly Dictionary<(uint, int), byte[]> icons = new Dictionary<(uint, int), byte[]>();

        /// <summary>The mark as a 64 px PNG, in any colour, still (frame -1) or one frame of the working animation.</summary>
        public static byte[] IconPng(Color c, int frame = -1)
        {
            if (icons.TryGetValue((c.ToUInt32(), frame), out var png)) return png;
            const int n = 64;
            using (var bmp = new RenderTargetBitmap(new PixelSize(n, n)))
            {
                using (var g = bmp.CreateDrawingContext()) DrawNodes(g, new Rect(0, 0, n, n), c, frame < 0 ? -1 : (double)frame / Frames, true);
                var ms = new MemoryStream();
                bmp.Save(ms, PngBitmapEncoderOptions.Default);
                return icons[(c.ToUInt32(), frame)] = ms.ToArray();
            }
        }

        /// <summary>The mark as a window icon.</summary>
        static readonly Dictionary<(uint, int), WindowIcon> windowIcons = new Dictionary<(uint, int), WindowIcon>();
        public static WindowIcon Icon(Color c, int frame = -1)
        {
            if (!windowIcons.TryGetValue((c.ToUInt32(), frame), out var icon))
                windowIcons[(c.ToUInt32(), frame)] = icon = new WindowIcon(new MemoryStream(IconPng(c, frame)));
            return icon;
        }

        /// <summary>The mark drawn as a control (the footer's logo).</summary>
        public sealed class Mark : Control
        {
            public override void Render(DrawingContext g) => DrawNodes(g, new Rect(Bounds.Size), Accent);
        }
    }
}
