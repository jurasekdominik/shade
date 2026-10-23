// Shade gallery - the showcase: six numbered chapters, each a Masonry of blocks, built from Shade's own components.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Shade;
using static Shade.Look;

static class Showcase
{
    /// <summary>The chapters in order, in three groups (Foundations, Components, Patterns): file name, title, the line under
    /// it, and its content (built in the current mode).</summary>
    public static readonly (string group, string slug, string title, string note, Func<bool, Control> build)[] Chapters =
    {
        ("Foundations", "colors", "Colors", "Thirteen roles named by their job. Tints, hovers and text on colour follow rules, in both modes.", Colors),
        ("Foundations", "typography", "Typography", "Inter for the interface, Cascadia Code for values. Five sizes, nothing in between.", Typography),
        ("Foundations", "spacing-radii", "Spacing · Radii", "A 4 px grid, two corner sizes, two heights.", Spacing),
        ("Components", "actions-inputs", "Actions & inputs", "What people click and type into: one implementation each, every state shown.", i => Components(i, 0)),
        ("Components", "data-status", "Data & status", "What the app shows back: figures, rows, states and work under way.", i => Components(i, 1)),
        ("Components", "overlays-help", "Overlays & help", "What opens over a page, and the help beside a form.", i => Components(i, 2)),
        ("Patterns", "navigation", "Navigation", "The sidebar says where you are; tabs switch views of one thing; Back returns.", Navigation),
        ("Patterns", "responsive", "Responsive", "Blocks reflow into as many columns as fit; the sidebar collapses to its icons.", Responsive),
    };

    /// <summary>One chapter: its numbered heading, then its blocks.</summary>
    public static Control Chapter(int index, bool interactive)
    {
        var c = Chapters[index];
        var s = new StackPanel { Margin = new Thickness(0, 0, 0, 40) };
        s.Children.Add(FormLayout.Chapter(index + 1, c.title, c.note));
        s.Children.Add(c.build(interactive));
        return s;
    }

    static Masonry Blocks(double column, int max, params Control[] blocks)
    {
        var m = new Masonry { MinColumn = column, MaxColumns = max };
        foreach (var b in blocks) m.Children.Add(b);
        return m;
    }

    static StackPanel Stack(double spacing, params Control[] items) { var s = new StackPanel { Spacing = spacing }; foreach (var i in items) s.Children.Add(i); return s; }
    static WrapPanel Wrap(params Control[] items) { var w = new WrapPanel(); foreach (var i in items) { i.Margin = new Thickness(0, 0, 8, 8); w.Children.Add(i); } return w; }
    static StackPanel Captioned(string caption, Control c) => Stack(4, Kit.Text(caption, Faint, 8.25), c);

    // ---- 01 Colors --------------------------------------------------------------------------------

    static Control Swatch(string name, Tone c, bool derived = false) => new SwatchView(name, c, derived);

