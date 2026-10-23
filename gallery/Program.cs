// Shade gallery - the showcase in a window (a button switches dark and light live), or `--docs REPO`: the checks, every
// chapter as a PNG in both modes under docs/preview, DESIGN.md's tokens and colours, and the landing page (docs/index.html).
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Shade;
using static Shade.Look;

sealed class GalleryApp : Application
{
    public override void Initialize() => Look.Apply(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life) life.MainWindow = Gallery.Window();
        base.OnFrameworkInitializationCompleted();
    }
}

static class Program
{
    const string Inter = "avares://Shade/Assets/Inter#Inter";

    [STAThread]
    static void Main(string[] args)
    {
        Look.AppName = "Shade";
        if (args.Length == 2 && args[0] == "--docs")
        {
            AppBuilder.Configure<GalleryApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia()
                .With(new FontManagerOptions { DefaultFamilyName = Inter }).SetupWithoutStarting();
            if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(Inter)), out var glyph) || glyph.FamilyName != "Inter") throw new Exception("Embedded Inter did not resolve");
            Gallery.Checks();
            Gallery.Docs(args[1]);
            Console.WriteLine("Checks passed; previews, DESIGN.md and docs/index.html written to " + args[1]);
            return;
        }
        AppBuilder.Configure<GalleryApp>().UsePlatformDetect().With(new FontManagerOptions { DefaultFamilyName = Inter }).StartWithClassicDesktopLifetime(args);
    }
}

static class Gallery
{
    static Control Page(bool interactive)
    {
        var page = new StackPanel { Margin = new Thickness(32, 24) };
        for (int i = 0; i < Showcase.Chapters.Length; i++) page.Children.Add(Showcase.Chapter(i, interactive));
        return page;
    }

    /// <summary>The gallery window; the mode button switches it live.</summary>
    public static Window Window()
    {
        var mode = new Btn("Light mode", BtnKind.Ghost);
        mode.Click += () => { SetMode(Current == Mode.Dark ? Mode.Light : Mode.Dark); mode.Text = Current == Mode.Dark ? "Light mode" : "Dark mode"; };
        var head = FormLayout.Header("Shade", "The design system: colours, type, components, spacing, layout and navigation.", mode);
        var top = new Border { Child = head, Padding = new Thickness(32, 16, 32, 0) };
        var d = new DockPanel(); DockPanel.SetDock(top, Dock.Top); d.Children.Add(top); d.Children.Add(new ScrollViewer { Content = Page(true) });
        return new Window { Title = "Shade gallery", Width = 1240, Height = 900, Icon = Look.Icon(Accent), Background = B(Bg), Content = d };
    }

