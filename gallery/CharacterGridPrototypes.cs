// The two approved paper appearances of the shared CharacterGridField, with synthetic document values.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shade;
using static Shade.Look;

internal static class CharacterGridPrototypes
{
    static StackPanel Stack(double gap, params Control[] children)
    {
        var panel = new StackPanel { Spacing = gap };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    // These are host-owned document brushes. They deliberately retain paper contrast in a dark shell.
    sealed class Sample : Border, IRestyle
    {
        readonly List<(CharacterGridField field, bool green)> fields = new();
        readonly List<TextBlock> captions = new();
        readonly SolidColorBrush paperFill = new(White), paperInk = new(Ink), paperRule = new(Blend(White, Ink, .28));
        readonly SolidColorBrush yellow = new(), green = new(), yellowTail = new(), greenTail = new();
        readonly CharacterGridAppearance appearance;
        bool Paper => appearance == CharacterGridAppearance.Paper;
        readonly TextBlock value;
        readonly Dictionary<CharacterGridField, string> initial = new();

        internal Sample(CharacterGridAppearance appearance)
        {
            this.appearance = appearance;
            Padding = new Thickness(16);
            BorderThickness = new Thickness(1);
            BorderBrush = B(Look.Separator);
            HorizontalAlignment = HorizontalAlignment.Left;
            value = Build.Text("Click a field to inspect its value", Text3, TextSize.Caption);
            value.TextWrapping = TextWrapping.Wrap;
            var content = Stack(16,
                Label("RECORD / 026", true),
                Label("Original", true),
                Entry("Serial number", "0026018342", 16),
                Entry("Certification", "M26 0122", 12),
                Entry("Year", "2026 / 2026", 11),
                Entry("Reading", "024581,36", 9, tail: 7),
                Label("Replacement", true),
                Entry("Serial number", "0031084726", 16, green: true),
                Entry("Reading", "000000,00", 9, green: true, tail: 7),
                Entry("Your value", "", 16),
                value);
            Child = content;
            Restyle();
        }

        TextBlock Label(string text, bool heading = false)
        {
            var label = Build.Text(text, Text2, heading ? TextSize.Label : TextSize.Caption, heading ? 600 : 400);
            captions.Add(label);
            return label;
        }

        Control Entry(string caption, string text, int count, bool green = false, int tail = -1)
        {
            var field = new CharacterGridField { CellCount = count, Appearance = appearance, Text = text };
            if (tail >= 0) field.CellDecorations!.Add(new CharacterCellDecoration { Start = tail, Length = count - tail });
            fields.Add((field, green)); initial[field] = text;
            AutomationProperties.SetName(field, caption);
            void Report() => value.Text = $"{caption}: \"{field.Text}\" · {new StringInfo(field.Text ?? "").LengthInTextElements}/{field.CellCount} cells";
            field.GotFocus += (_, _) => Report();
            field.TextChanged += (_, _) => { if (field.IsFocused) Report(); };
            return Stack(4, Label(caption), field);
        }

        internal void ReadOnly(bool readOnly) { foreach (var (field, _) in fields) field.IsReadOnly = readOnly; }
        internal void Reset() { foreach (var (field, text) in initial) field.SetCurrentValue(TextBox.TextProperty, text); }

        public void Restyle()
        {
            yellow.Color = Blend(White, Hex(Palette.First(p => p.hue == "yellow").shades[0]), .6);
            green.Color = Blend(White, Hex(Palette.First(p => p.hue == "green").shades[0]), .6);
            yellowTail.Color = Blend(yellow.Color, Ink, .10); greenTail.Color = Blend(green.Color, Ink, .10);
            Background = Paper ? paperFill : B(Raised);
            foreach (var label in captions) label.Foreground = Paper ? paperInk : B(Text2);
            value.Foreground = Paper ? paperInk : B(Text3);
            foreach (var (field, isGreen) in fields)
            {
                field.Background = Paper ? isGreen ? green : yellow : B(Raised);
                field.Foreground = Paper ? paperInk : B(Look.Text);
                field.CaretBrush = Paper ? paperInk : B(Look.Text);
                field.SelectionForegroundBrush = Paper ? paperInk : B(Look.Text);
                field.CellBorderBrush = Paper ? paperRule : B(Look.Separator);
                field.BorderBrush = Paper ? paperRule : B(Look.Separator);
                if (field.CellDecorations?.FirstOrDefault() is { } tail) tail.Background = Paper ? isGreen ? greenTail : yellowTail : B(Fill);
            }
        }
    }

    internal static Control Page()
    {
        var samples = new[] { new Sample(CharacterGridAppearance.Paper), new Sample(CharacterGridAppearance.Field) };
        var compare = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        var names = new[] { "A · Printed boxes", "C · Shade field" };
        var notes = new[] { "Compact, square, continuous grid", "Larger cells, ordinary theme roles" };
        for (int i = 0; i < samples.Length; i++)
            compare.Children.Add(Stack(8, Build.Text(names[i], Look.Text, TextSize.Section, 600), Build.Text(notes[i], Text3, TextSize.Caption), samples[i]));

        var document = new LayoutTransformControl { Child = compare };
        var zoom = new ChoiceButton { Items = new[] { "75%", "100%", "125%", "150%" }, Current = "100%", Width = M(88) };
        AutomationProperties.SetName(zoom, "Document zoom");
        zoom.SelectionChanged += (_, _) =>
        {
            if (double.TryParse(zoom.Current.TrimEnd('%'), out double percent)) document.LayoutTransform = new ScaleTransform(percent / 100, percent / 100);
        };
        var readOnly = new CheckBox { Content = "Read-only", VerticalAlignment = VerticalAlignment.Center };
        readOnly.IsCheckedChanged += (_, _) => { foreach (var sample in samples) sample.ReadOnly(readOnly.IsChecked == true); };
        var reset = new ActionButton("Reset examples", ButtonKind.Ghost);
        reset.Click += (_, _) => { foreach (var sample in samples) sample.Reset(); };
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(16), Children = { new ModeSwitch(), zoom, readOnly, reset } };
        var intro = FormLayout.Header("Character grid", "Both approved treatments. Click a box, type, select and paste to compare the feel.");
        var footer = Build.Text("Both appearances use CharacterGridField. Editing, selection, value automation and validation are shared.", Text3, TextSize.Caption);
        footer.MaxWidth = 640;
        footer.TextWrapping = TextWrapping.Wrap;
        return Stack(24, intro, controls, document, footer);
    }