    /// <summary>A role's colour, its name and hex, and (derived) its Tint and Hover bars; painted again on a new mode or accent.</summary>
    sealed class SwatchView : StackPanel, IRestyle
    {
        readonly Tone tone;
        readonly Border tint, hover;
        readonly TextBlock hex = Kit.MonoText("", Faint, 10);
        public SwatchView(string name, Tone tone, bool derived)
        {
            this.tone = tone; Width = 92;
            Children.Add(new Border { Height = 40, Background = B(tone), BorderBrush = B(Look.Border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius) });
            if (derived)
            {
                tint = new Border { Width = 45, Height = 8, CornerRadius = new CornerRadius(2) }; hover = new Border { Width = 45, Height = 8, CornerRadius = new CornerRadius(2) };
                Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 3, 0, 0), Children = { tint, hover } });
            }
            var label = Kit.Text(name, Look.Text, 8.25, 500); label.Margin = new Thickness(0, 4, 0, 0);
            Children.Add(label); Children.Add(hex);
            Restyle();
        }
        public void Restyle()
        {
            hex.Text = HexOf(tone);
            if (tint != null) { tint.Background = B(Tint(tone)); hover.Background = B(Hover(tone)); }
        }
    }

    static Control Colors(bool interactive)
    {
        var text = new StackPanel { Spacing = 6 };
        foreach (var (name, c) in new[] { ("Text: main text", Look.Text), ("Text 2: labels and supporting text", Second), ("Text 3: placeholders, metadata, hints", Faint), ("Disabled", Disabled) })
            text.Children.Add(Kit.Text(name, c, 9.75));
        var accent = Stack(12, Wrap(Swatch("Accent", Accent, true), Swatch("Tint", Soft), Swatch("Accent text", AccentText)),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new Btn("On accent", BtnKind.Primary), new Tag("Selected", Accent) } });
        var pickers = Blocks(520, 2,
            FormLayout.Block("Accent picker", "The app's one colour: every control follows (Look.SetAccent).", Picker(interactive)),
            FormLayout.Block("Background picker", "Row and cell backgrounds: faded swatches are too low in contrast.", new ColorGrid(Hex("#83CEA5"), background: true)));
        pickers.Margin = new Thickness(0, 20, 0, 0);
        return Stack(0, Blocks(340, 3,
            FormLayout.Block("Surfaces", "From the page to the lines on it.", Wrap(Swatch("Background", Bg), Swatch("Raised", Surface), Swatch("Fill", Surface2), Swatch("Separator", Look.Border), Swatch("Outline", MenuEdge))),
            FormLayout.Block("Text", "Three steps of emphasis, and Disabled.", text),
            FormLayout.Block("Accent", "Primary action, selection, focus, the active tab. The small bars: Tint and Hover, by rule.", accent),
            FormLayout.Block("Status", "Fixed per mode; never the accent.", Wrap(Swatch("Success", Success, true), Swatch("Warning", Warn, true), Swatch("Danger", Danger, true), Swatch("Info", Override, true))),
            FormLayout.Block("Status in use", "A colour always comes with a word.", Stack(8, Wrap(new StatusBadge("OK", "ok"), new StatusBadge("Retry at 14:30", "warn"), new StatusBadge("Failed", "err")),
                Wrap(new Tag("3 new", Success), new Tag("2 updated", Warn), new Tag("1 failed", Danger), new Tag("Inherited", Override))))),
            pickers);
    }

    static Control Picker(bool interactive)
    {
        var grid = new ColorGrid(Accent);
        if (interactive) { grid.Picked += (c, _) => SetAccent(c); grid.PickedCurated += i => SetAccent(Accents[i].dark, Accents[i].light); }
        return grid;
    }

    // ---- 02 Typography -----------------------------------------------------------------------------

    static Control Typography(bool interactive)
    {
        var scale = new StackPanel { Spacing = 10 };
        foreach (var (name, px, weight, use) in new[] { ("Title", Title, 600, "Page and dialog titles"), ("Section", Look.Section, 600, "Block and card headings"),
                     ("Body", Body, 400, "Fields, table cells, running text"), ("Label", Look.Label, 500, "Field labels, table headers, buttons"), ("Caption", Caption, 400, "Help under a field, metadata") })
        {
            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(new TextBlock { Text = "Quarterly total 1 234,56 €", FontSize = px, FontWeight = Weight(weight), Foreground = B(Look.Text) });
            row.Children.Add(Kit.Text($"{name} · {px} px · {weight} · {use}", Faint, 8.25));
            scale.Children.Add(row);
        }
        var mono = Stack(8, Kit.MonoText("a3f9c21 · v1.4.0", Look.Text, 12), Kit.MonoText("192.168.1.20:8080 · 2026-10-14T09:30Z", Second, 12),
            new InlineCode("{ \"project\": \"website\", \"limit\": 5 }"));
        var weights = new StackPanel { Spacing = 4 };
        foreach (var w in new[] { 400, 500, 600, 700 }) weights.Children.Add(new TextBlock { Text = $"Inter {w} · Publish", FontSize = Body, FontWeight = Weight(w), Foreground = B(Look.Text) });
        var writing = Stack(6, Kit.Text("Sentence case everywhere: “Create project”, not “Create Project”.", Second, 9.75), Kit.Text("Actions are verbs and never end in dots: “Sync”.", Second, 9.75),
            Kit.Text("Work in progress keeps them: “Connecting…”.", Second, 9.75));
        foreach (TextBlock t in writing.Children) t.TextWrapping = TextWrapping.Wrap;
        return Blocks(340, 3,
            FormLayout.Block("Scale", "Five sizes in px; a size between them is rounded to the nearest.", scale),
            FormLayout.Block("Weights", "Inter, embedded: the same on every machine.", weights),
            FormLayout.Block("Mono", "Cascadia Code without ligatures: addresses, versions, code.", mono),
            FormLayout.Block("Writing", "How labels read.", writing));
    }

    // ---- 03 Components ----------------------------------------------------------------------------

    /// <summary>The components, in three chapters (part 0, 1, 2).</summary>
    static Control Components(bool interactive, int part)
    {
        Btn Off(Btn b) { b.IsEnabled = false; return b; }
        var invalid = new Field("Invalid value") { Text = "abc", Width = 140 }; invalid.Invalid = true;
        var fields = Stack(12, Kit.Labelled("Name", new Field("Search projects")), Kit.Labelled("Address", new HostPortField("Server or IP")),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new Field("Filled") { Text = "Acme Ltd.", Width = 140 }, invalid } });
        var multiline = Workbench.Lines("Optional report content"); multiline.Height = 72; fields.Children.Add(Kit.Labelled("Content", multiline));

        var methodTones = new Dictionary<string, Tone> { ["Design"] = new Tone(Color.Parse("#2FA874")), ["Research"] = new Tone(Color.Parse("#C99A1E")), ["Marketing"] = new Tone(Color.Parse("#3D8BD6")), ["Sales"] = new Tone(Color.Parse("#D2554A")) };
        Tone Method(string m) => methodTones[m];  // a category's colours: fixed, the same in both modes
        var methods = new[] { "Design", "Research", "Marketing", "Sales" };
        var open = new ChoiceButton { Width = 200, ItemColor = Method, HorizontalAlignment = HorizontalAlignment.Left }; open.SetItems(methods, "Design");
        open.ShowOpen = true;
        var list = Shade.Menu.Panel(methods, i => { }, () => { }, 0, 200, i => Method(methods[i]), attached: true); list.HorizontalAlignment = HorizontalAlignment.Left;
        var closed = new ChoiceButton { Width = 200, HorizontalAlignment = HorizontalAlignment.Left }; closed.SetItems(new[] { "Europe/Prague", "UTC" }, "Europe/Prague");
        var dropdowns = Stack(12, Captioned("Closed", closed), Captioned("Open: field and list are one shape", Stack(0, open, list)), Captioned("Several from a list", new MultiChoice(new[] { "Anna", "Ben", "Chloe" }, "Everyone") { Width = 200, HorizontalAlignment = HorizontalAlignment.Left }));

        var menu = Shade.Menu.Panel(new[] { "Edit", "Duplicate", Shade.Menu.Divider, "Delete task" }, i => { }, () => { }, -1, 200);
        menu.HorizontalAlignment = HorizontalAlignment.Left;
        var suggest = new SuggestionField("Type a name", new[] { "Annual report", "Annual budget", "Brand guide" }) { Width = 220, Text = "ann", Open = true, HorizontalAlignment = HorizontalAlignment.Left };
        var hints = Shade.Menu.Panel(new[] { "Annual budget", "Annual report" }, i => { }, () => { }, 0, 220, attached: true); hints.HorizontalAlignment = HorizontalAlignment.Left;

        var toggles = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,16,*"), RowDefinitions = new RowDefinitions("Auto,8,Auto,8,Auto,8,Auto") };
        void Line(int row, Control a, string what) { Grid.SetRow(a, row); a.VerticalAlignment = VerticalAlignment.Center; var t = Kit.Text(what, Second, 9.75); Grid.SetRow(t, row); Grid.SetColumn(t, 2); toggles.Children.Add(a); toggles.Children.Add(t); }
        Line(0, new Switch(true), "Switch: applies at once"); Line(2, new Switch(false), "Switch off");
        Line(4, new Tick { State = true }, "Tick: applies on Save"); Line(6, new Tick { State = null }, "Tick, mixed");
        var chipOn = new FilterChip("Engineering"); chipOn.Active = true;
        var states = Wrap(Captioned("Unknown", new StateToggle { Width = 104, Height = ControlHeight }), Captioned("Off", new StateToggle { Width = 104, Height = ControlHeight, Unknown = false }),
            Captioned("On", new StateToggle { Width = 104, Height = ControlHeight, On = true }), Captioned("Pending", new StateToggle { Width = 104, Height = ControlHeight, On = true, Pending = true, ShowUndo = true }));

        var table = Workbench.Table(new[] { "Date", "Orders", "Revenue (€)" }, Enumerable.Range(1, 5).Select(i => new[] { "2026-10-14", i.ToString(), (80 + i * 3.25).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) }).ToList());
        table.Height = 172;
        var compact = new CompactTable(new[] { "Action", "When" }, new List<string[]> { new[] { "Send a summary", "On success" }, new[] { "Alert the owner", "On failure" } }, color: (r, c) => c == 1 ? (r == 0 ? Success : Danger) : null, faded: r => r == 1);

        var tip = new Border { Background = B(Surface2), BorderBrush = B(MenuEdge), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius), Padding = new Thickness(8, 5), HorizontalAlignment = HorizontalAlignment.Left, Child = Kit.Text("Collapse navigation", Look.Text, 9) };
        var hover = new Btn("Hover me") { HorizontalAlignment = HorizontalAlignment.Left }; ToolTip.SetTip(hover, "Tooltips: Fill, Outline and Label text");

        if (part == 0) return Blocks(340, 3,
            FormLayout.Block("Buttons", "One Primary per area; Danger for deletes and outside-world actions.", Stack(12,
                Wrap(new Btn("Publish", BtnKind.Primary), new Btn("Save"), new Btn("Cancel", BtnKind.Ghost), new Btn("Delete", BtnKind.Del)),
                Wrap(Off(new Btn("Save")), new Btn("+"), new Btn("Add", BtnKind.Ghost, icon: Icons.Plus), new Capsule("Add", Capsule.Plus)))),
            FormLayout.Block("Fields", "Raised fill; the frame changes on hover, focus and error.", fields),
            FormLayout.Block("Search", "A magnifier, × once typed (Esc clears it too), an optional count.", Searches()),
            FormLayout.Block("Dropdowns", "Items keep their colours in the list.", dropdowns),
            FormLayout.Block("Suggestions", "Up to eight matches under the field, as one shape with it.", Stack(0, suggest, hints)),
            FormLayout.Block("Switches and ticks", "A switch acts now; a tick is a choice for Save.", toggles),
            FormLayout.Block("State switches", "Field-sized ON/OFF for a setting on another system.", states));
        if (part == 1) return Stack(20, FormLayout.Block("Summary cards", "Monitoring at a glance; a card that filters gets an accent frame (click Incidents).", Cards()), Blocks(340, 3,
            FormLayout.Block("Data table", "28 px rows, numbers right, a tooltip for cut text.", table),
            FormLayout.Block("Configuration table", "Natural height; a disabled row fades.", compact),
            FormLayout.Block("Status and tags", "A dot and a word; a tag shows a value.", Stack(10, Wrap(new StatusBadge("OK", "ok"), new StatusBadge("Syncing…", "busy"), new StatusBadge("Scheduled", ""), new StatusBadge("Disabled", "off")),
                Wrap(new Tag("3 new", Success), new Tag("37 up to date"), new Tag("brief-v2.pdf", null, true)))),
            FormLayout.Block("Filter chips", "A chip per value; on, it narrows the list.", Wrap(new FilterChip("Design"), chipOn, new FilterChip("Failed", Danger) { Active = true })),
            FormLayout.Block("Progress", "Known, unknown length, a countdown; the status bar has a small one.", Progresses())));
        return Blocks(340, 3,
            FormLayout.Block("Menus", "The same rows as a dropdown, as a free card; danger last, below a line.", menu),
            FormLayout.Block("Tray menu", "The tray icon's right-click menu: every item at its natural height.", TrayCard()),
            FormLayout.Block("Tooltip", "Hover the button in the gallery; this is how it looks.", Stack(10, hover, tip)),
            FormLayout.Block("Dialog", "8 px corners, Cancel then the action, named for what it does.", DialogPreview()),
            FormLayout.Block("Reference card", "Documentation beside a form; a click on an example uses it.", Reference()),
            FormLayout.Block("Progressive help", "Reference help opens on demand.", FormLayout.Disclosure("Formatting help", () => Kit.Text("Use *italic*, **bold** and `code`.", Second))),
            FormLayout.Block("Empty state", "What's missing, why, and the action that fixes it.", Kit.Empty("No projects yet", "A project groups tasks, files and people.", "New project", () => { })));
    }

    static Control Cards()
    {
        var cards = new SummaryCards();
        var items = new List<SummaryCards.Item>
        {
            new() { Key = "services", Title = "Services", Value = "11/12", Unit = "healthy", Share = 11 / 12.0, Tone = Warn, Line = "Degraded: billing-api", LineTone = Warn, Corner = "checked 14:02" },
            new() { Key = "uptime", Title = "Uptime", Value = "99.94 %", Unit = "30 d", Share = 0.9994, Tone = Success, Line = "SLO 99.9 % · met", Corner = "30 days" },
            new() { Key = "latency", Title = "Latency p95", Value = "182 ms", Trend = new double[] { 140, 150, 146, 160, 158, 171, 166, 182 }, Line = "▲ 14 % on the last hour", LineTone = Warn, Corner = "api-gw" },
            new() { Key = "incidents", Title = "Incidents", Value = "2", Unit = "open", Tone = DangerHi, Line = "Oldest 42 min · api-gw", LineTone = Warn, Corner = "P1 · P2", Active = true },
        };
        cards.Items = items;
        cards.Picked += key => cards.Items = cards.Items.Select(it => it with { Active = it.Key == key && !it.Active }).ToList();
        var detailed = new SummaryCards { Detailed = true };
        var more = new List<SummaryCards.Item>
        {
            new() { Key = "checks", Title = "Checks", Sub = "every 5 minutes", Value = "11/12", Unit = "OK", Share = 11 / 12.0, Tone = Warn, Line = "Problems: billing-api", LineTone = Warn, Corner = "next 14:05" },
            new() { Key = "backups", Title = "Backups", Sub = "nightly and weekly", Value = "6/6", Unit = "OK", Share = 1, Tone = Success, Line = "All fine", Corner = "next 22:30", Active = true },
            new() { Key = "problems", Title = "Problems", Sub = "failed or with warnings", Value = "1", Unit = "service", Tone = Warn, Line = "billing-api", LineTone = Warn },
            new() { Key = "next", Title = "Next run", Sub = "Nightly backup", Value = "22:30", Unit = "today", Line = "Every day at 22:30", Corner = "Daily 22:30" },
        };
        detailed.Items = more;
        detailed.Picked += key => detailed.Items = detailed.Items.Select(it => it with { Active = it.Key == key && !it.Active }).ToList();
        return Stack(12, Kit.Text("Compact · 84 px", Faint, 8.25), cards, Kit.Text("Detailed · 112 px, with a subtitle", Faint, 8.25), detailed);
    }

    static Control Searches()
    {
        var typed = new SearchField("Search projects") { Text = "web", Count = "3 of 12" };
        return Stack(12, new SearchField("Search projects"), typed);
    }

    static Control Progresses() => Stack(12,
        new Progress { Label = "Uploading 3 files", Detail = "64 %", Value = 0.64 },
        new Progress { Label = "Preparing the export…", Value = null },
        new Progress { Label = "Next refresh in 12 s", Value = 0.4, Countdown = true });

    static Control Reference() => new ReferenceCard("Syntax")
        .Help("Full syntax", () => { }, "Every field, character and shortcut")
        .Columns(new[] { ("minute", "0–59"), ("hour", "0–23"), ("day", "1–31"), ("month", "1–12"), ("weekday", "0–6, SUN–SAT") },
                 new[] { ("*", "any"), (",", "list 1,15"), ("-", "range 1-5"), ("/", "step */15") })
        .Codes("@daily", "@hourly", "@weekly", "@monthly")
        .Examples(new[] { "0 22 * * 1-5", "*/10 8-17 * * 1-5", "0 0 1 * *" }, _ => { })
        .Note("Days of the month or weekdays, not both.");

    static Control TrayCard()
    {
        var menu = Shade.Menu.Panel(new[] { "Open Shade", "Projects", "Tasks", Shade.Menu.Divider, "Pause sync", "Quit" }, _ => { }, () => { }, 1, 220, scrollable: false);
        menu.HorizontalAlignment = HorizontalAlignment.Left;
        return Stack(8, menu, Kit.Text("TrayMenu.Show(x, y, items, picked): by the pointer, kept on screen.", Faint, 8.25));
    }

    static Control Tabs() { var t = new TextTabs("Summary", "Tasks", "Files"); t.SetActive(1); return t; }

    /// <summary>A real confirmation dialog's content, shown in its frame.</summary>
    static Control DialogPreview()
    {
        var ask = new Ask("Delete “Website refresh”?\n\nIts files stay in the library.", "Delete project", true, 330, true);
        var body = (Control)ask.Content; ask.Content = null;
        return new Border { Background = B(Bg), BorderBrush = B(Look.Border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(CardRadius), Child = body, HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = true };
    }

    // ---- 04 Spacing · Radii ------------------------------------------------------------------------

    static Control Spacing(bool interactive)
    {
        var spaces = new StackPanel { Spacing = 8 };
        foreach (var s in Spaces)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new Border { Width = 48, Child = Kit.MonoText(s + " px", Second, 11) });
            row.Children.Add(new Border { Width = s * 4, Height = 12, Background = B(Soft), BorderBrush = B(SoftLine), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) });
            spaces.Children.Add(row);
        }
        var uses = new StackPanel { Spacing = 4 };
        foreach (var (what, px) in new[] { ("Label and its field", "4"), ("Buttons in a row", "8"), ("Fields stacked", "12"), ("Page edge, fields side by side", "16"), ("Dialog edge", "24"), ("Columns of blocks", "32") })
        {
            var d = new DockPanel(); var v = Kit.MonoText(px, Faint, 11); DockPanel.SetDock(v, Dock.Right); d.Children.Add(v); d.Children.Add(Kit.Text(what, Second, 9.75)); uses.Children.Add(d);
        }
        Control Corner(string name, double r) => Stack(6, new Border { Width = 72, Height = 48, Background = B(Surface2), BorderBrush = B(MenuEdge), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(r) },
            Kit.Text(name, Look.Text, 8.25, 500), Kit.MonoText(r + " px", Faint, 10));
        var heights = Stack(10, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new Field("28 px") { Width = 120 }, new Btn("28 px", BtnKind.Primary) } },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new FilterChip("24 px chip"), new Tag("24 px tag") } });
        var lines = Stack(10, new Border { Height = 1, Background = B(Look.Border), Width = 240, HorizontalAlignment = HorizontalAlignment.Left },
            Kit.Text("1 px Separator: every resting line", Second, 9.75),
            new Border { Width = 160, Height = ControlHeight, BorderBrush = B(Accent), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius), Background = B(Surface), HorizontalAlignment = HorizontalAlignment.Left },
            Kit.Text("1 px Accent: keyboard focus; nothing glows", Second, 9.75));
        return Blocks(340, 3,
            FormLayout.Block("Spacing scale", "Only these six values.", spaces),
            FormLayout.Block("Where each goes", "Compact inside a group, generous between groups.", uses),
            FormLayout.Block("Corners", "No pills.", Wrap(Corner("Rows, sidebar", 0), Corner("Controls, menus", Radius), Corner("Dialogs, cards", CardRadius))),
            FormLayout.Block("Heights", "Every field, dropdown, button and row; chips and tags.", heights),
            FormLayout.Block("Lines and focus", "No shadows on controls.", lines));
    }

    // ---- 05 Responsive -----------------------------------------------------------------------------

    static Control Responsive(bool interactive)
    {
        // the same page's blocks at three widths: the Masonry picks the column count (MinColumn 140 here, to fit the preview)
        Control Sample(double width)
        {
            var m = new Masonry { MinColumn = 140, MaxColumns = 3, Gap = 16, RowGap = 10, Width = width };
            foreach (var (t, h) in new[] { ("General", 56.0), ("Email", 84.0), ("Notifications", 36.0), ("Storage", 64.0), ("Appearance", 46.0) })
                m.Children.Add(FormLayout.Block(t, null, new Border { Height = h, Background = B(Surface2), CornerRadius = new CornerRadius(Radius) }));
            return new Border { Padding = new Thickness(16), Background = B(Bg), BorderBrush = B(Look.Border), BorderThickness = new Thickness(1), Child = m, HorizontalAlignment = HorizontalAlignment.Left };
        }
        var rules = new StackPanel { Spacing = 6 };
        foreach (var r in new[] { "A Masonry fits as many columns as reach MinColumn, up to MaxColumns; each block goes to the shortest column.",
                     "Page headers keep their title left and actions right; the hint wraps.", "Tables scroll inside their area; headers stay.",
                     "The sidebar is 180 px, 54 px collapsed (icons only, tooltips for names).", "Desktop only: the window's minimum is the content's, nothing is hidden." })
        { var t = Kit.Text("• " + r, Second, 9.75); t.TextWrapping = TextWrapping.Wrap; rules.Children.Add(t); }
        return Stack(20, Blocks(520, 2,
                FormLayout.Block("Wide: three columns", "A 520 px page.", Sample(520)),
                FormLayout.Block("Medium: two columns", "A 300 px page.", Sample(300))),
            Blocks(340, 3,
                FormLayout.Block("Narrow: one column", "A 200 px page.", Sample(200)),
                FormLayout.Block("Sidebar collapsed", "Icons only; a click on the mark opens it.", SidebarSample(true, 420)),
                FormLayout.Block("Rules", "How a page adapts to the window.", rules)));
    }

    // ---- 06 Navigation -----------------------------------------------------------------------------

    static Control SidebarSample(bool collapsed, double height)
    {
        var nav = new[]
        {
            new NavEntry("Projects", null, Icons.Folder, new NavEntry("Overview", "projects/overview"), new NavEntry("Activity", "projects/activity")),
            new NavEntry("Tasks", "tasks", Icons.ListChecks),
            new NavEntry("Library", "library", Icons.Library),
            new NavEntry("Settings", "settings", Icons.Settings),
        };
        var sidebar = new Sidebar("shade", null, nav) { Collapsed = collapsed };
        sidebar.Select("projects/overview"); sidebar.History.Update(true, false);
        sidebar.Navigate += sidebar.Select;
        return new Border { Height = height, HorizontalAlignment = HorizontalAlignment.Left, BorderBrush = B(Look.Border), BorderThickness = new Thickness(1), Child = sidebar };
    }

    static Control Navigation(bool interactive)
    {
        var sidebar = (Border)SidebarSample(false, 400);
        var nav = (Sidebar)sidebar.Child; sidebar.Child = null;
        var status = new StatusBar(); status.Show("Warning · Sync retries at 14:30", Warn, loud: true); status.Count("2 people online", true); status.Busy("Uploading 3 files · 64 %", 0.64);
        var page = new StackPanel { Margin = new Thickness(16) };
        page.Children.Add(FormLayout.Header("Overview", "Everything in this project at a glance.", new Btn("Export", BtnKind.Ghost), new Btn("Publish", BtnKind.Primary)));
        var tabs = Tabs(); tabs.Margin = new Thickness(0, 8, 0, 0); page.Children.Add(tabs);
        var shell = new Border { Height = 400, BorderBrush = B(Look.Border), BorderThickness = new Thickness(1), Background = B(Bg), Child = Shell.Layout(nav, page, status) };
        var history = new NavigationButtons(); history.Update(true, false);
        var sub = FormLayout.SubHeader("Projects", () => { }, "Website refresh", "4 tasks · due Friday", new Btn("Save", BtnKind.Primary));
        return Stack(20, FormLayout.Block("App shell", "Sidebar, page, status bar (Shell.Layout). The sidebar's mark collapses it.", shell),
            Blocks(340, 3,
                FormLayout.Block("Back and Forward", "In the sidebar footer; mouse side buttons and Alt+←/→ use the same history.", history),
                FormLayout.Block("Subpage header", "A page opened from another names its parent and goes back to it.", sub),
                FormLayout.Block("Tabs", "Views of one thing; the line slides to the active tab. Never pages.", Tabs())));
    }
}
