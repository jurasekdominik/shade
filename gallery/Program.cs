// Baseline gallery - every shared control in its states. Run it to look; `--png DIR` writes gallery.png and dialog.png headless.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Baseline;
using static Baseline.Look;

sealed class GalleryApp : Application
{
    public override void Initialize() => Look.Apply(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life) life.MainWindow = Gallery.Window(interactive: true);
        base.OnFrameworkInitializationCompleted();
    }
}

static class Program
{
    const string Inter = "avares://Baseline/Assets/Inter#Inter";

    [STAThread]
    static void Main(string[] args)
    {
        Look.AppName = "Baseline";
        if (args.Length == 2 && args[0] == "--png")
        {
            AppBuilder.Configure<GalleryApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia()
                .With(new FontManagerOptions { DefaultFamilyName = Inter }).SetupWithoutStarting();
            var face = new Typeface(new FontFamily(Inter));
            if (!FontManager.Current.TryGetGlyphTypeface(face, out var glyph) || glyph.FamilyName != "Inter") throw new Exception("Embedded Inter did not resolve");
            Gallery.Render(args[1]);
            Console.WriteLine("Gallery written to " + args[1]);
            return;
        }
        AppBuilder.Configure<GalleryApp>().UsePlatformDetect().With(new FontManagerOptions { DefaultFamilyName = Inter }).StartWithClassicDesktopLifetime(args);
    }
}

static class Gallery
{
    static TextBlock Label(string text) => new TextBlock { Text = text, Foreground = B(Faint), FontSize = 11, Margin = new Thickness(0, 0, 0, 6) };