    internal static Window Window() => new()
    {
        Title = "Shade · Character grid prototypes",
        Width = 820,
        Height = 900,
        Background = B(Look.Background),
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(32, 24), Child = Page() }
        }
    };

    static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    internal static void Previews(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var mode in new[] { Mode.Dark, Mode.Light })
        {
            SetMode(mode);
            var window = Window();
            window.Show(); Settle();
            var first = window.GetVisualDescendants().OfType<CharacterGridField>().First();
            first.Focus(); first.SelectionStart = 2; first.SelectionEnd = 6; Settle();
            using var image = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
            image.Render(window);
            image.Save(Path.Combine(directory, $"character-grid-{mode.ToString().ToLowerInvariant()}.png"), PngBitmapEncoderOptions.Default);
            window.Close();
        }
        SetMode(Mode.Dark);
    }

    internal static void Checks()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception("Character grid prototype: " + message); }
        var model = new TextBox { Text = "0026018342" };
        var field = new CharacterGridField();
        field.Bind(TextBox.TextProperty, new Binding(nameof(TextBox.Text)) { Source = model, Mode = BindingMode.TwoWay });
        var scaled = new LayoutTransformControl { Child = field, HorizontalAlignment = HorizontalAlignment.Left };
        var window = new Window { Width = 600, Height = 200, Content = new StackPanel { Children = { scaled } } };
        window.Show(); Settle();
        var presenter = field.GetVisualDescendants().OfType<TextPresenter>().Single();
        for (int i = 1; i < field.Text.Length; i++)
        {
            double distance = presenter.TextLayout.HitTestTextPosition(i).X - presenter.TextLayout.HitTestTextPosition(i - 1).X;
            Check(Math.Abs(distance - field.CellSize.Width) < .1, $"character {i} follows the cell pitch ({distance})");
        }
        var size = field.Bounds.Size;
        var point = field.TranslatePoint(new Point(field.CellSize.Width * 4 + field.Padding.Left + field.BorderThickness.Left + 1, field.Bounds.Height / 2), window).Value;
        window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Settle();
        Check(field.CaretIndex == 4 && field.SelectionStart == field.SelectionEnd, $"click places the caret without selecting the whole value (caret={field.CaretIndex}, selection={field.SelectionStart}..{field.SelectionEnd}, origin={presenter.TranslatePoint(default, field)}, bounds={field.Bounds.Size})");
        window.KeyTextInput("9"); Settle();
        Check(field.Text == "00269018342" && model.Text == field.Text && field.Bounds.Size == size, "native insertion preserves binding, leading zeroes and geometry");
        field.Undo(); Settle(); Check(field.Text == "0026018342", "native undo restores the value");
        field.SelectionStart = 2; field.SelectionEnd = 5;
        window.KeyTextInput("ABC"); Settle(); Check(field.Text == "00ABC18342", "native selection replacement spans cells");
        model.Text = "Žluťoučký"; Settle(); Check(field.Text == model.Text, "external updates still reach the editor after user edits");
        for (int i = 1; i < field.Text.Length; i++)
            Check(Math.Abs(presenter.TextLayout.HitTestTextPosition(i).X - presenter.TextLayout.HitTestTextPosition(i - 1).X - field.CellSize.Width) < .1, "precomposed Czech characters follow the cell pitch");
        foreach (var scale in new[] { .75, 1d, 1.5 })
        {
            scaled.LayoutTransform = new ScaleTransform(scale, scale); Settle();
            var at = field.TranslatePoint(new Point(field.CellSize.Width * 3 + field.Padding.Left + field.BorderThickness.Left + 1, field.Bounds.Height / 2), window).Value;
            window.MouseDown(at, MouseButton.Left); window.MouseUp(at, MouseButton.Left); Settle();
            Check(field.CaretIndex == 3 && field.SelectionStart == field.SelectionEnd, $"pointer placement follows {scale:P0} document zoom");
        }
        model.Text = "0026018342"; Settle();
        field.IsReadOnly = true; window.KeyTextInput("7"); Settle(); Check(field.Text == "0026018342", "read-only prevents edits");
        field.IsReadOnly = false;
        model.Text = "01234567890123456789"; Settle();
        Check(field.Text.Length == 20 && field.Bounds.Size == size && field.Classes.Contains(":invalid"), "overflow preserves the value and fixed geometry");
        window.Close();
        CheckLiveModes();
        Console.WriteLine("Character grid prototype: pitch, pointer/zoom, binding, insertion, selection, Czech text, undo, read-only, overflow and live mode checks passed.");
    }

    static byte[] Pixels(Window window)
    {
        using var image = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
        image.Render(window);
        var pixels = new byte[image.PixelSize.Width * image.PixelSize.Height * 4];
        var pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { image.CopyPixels(new PixelRect(image.PixelSize), pin.AddrOfPinnedObject(), pixels.Length, image.PixelSize.Width * 4); }
        finally { pin.Free(); }
        return pixels;
    }

    static void CheckLiveModes()
    {
        var oldWindows = Look.Windows;
        try
        {
            foreach (var (from, to) in new[] { (Mode.Dark, Mode.Light), (Mode.Light, Mode.Dark) })
            {
                SetMode(from);
                var live = Window(); live.Show(); Settle();
                Look.Windows = () => new[] { live };
                SetMode(to); Settle();
                var fresh = Window(); fresh.Show(); Settle();
                if (!Pixels(live).SequenceEqual(Pixels(fresh))) throw new Exception($"Character grid prototype: live {from} to {to} differs from fresh construction");
                live.Close(); fresh.Close();
            }
        }
        finally { Look.Windows = oldWindows; SetMode(Mode.Dark); }
    }
}