    static void Settle(int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);  // the headless clock only moves when ticked (dialogs rise in, the sidebar rolls)
        while (DateTime.UtcNow < until) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(10); }
    }

    static void Snap(Control content, string file, double width)
    {
        using var image = Render(content, width);
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        image.Save(file, PngBitmapEncoderOptions.Default);
    }

    /// <summary>A chapter in a window of its own natural height, rendered; the window is closed after.</summary>
    static RenderTargetBitmap Render(Control content, double width, Action shown = null)
    {
        var window = new Window { Width = width, Height = 800, Background = B(Bg), Content = new Border { Padding = new Thickness(32, 28, 32, 0), Child = content } };
        window.Show();
        if (shown != null) { Look.Windows = () => new[] { window }; shown(); }
        Settle(300);
        for (int i = 0; i < 3; i++) { window.Measure(new Size(width, window.Height)); window.Arrange(new Rect(0, 0, width, window.Height)); Dispatcher.UIThread.RunJobs(); }
        var body = (Control)window.Content;
        body.Measure(new Size(width, double.PositiveInfinity));
        window.Height = Math.Ceiling(body.DesiredSize.Height);
        window.Measure(new Size(width, window.Height)); window.Arrange(new Rect(0, 0, width, window.Height)); Dispatcher.UIThread.RunJobs();
        var image = new RenderTargetBitmap(new PixelSize((int)width, (int)window.Height)); image.Render(window);
        window.Close();
        return image;
    }

    static byte[] Pixels(Bitmap b)
    {
        var bytes = new byte[b.PixelSize.Width * b.PixelSize.Height * 4];
        var pin = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { b.CopyPixels(new PixelRect(b.PixelSize), pin.AddrOfPinnedObject(), bytes.Length, b.PixelSize.Width * 4); } finally { pin.Free(); }
        return bytes;
    }

    /// <summary>Live switching paints exactly what a fresh build paints: each chapter built in one mode and switched to the
    /// other must match, pixel for pixel, the same chapter built in that mode. A stale colour anywhere shows as a difference.</summary>
    static void CheckLiveSwitch()
    {
        foreach (var (from, to) in new[] { (Mode.Dark, Mode.Light), (Mode.Light, Mode.Dark) })
            for (int i = 0; i < Showcase.Chapters.Length; i++)
            {
                SetMode(from);
                using var switched = Render(Showcase.Chapter(i, false), 1240, () => SetMode(to));
                using var fresh = Render(Showcase.Chapter(i, false), 1240);
                var (a, b) = (Pixels(switched), Pixels(fresh));
                int width = switched.PixelSize.Width, first = -1, diff = 0;
                if (a.Length != b.Length) throw new Exception($"Live switch {from}→{to}, {Showcase.Chapters[i].title}: a different height");
                for (int k = 0; k < a.Length; k += 4) if (a[k] != b[k] || a[k + 1] != b[k + 1] || a[k + 2] != b[k + 2]) { diff++; if (first < 0) first = k / 4; }
                if (diff > 0) throw new Exception($"Live switch {from}→{to}, {Showcase.Chapters[i].title}: {diff} pixels differ from a fresh build, the first at x {first % width}, y {first / width}");
            }
        Look.Windows = () => Array.Empty<Window>();
        SetMode(Mode.Dark);
    }

    /// <summary>Every chapter in both modes (docs/preview/dark|light/NN-slug.png) and DESIGN.md's tokens from the code.</summary>
    public static void Docs(string repo)
    {
        foreach (var mode in new[] { Mode.Dark, Mode.Light })
        {
            SetMode(mode);
            for (int i = 0; i < Showcase.Chapters.Length; i++)
                Snap(Showcase.Chapters[i].build(false), Path.Combine(repo, "docs", "preview", mode.ToString().ToLowerInvariant(), $"{i + 1:00}-{Showcase.Chapters[i].slug}.png"), 1240);
        }
        SetMode(Mode.Dark);
        DesignDoc.Write(Path.Combine(repo, "DESIGN.md"));
        Landing.Write(repo);
    }

    /// <summary>Checks run before the docs are made: a failure stops them.</summary>
    public static void Checks()
    {
        static void Check(bool ok, string what) { if (!ok) throw new Exception("Check failed: " + what); }
        foreach (var mode in new[] { Mode.Dark, Mode.Light })
        {
            SetMode(mode);
            // text roles stay readable on the surface they sit on: AA for text, 3:1 for status colours beside their words
            Check(Contrast(Look.Text, Surface) >= 7, mode + ": Text on Raised");
            Check(Contrast(Second, Surface) >= 4.5, mode + ": Text 2 on Raised");
            Check(Contrast(Faint, Surface) >= 4.5, mode + ": Text 3 on Raised");
            Check(Contrast(AccentText, Surface) >= 4.5, mode + ": accent text on Raised");
            Check(Contrast(OnAccent, Accent) >= 4.5, mode + ": text on the accent");
            Check(Contrast(DangerHi, Surface) >= 4.5, mode + ": danger text on Raised");
            foreach (var (name, c) in new[] { ("Success", Success), ("Warning", Warn), ("Danger", Danger), ("Info", Override) })
                Check(Contrast(c, Surface) >= 3, mode + ": " + name + " on Raised");
        }
        foreach (var (name, dark, light) in Accents)  // every curated accent, in its own mode
            foreach (var (mode, c) in new[] { (Mode.Dark, dark), (Mode.Light, light) })
            {
                SetMode(mode);
                Check(Contrast(c, Bg) >= 4.5, $"{mode}: the {name} accent on the page");
                Check(Math.Max(Contrast(c, Ink), Contrast(c, White)) >= 4.5, $"{mode}: text on the {name} accent");
                Check(NearStatus(c) == null, $"{mode}: the {name} accent is clear of the status colours");
            }
        SetMode(Mode.Dark);
        CheckSuggestionShape();
        CheckLiveSwitch();
    }

    /// <summary>A real suggestion field, typed into: its list opens and the field keeps the open shape through focus restyles.</summary>
    static void CheckSuggestionShape()
    {
        var field = new SuggestionField("Name", new[] { "Annual report", "Annual budget" }) { Width = 220 };
        var window = new Window { Width = 300, Height = 200, Content = new StackPanel { Children = { field } } };
        window.Show(); field.Box.Focus(); field.Box.Text = "ann"; Dispatcher.UIThread.RunJobs();
        static void Check(bool ok, string what) { if (!ok) throw new Exception("Suggestion field: " + what); }
        Check(field.SuggestionsOpen, "the list opens on a matching prefix");
        Check(field.BorderThickness.Bottom == 0 && field.CornerRadius.BottomLeft == 0, "an open field has no bottom line or bottom corners");
        Check(field.BorderBrush is ISolidColorBrush b && b.Color == MenuEdge, "an open, focused field takes the menu's edge colour");
        field.Box.Text = "zzz"; Dispatcher.UIThread.RunJobs();
        Check(!field.SuggestionsOpen && field.BorderThickness.Bottom == 1 && field.CornerRadius.BottomLeft == 4, "a closed field is whole again");
        window.Close();
    }
}