    static StackPanel Row(params Control[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 0, 0, 14) };
        foreach (var c in items) row.Children.Add(c);
        return row;
    }

    static StackPanel Item(string caption, Control c) { var s = new StackPanel(); s.Children.Add(Label(caption)); s.Children.Add(c); return s; }

    static Control Section(string title, params Control[] rows)
    {
        var s = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        s.Children.Add(new TextBlock { Text = title, Foreground = B(Look.Text), FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var r in rows) s.Children.Add(r);
        return s;
    }

    /// <summary>The app shell as a window shows it: sidebar, a page with its header, the status bar.</summary>
    static Control ShellSample()
    {
        var nav = new[]
        {
            new NavEntry("Workspace", null, Icons.Monitor, new NavEntry("Read", "workspace/read"), new NavEntry("Write", "workspace/write")),
            new NavEntry("Jobs", "jobs", Icons.CalendarClock),
            new NavEntry("Logs", "logs", Icons.SquareTerminal),
            new NavEntry("Library", "library", Icons.Library),
            new NavEntry("Settings", "settings", Icons.Settings),
        };
        var sidebar = new Sidebar("baseline", "Design, shared.", nav);
        sidebar.Select("workspace/read"); sidebar.History.Update(true, false);
        sidebar.Navigate += sidebar.Select;
        var status = new StatusBar(); status.Show("Warning · Retry at 14:30", Warn, loud: true); status.Count("2 connections open", true);
        var page = new StackPanel { Margin = new Thickness(16) };
        page.Children.Add(FormLayout.Header("Read", "What a page header looks like inside the shell.", new Btn("Export", BtnKind.Ghost), new Btn("Run now", BtnKind.Primary)));
        return new Border { Height = 400, Width = 900, HorizontalAlignment = HorizontalAlignment.Left, BorderBrush = B(Look.Border), BorderThickness = new Thickness(1), Background = B(Bg), Child = Shell.Layout(sidebar, page, status) };
    }

    static Control Page(bool interactive)
    {
        var sizes = new StackPanel { Spacing = 4 };
        foreach (var (name, px, weight) in new[] { ("Caption 11", Caption, 400), ("Label 12", Look.Label, 500), ("Body 13", Body, 400), ("Section 14", Look.Section, 600), ("Title 18", Title, 600) })
            sizes.Children.Add(new TextBlock { Text = $"{name} · Daily yield 1 234,56 kWh · Retry at 14:30", FontSize = px, FontWeight = Weight(weight), Foreground = B(Look.Text) });
        sizes.Children.Add(new TextBlock { Text = "Mono 12 · 01 06 00 0A 00 64 · 127.0.0.1:502", FontSize = 12, FontFamily = Mono, Foreground = B(Second) });

        Btn Disabled(Btn b) { b.IsEnabled = false; return b; }
        var invalid = new Field("Invalid value") { Text = "abc" }; invalid.Invalid = true;
        var filled = new Field("Placeholder") { Text = "127.0.0.1", Width = 180 };
        Color? Method(string m) => Color.Parse(m switch { "GET" => "#55D6A0", "POST" => "#F3C86A", "PUT" => "#69B5F5", "DELETE" => "#FF8585", _ => "#C2A9E8" });
        var choice = new ChoiceButton { Width = 160, ItemColor = Method }; choice.SetItems(new[] { "GET", "POST", "PUT", "DELETE" }, "GET");
        var methods = new[] { "GET", "POST", "PUT", "DELETE" };
        var menu = Baseline.Menu.Panel(methods, i => { }, () => { }, 0, 160, i => Method(methods[i]), attached: true);
        choice.CornerRadius = new CornerRadius(4, 4, 0, 0); choice.BorderThickness = new Thickness(1, 1, 1, 0); choice.BorderBrush = B(MenuEdge);  // as it looks while open
        menu.Width = 160; menu.HorizontalAlignment = HorizontalAlignment.Left; choice.HorizontalAlignment = HorizontalAlignment.Left;
        var dropdown = new StackPanel(); dropdown.Children.Add(choice); dropdown.Children.Add(menu);
        var plain = new ChoiceButton { Width = 200 }; plain.SetItems(new[] { "Europe/Prague", "UTC" }, "Europe/Prague");
        var plainMenu = Baseline.Menu.Panel(new[] { "Edit", "Duplicate", Baseline.Menu.Divider, "Delete request" }, i => { }, () => { }, -1, 200);
        var tabs = new TextTabs("Params", "Headers", "Authorization", "Body"); tabs.SetActive(1);
        var chipOn = new FilterChip("Modbus"); chipOn.Active = true;
        var chips = new WrapPanel(); foreach (var st in new[] { "OK", "Failed", "Warning" }) chips.Children.Add(new FilterChip(st, st == "OK" ? Success : st == "Failed" ? Danger : Warn) { Active = st == "Failed", Margin = new Thickness(0, 0, 8, 0) });
        var tickOn = new Tick { State = true };
        var multi = new MultiChoice(new[] { "SG125CXP2", "SG250HX" }, "Every model") { Width = 180 };
        var table = Workbench.Table(new[] { "Date", "Period", "Price (EUR/MWh)" }, Enumerable.Range(1, 6).Select(i => new[] { "2026-10-14", i.ToString(), (80 + i * 3.25).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) }).ToList());
        table.Height = 190; table.Width = 420;
        var scroll = new ScrollViewer { Height = 120, Width = 260, Content = new StackPanel { Children = { Kit.Text(string.Join("\n", Enumerable.Range(1, 30).Select(i => "Line " + i)), Second, 9.75) } } };

        var page = new StackPanel { Margin = new Thickness(24) };
        page.Children.Add(new TextBlock { Text = "Baseline controls", Foreground = B(Look.Text), FontSize = 20, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 18) });
        page.Children.Add(Section("App shell: sidebar, page header, status bar", ShellSample()));
        // 13 named roles; tints, hovers and text on solid colours follow rules instead of stored shades
        var roles = new WrapPanel();
        foreach (var (name, color) in new[] { ("Background", Bg), ("Raised", Surface), ("Fill", Surface2), ("Separator", Look.Border), ("Outline", MenuEdge),
                     ("Text", Look.Text), ("Text 2", Second), ("Text 3", Faint), ("Accent", Accent), ("Success", Success), ("Warning", Warn), ("Danger", Danger), ("Info", Override) })
        {
            var s = new StackPanel { Width = 84, Margin = new Thickness(0, 0, 6, 10) };
            s.Children.Add(new Border { Height = 34, Background = B(color), BorderBrush = B(Look.Border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) });
            var derived = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 3, 0, 0) };
            if (name is "Accent" or "Success" or "Warning" or "Danger" or "Info")
                foreach (var d in new[] { Tint(color), Hover(color) }) derived.Children.Add(new Border { Width = 40, Height = 10, Background = B(d), CornerRadius = new CornerRadius(2) });
            s.Children.Add(derived);
            s.Children.Add(new TextBlock { Text = name, Foreground = B(Look.Text), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
            s.Children.Add(new TextBlock { Text = HexOf(color), Foreground = B(Faint), FontSize = 10, FontFamily = Mono });
            roles.Children.Add(s);
        }
        page.Children.Add(Section("Colour roles (the small bars: tint and hover, by rule)", roles));
        var accent = new ColorGrid(Accent);
        accent.Picked += (c, _) =>
        {
            var before = Shades(); SetAccent(c);
            if (TopLevel.GetTopLevel(accent) is Window w) Recolor(new[] { w }, before);
        };
        page.Children.Add(Section("Accent: pick one and every control follows (Look.SetAccent, Look.Recolor)", accent));
        page.Children.Add(Section("Type scale", sizes));
        var headers = new StackPanel { Width = 760, HorizontalAlignment = HorizontalAlignment.Left };
        headers.Children.Add(FormLayout.Header("Jobs", "Scheduled reads, retry windows and outputs.", new Btn("Export", BtnKind.Ghost), new Btn("Create job", BtnKind.Primary)));
        headers.Children.Add(FormLayout.SubHeader("Jobs", () => { }, "Daily yield", "2 sites · Daily 22:30–23:50", new Btn("Run now"), new Btn("Save", BtnKind.Primary)));
        page.Children.Add(Section("Page header and subpage header", headers));
        var compact = new StackPanel { Spacing = Kit.Gap, Width = FormLayout.EditorColumn };
        compact.Children.Add(Kit.Labelled("Name", new Field("Destination")));
        var multiline = Workbench.Lines("Optional report content"); multiline.Height = FormLayout.TextAreaHeight; compact.Children.Add(Kit.Labelled("Content", multiline));
        page.Children.Add(Section("Compact editor · shared form sizes", FormLayout.Block("Destination", "Fields and multiline content use the same sizing in every editor.", compact)));
        page.Children.Add(Section("Buttons", Row(Item("Primary", new Btn("Run now", BtnKind.Primary)), Item("Secondary", new Btn("Save")), Item("Ghost", new Btn("Cancel", BtnKind.Ghost)),
            Item("Danger", new Btn("Delete", BtnKind.Del)), Item("Disabled", Disabled(new Btn("Save"))), Item("Icon", new Btn("+")))));
        page.Children.Add(Row(Item("List action · left-aligned label", new Btn("host.get", BtnKind.Ghost) { Width = 220, LabelAlignment = HorizontalAlignment.Left })));
        page.Children.Add(Section("Fields and pickers", Row(Item("Empty", new Field("Search name or source") { Width = 220 }), Item("Filled", filled), Item("Invalid", invalid),
            Item("Host:port", new HostPortField("Host or IP") { Width = 200 })),
            Row(Item("Dropdown open: items keep their colours", dropdown), Item("Dropdown", plain), Item("Context menu", plainMenu), Item("Short action menu", Baseline.Menu.Panel(new[] { "Save local files", "Email result", "Notify about failure", "Write to database" }, _ => { }, () => { })), Item("MultiChoice", multi))));
        var suggested = new SuggestionField("Type a method prefix", new[] { "authentication.get", "authentication.update", "host.get", "item.get" }) { Width = 220, Text = "auth", Open = true };  // as it looks while its list is open
        var hints = Baseline.Menu.Panel(new[] { "authentication.get", "authentication.update" }, i => { }, () => { }, 0, 220, attached: true);
        var suggestionExample = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left }; suggestionExample.Children.Add(suggested); suggestionExample.Children.Add(hints);
        page.Children.Add(Section("Suggestions", suggestionExample));
        page.Children.Add(Section("Toggles and tabs", Row(Item("Switch off", new Switch(false)), Item("Switch on", new Switch(true)), Item("Tick", new Tick()), Item("Tick on", tickOn),
            Item("Filter chip", new FilterChip("DLMS")), Item("Chip on", chipOn), Item("Capsule", new Capsule("Add", Capsule.Plus))), Row(Item("TextTabs", tabs), Item("Filter: a chip per value, highlighted while on", chips))));
        page.Children.Add(Section("State switches", Row(Item("Not read", new StateToggle { Width = 112, Height = 28 }),
            Item("Off", new StateToggle { Width = 112, Height = 28, Unknown = false }), Item("On", new StateToggle { Width = 112, Height = 28, On = true }),
            Item("Pending", new StateToggle { Width = 112, Height = 28, On = true, Pending = true, ShowUndo = true }),
            Item("Read only", new StateLook { Width = 112, Height = 28, On = true }))));
        page.Children.Add(Section("Status", Row(Item("OK", new StatusBadge("OK", "ok")), Item("Warning", new StatusBadge("Retry at 14:30", "warn")), Item("Failed", new StatusBadge("Failed", "err")),
            Item("Running", new StatusBadge("Reading…", "busy")), Item("Waiting", new StatusBadge("Scheduled", "")), Item("Disabled", new StatusBadge("Disabled", "off")))));
        page.Children.Add(Section("Data", Row(Item("Table (Workbench.Table)", table), Item("Scroll viewer", scroll))));
        page.Children.Add(Section("Tags", Row(Item("Count", new Tag("3 matches", Success)), Item("New", new Tag("3 new", Success)), Item("Updated", new Tag("2 updated", Warn)), Item("Neutral", new Tag("37 up to date")), Item("Removable", new Tag("report-jan.csv", null, true)))));
        page.Children.Add(Section("Configuration table", new CompactTable(new[] { "Action", "When" }, new List<string[]> { new[] { "Save local files", "Success" }, new[] { "Notify about failure", "Failure" } }, color: (r, c) => c == 1 ? (r == 0 ? Success : Danger) : null, faded: r => r == 1)));
        page.Children.Add(Section("Background colour picker", new ColorGrid(Success, background: true)));
        page.Children.Add(Section("Progressive help", FormLayout.Disclosure("Expression help", () => Kit.Text("Run expressions use outcome and hasResult.", Second))));
        page.Children.Add(Section("Inline reference code", new InlineCode("{\n  \"method\": \"host.get\",\n  \"params\": { \"limit\": 5 }\n}")));
        var tipHost = new Btn("Hover me"); ToolTip.SetTip(tipHost, "Tooltips use Fill, Outline and Label text");
        var tipSample = new Border { Background = B(Surface2), BorderBrush = B(MenuEdge), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 5), Child = Kit.Text("Tooltips use Fill, Outline and Label text", Look.Text, 9) };
        var confirm = new Btn("Confirm dialog"); confirm.Click += async () => await Ask.Confirm(TopLevel.GetTopLevel(confirm) as Window, "Run the job now?\n\nIt reads every connection once.", "Run now");
        var delete = new Btn("Delete dialog", BtnKind.Del); delete.Click += async () => await Ask.ConfirmDelete(TopLevel.GetTopLevel(delete) as Window, "Delete “Daily yield”?\n\nIts history stays in the logs.", "Delete");
        page.Children.Add(Section("Tooltip and dialogs" + (interactive ? "" : " (dialog.png shows one)"), Row(Item("Tooltip (hover the button)", tipHost), Item("How it looks", tipSample), Item("Ask.Confirm", confirm), Item("Ask.ConfirmDelete", delete))));
        return page;
    }

    public static Window Window(bool interactive) => new Window
    {
        Title = "Baseline gallery",
        Width = 1180,
        Height = 900,
        Background = B(Bg),
        Icon = Look.Icon(Accent),
        Content = new ScrollViewer { Content = Page(interactive) },
    };

    static void Snap(Window window, string file, double width)
    {
        window.Show();
        var until = DateTime.UtcNow.AddMilliseconds(400);  // a dialog rises in (160 ms): the headless clock only moves when ticked
        while (DateTime.UtcNow < until) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(10); }
        for (int i = 0; i < 3; i++) { window.Measure(new Size(width, window.Height)); window.Arrange(new Rect(0, 0, width, window.Height)); Dispatcher.UIThread.RunJobs(); }
        var content = (Control)window.Content;
        content.Measure(new Size(width, double.PositiveInfinity));
        window.Height = Math.Ceiling(content.DesiredSize.Height) + 10;
        window.Measure(new Size(width, window.Height)); window.Arrange(new Rect(0, 0, width, window.Height)); Dispatcher.UIThread.RunJobs();
        using var image = new RenderTargetBitmap(new PixelSize((int)width, (int)window.Height)); image.Render(window);
        image.Save(file, PngBitmapEncoderOptions.Default);
        window.Close();
    }

    /// <summary>A real suggestion field, typed into: its list opens and the field keeps the open shape through focus restyles.</summary>
    static void CheckSuggestionShape()
    {
        var field = new SuggestionField("Method", new[] { "authentication.get", "authentication.update" }) { Width = 220 };
        var window = new Window { Width = 300, Height = 200, Content = new StackPanel { Children = { field } } };
        window.Show(); field.Box.Focus(); field.Box.Text = "auth"; Dispatcher.UIThread.RunJobs();
        static void Check(bool ok, string what) { if (!ok) throw new Exception("Suggestion field: " + what); }
        Check(field.SuggestionsOpen, "the list opens on a matching prefix");
        Check(field.BorderThickness.Bottom == 0 && field.CornerRadius.BottomLeft == 0, "an open field has no bottom line or bottom corners");
        Check(field.BorderBrush is ISolidColorBrush b && b.Color == MenuEdge, "an open, focused field takes the menu's edge colour");
        field.Box.Text = "zzz"; Dispatcher.UIThread.RunJobs();
        Check(!field.SuggestionsOpen && field.BorderThickness.Bottom == 1 && field.CornerRadius.BottomLeft == 4, "a closed field is whole again");
        window.Close();
    }

    public static void Render(string dir)
    {
        CheckSuggestionShape();
        System.IO.Directory.CreateDirectory(dir);
        Snap(new Window { Width = 1180, Height = 1500, Background = B(Bg), Content = Page(false) }, System.IO.Path.Combine(dir, "gallery.png"), 1180);
        var ask = new Ask("Delete “Daily yield”?\n\nIts history stays in the logs.", "Delete", true, 460, true) { SizeToContent = SizeToContent.Manual, Width = 460, Height = 200 };
        Snap(ask, System.IO.Path.Combine(dir, "dialog.png"), 460);
    }
}
