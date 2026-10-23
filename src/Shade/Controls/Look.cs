// Shade - the look: the colours and their accent shades, the fonts and the node mark.
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

namespace Shade
{
    public static class Look
    {
        public static Color Hex(string h) => Color.Parse(h);
        public static string HexOf(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        public static Color Alpha(Color c, double a) => Color.FromArgb((byte)Math.Round(255 * Math.Max(0, Math.Min(1, a))), c.R, c.G, c.B);
        public static Color Blend(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
        /// <summary>A fixed colour's brush; computed colours (blends, tints) are painted again by the control's Restyle.</summary>
        public static IBrush B(Color c) => new SolidColorBrush(c);
        /// <summary>A role's brush: one shared brush per role, so whatever is painted with it follows a new mode or accent at once.</summary>
        public static IBrush B(Tone t) => t.Brush;

        /// <summary>The two looks. SetMode switches them live: every role's brush changes colour, and controls that work their
        /// colours out (blends, states) paint again.</summary>
        public enum Mode { Dark, Light }
        public static Mode Current { get; private set; } = Mode.Dark;

        /// <summary>The dark ink: text on light fills in both modes.</summary>
        public static readonly Color Ink = Hex("#151519"), White = Colors.White;
        // The neutral roles (DESIGN_SYSTEM.md §2) and the status colours: set by SetMode.
        public static readonly Tone Bg = new(), Surface = new(), Surface2 = new(), Border = new(), MenuEdge = new(), Text = new(), Second = new(),
            Faint = new(), Disabled = new(), Success = new(), Far = new(), Hairline = new();
        public const string DefaultAccent = "#8C7AE6";
        /// <summary>The curated accents: a value for each mode, both readable on their page (4.5:1), with readable text on
        /// them, and clear of the status hues. The first is the default.</summary>
        public static readonly (string name, Color dark, Color light)[] Accents =
        {
            ("Violet", Hex("#8C7AE6"), Hex("#6A58D6")), ("Indigo", Hex("#818CF8"), Hex("#4F46E5")), ("Purple", Hex("#C084FC"), Hex("#9333EA")),
            ("Fuchsia", Hex("#E879F9"), Hex("#A21CAF")), ("Pink", Hex("#F472B6"), Hex("#BE185D")), ("Teal", Hex("#2DD4BF"), Hex("#0F766E")),
            ("Lime", Hex("#A3E635"), Hex("#4D7C0F")), ("Graphite", Hex("#A1A1AA"), Hex("#52525B")),
        };
        static Color accentDark = Accents[0].dark, accentLight = Accents[0].light;
        /// <summary>The 4 px spacing grid (DESIGN_SYSTEM.md §4): the only gaps used.</summary>
        public static readonly double[] Spaces = { 4, 8, 12, 16, 24, 32 };
        /// <summary>Corners (controls and menus; dialogs and cards; rows and the sidebar have none) and the two control heights.</summary>
        public const double Radius = 4, CardRadius = 8, ControlHeight = 28, ChipHeight = 24;
        /// <summary>The app's name: dialog titles and the default window title. Set by Apply.</summary>
        public static string AppName { get; private set; } = "App";
        /// <summary>Whether the windows take the mark as their icon, in the accent (Apply's markIcon).</summary>
        static bool markIcon;
        public static readonly Tone Accent = new(Hex(DefaultAccent)), AccentText = new(), OnAccent = new(), AccentHi = new(), AccentLo = new(), AccentLine = new(),
            Soft = new(), SoftHi = new(), SoftLine = new(), CurrentRow = new(), Selection = new(), Danger = new(), DangerHi = new(), DangerMuted = new(), Override = new(), Warn = new(), Up = new();
        static Look() { SetMode(Mode.Dark); }

        /// <summary>Every colour role in one mode: the source of DESIGN.md's tokens.</summary>
        static readonly Dictionary<Mode, (string bg, string raised, string fill, string line, string text, string second, string faint, string success, string warn, string danger, string info)> Palettes = new()
        {
            [Mode.Dark] = ("#151519", "#1B1B20", "#29292F", "#35353D", "#EDEDF0", "#B1B1BC", "#9999A7", "#83CEA5", "#FFB142", "#E0685C", "#6FA8DC"),
            [Mode.Light] = ("#F4F4F7", "#FFFFFF", "#ECECF1", "#DADAE1", "#18181C", "#474752", "#676775", "#1F8A57", "#A86400", "#C4453A", "#2C6CB0"),
        };

        /// <summary>Dark or light, live: the neutral roles, the status colours and the accent's shades, then every open window
        /// paints again (Repaint).</summary>
        public static void SetMode(Mode mode)
        {
            Current = mode;
            var p = Palettes[mode];
            Bg.Set(Hex(p.bg)); Surface.Set(Hex(p.raised)); Surface2.Set(Hex(p.fill)); Border.Set(Hex(p.line));
            Text.Set(Hex(p.text)); Second.Set(Hex(p.second)); Faint.Set(Hex(p.faint)); Success.Set(Hex(p.success));
            Disabled.Set(Blend(Bg, Faint, 0.5));
            MenuEdge.Set(Blend(Border, Second, 0.35));  // a field's or dropdown's border on hover and while its menu is open
            Hairline.Set(Alpha(Border, 0.6));  // the faint line above a block
            Far.Set(mode == Mode.Dark ? White : Ink);  // the high-contrast end: text lifted toward it reads better
            Danger.Set(Hex(p.danger)); Warn.Set(Hex(p.warn)); Override.Set(Hex(p.info));
            if (app != null) app.RequestedThemeVariant = mode == Mode.Dark ? ThemeVariant.Dark : ThemeVariant.Light;  // Fluent's own parts
            MakeAccent(mode == Mode.Dark ? accentDark : accentLight);
        }

        /// <summary>The colour roles' rules (DESIGN_SYSTEM.md §2): a 15 % tint over Raised, a hover 10 % lighter, text on a solid colour.</summary>
        public static Color Tint(Color c, double amount = 0.15) => Blend(Surface, c, amount);
        public static Color Hover(Color c) => Blend(c, White, 0.10);

        /// <summary>One accent for both modes (a custom colour), live.</summary>
        public static void SetAccent(Color a) => SetAccent(a, a);

        /// <summary>An accent with a value for each mode (a curated one, Accents), live; SetMode takes the mode's value.</summary>
        public static void SetAccent(Color dark, Color light)
        {
            (accentDark, accentLight) = (dark, light);
            MakeAccent(Current == Mode.Dark ? dark : light);
        }

        /// <summary>The accent and everything made from it by the rules; the status colours come with the mode (Danger,
        /// Warn, Info = Override, Success; Up is Success). Then every window paints again.</summary>
        static void MakeAccent(Color a)
        {
            Accent.Set(Color.FromRgb(a.R, a.G, a.B));
            Color text = Accent;
            for (int i = 0; i < 20 && Contrast(text, Surface) < 4.5; i++) text = Blend(text, Far, 0.08);
            AccentText.Set(text);
            OnAccent.Set(OnColor(Accent));
            AccentHi.Set(Hover(Accent)); AccentLo.Set(Blend(Accent, Ink, 0.10)); AccentLine.Set(Blend(Bg, Accent, 0.50));
            Soft.Set(Tint(Accent)); SoftHi.Set(Tint(Accent, 0.25)); SoftLine.Set(Blend(Border, Accent, 0.60));
            CurrentRow.Set(Tint(Accent, 0.06)); Selection.Set(Alpha(Accent, .45));  // selected text
            DangerHi.Set(Blend(Danger, Far, 0.10)); DangerMuted.Set(Tint(Danger));  // DangerHi is danger text: lifted toward the high-contrast end
            Up.Set(Success);
            icons.Clear();
            Repaint();
        }

        /// <summary>The status colour an accent is too close to (hue within 20°, both saturated), or null.</summary>
        public static string? NearStatus(Color c)
        {
            var (hue, sat) = HueSat(c);
            if (sat < 0.3) return null;
            foreach (var (name, s) in new[] { ("Success", Success), ("Warning", Warn), ("Danger", Danger), ("Info", Override) })
            {
                var (h2, _) = HueSat(s);
                double d = Math.Abs(hue - h2); if (Math.Min(d, 360 - d) < 20) return name;
            }
            return null;
        }

        /// <summary>The windows a new mode or accent repaints: the app's, or the ones a headless run sets.</summary>
        internal static Func<IEnumerable<Window>> Windows = () =>
            (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows ?? (IEnumerable<Window>)Array.Empty<Window>();

        /// <summary>Paints every open window again: role brushes already follow; controls that work colours out (IRestyle)
        /// and drawn controls (Render) need a new pass. Visual tree: rows draw child switches outside the logical tree.</summary>
        public static void Repaint()
        {
            static void Walk(Visual v)
            {
                (v as IRestyle)?.Restyle();
                v.InvalidateVisual();
                foreach (var k in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(v)) Walk(k);
            }
            foreach (var w in Windows().ToList()) { if (markIcon) w.Icon = Icon(Accent); Walk(w); }
        }

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
        public static Color OnColor(Color c) => Contrast(c, Ink) >= Contrast(c, White) ? Ink : White;

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

        /// <summary>The app-wide styles on top of Fluent's theme, once, from Application.Initialize. appName titles the dialogs;
        /// markIcon gives every window Shade's mark as its icon, in the accent (an app with its own icon leaves it off).</summary>
        public static void Apply(Application app, string appName = "App", bool markIcon = false)
        {
            if (Look.app != null) throw new InvalidOperationException("Look.Apply runs once, from Application.Initialize");
            app.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
            Look.app = app; AppName = appName; Look.markIcon = markIcon;
            app.RequestedThemeVariant = Current == Mode.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
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
            ApplyTip();
            app.Resources[typeof(ScrollBar)] = ThinScrollBar();
        }

        static Application? app;
        /// <summary>Tooltips: the Fill colour, the Outline edge, Label text (Fluent's grey otherwise).</summary>
        static void ApplyTip()
        {
            var tip = new Avalonia.Styling.Style(x => x.OfType<ToolTip>());
            foreach (var (p, v) in new (AvaloniaProperty, object)[] { (TemplatedControl.BackgroundProperty, B(Surface2)), (TemplatedControl.ForegroundProperty, B(Text)),
                         (TemplatedControl.BorderBrushProperty, B(MenuEdge)), (TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                         (TemplatedControl.CornerRadiusProperty, new CornerRadius(4)), (TemplatedControl.PaddingProperty, new Thickness(8, 5)), (TemplatedControl.FontSizeProperty, Label) })
                tip.Setters.Add(new Avalonia.Styling.Setter(p, v));
            app!.Styles.Add(tip);
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
        public static readonly FontFamily UiFont = new FontFamily("avares://Shade/Assets/Inter#Inter"),
            Mono = new FontFamily("avares://Shade/Assets#Cascadia Code");
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
        public const int Frames = 12;  // one loop of the "working" animation

        /// <summary>The mark, two offset discs (assets/shade.svg): the back one in the accent, the front one in Text, so it
        /// follows the mode. phase 0..1 = working: the pair orbits once around the mark's centre, as it does when the sidebar
        /// collapses (Sidebar turns the whole mark).</summary>
        public static void DrawMark(DrawingContext g, Rect box, Color back, Color front, double phase = -1)
        {
            double n = Math.Min(box.Width, box.Height), cx = box.X + box.Width / 2, cy = box.Y + box.Height / 2;
            double off = 23 / 256.0 * n, r = 76 / 256.0 * n;
            double a = phase < 0 ? 0 : Math.PI * (1 - Math.Cos(Math.PI * phase));  // one turn, eased in and out
            double dx = off * (Math.Cos(a) + Math.Sin(a)), dy = off * (Math.Sin(a) - Math.Cos(a));  // (off, -off) turned by a
            g.DrawEllipse(B(back), null, new Point(cx + dx, cy + dy), r, r);
            g.DrawEllipse(B(front), null, new Point(cx - dx, cy - dy), r, r);
        }

        static readonly Dictionary<(uint, uint, int), byte[]> icons = new Dictionary<(uint, uint, int), byte[]>();

        /// <summary>The mark as a 64 px PNG, its back disc in c and its front one in Text, still (frame -1) or one frame of the
        /// working animation.</summary>
        public static byte[] IconPng(Color c, int frame = -1)
        {
            Color front = Text;
            if (icons.TryGetValue((c.ToUInt32(), front.ToUInt32(), frame), out var png)) return png;
            const int n = 64;
            using (var bmp = new RenderTargetBitmap(new PixelSize(n, n)))
            {
                using (var g = bmp.CreateDrawingContext()) DrawMark(g, new Rect(0, 0, n, n), c, front, frame < 0 ? -1 : (double)frame / Frames);
                var ms = new MemoryStream();
                bmp.Save(ms, PngBitmapEncoderOptions.Default);
                return icons[(c.ToUInt32(), front.ToUInt32(), frame)] = ms.ToArray();
            }
        }

        /// <summary>The mark as a window icon.</summary>
        static readonly Dictionary<(uint, uint, int), WindowIcon> windowIcons = new Dictionary<(uint, uint, int), WindowIcon>();
        public static WindowIcon Icon(Color c, int frame = -1)
        {
            var key = (c.ToUInt32(), ((Color)Text).ToUInt32(), frame);
            if (!windowIcons.TryGetValue(key, out var icon))
                windowIcons[key] = icon = new WindowIcon(new MemoryStream(IconPng(c, frame)));
            return icon;
        }

        /// <summary>The mark drawn as a control (the footer's logo).</summary>
        public sealed class Mark : Control
        {
            public override void Render(DrawingContext g) => DrawMark(g, new Rect(Bounds.Size), Accent, Text);
        }
    }

    /// <summary>A control that works some colours out (a blend, a tint, a state): Look.Repaint calls Restyle after a new mode
    /// or accent, so it paints what a fresh build would. Role brushes (B(Tone)) follow without it.</summary>
    public interface IRestyle
    {
        void Restyle();
    }

    /// <summary>A colour role: its colour now, and the one brush every control paints the role with. Look changes the brush's
    /// colour for a new mode or accent, so all of it follows at once. Use it as a Color for blends and contrast.</summary>
    public sealed class Tone
    {
        public readonly SolidColorBrush Brush = new SolidColorBrush();
        public Color Color { get; private set; }
        public Tone() { }
        /// <summary>A fixed colour as a role (a category's colour): it never changes.</summary>
        public Tone(Color c) { Set(c); }
        internal void Set(Color c) { Color = c; Brush.Color = c; }
        public static implicit operator Color(Tone t) => t.Color;
        public override string ToString() => Look.HexOf(Color);
    }
}
