// Shade gallery - the showcase: six numbered chapters, each a Masonry of blocks, built from Shade's own components.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Shade;
using static Shade.Look;

static class Showcase
{
    sealed record Employee(int Id, string Name, string Title) { public string SearchText => Title + " " + Name; }
    internal static SearchChoice SearchableEmployees() => new()
    {
        ItemsSource = new[] { new Employee(1, "Jan Novák", "Mgr. Research"), new Employee(2, "Petra Šílá", "Bc. HR"), new Employee(3, "Josef Novotný", "Ing. Engineering"), new Employee(4, "Žofie Malá", "Support"), new Employee(5, "Jan Novák", "Ing. Design") },
        DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Employee.Name)),
        SearchMemberBinding = new Avalonia.Data.Binding(nameof(Employee.SearchText)),
        SelectedValueBinding = new Avalonia.Data.Binding(nameof(Employee.Id)),
        PlaceholderText = "Choose employee",
        SearchPlaceholderText = "Title, first or last name",
        Width = M(330),
        HorizontalAlignment = HorizontalAlignment.Left
    };

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
        var m = new Masonry { MinColumn = M(column), MaxColumns = max, Gap = M(32), RowGap = M(20) };
        foreach (var b in blocks) m.Children.Add(b);
        return m;
    }

    static StackPanel Stack(double spacing, params Control[] items) { var s = new StackPanel { Spacing = spacing }; foreach (var i in items) s.Children.Add(i); return s; }
    static T Left<T>(T c) where T : Control { c.HorizontalAlignment = HorizontalAlignment.Left; return c; }
    static WrapPanel Wrap(params Control[] items) { var w = new WrapPanel(); foreach (var i in items) { i.Margin = new Thickness(0, 0, 8, 8); w.Children.Add(i); } return w; }
    static StackPanel Captioned(string caption, Control c) => Stack(4, Build.Text(caption, Text3, TextSize.Caption), c);

    // ---- 01 Colors --------------------------------------------------------------------------------

    static Control Swatch(string name, Tone c, bool derived = false) => new SwatchView(name, c, derived);

    /// <summary>A role's colour, its name and hex, and (derived) its Tint and Hover bars; painted again on a new mode or accent.</summary>
    sealed class SwatchView : StackPanel, IRestyle
    {
        readonly Tone tone;
        readonly Border tint, hover;
        readonly TextBlock hex = Build.MonoText("", Text3, 10);
        public SwatchView(string name, Tone tone, bool derived)
        {
            this.tone = tone; Width = 92;
            Children.Add(new Border { Height = 40, Background = B(tone), BorderBrush = B(Look.Separator), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius) });
            if (derived)
            {
                tint = new Border { Width = 45, Height = 8, CornerRadius = new CornerRadius(2) }; hover = new Border { Width = 45, Height = 8, CornerRadius = new CornerRadius(2) };
                Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 3, 0, 0), Children = { tint, hover } });
            }
            var label = Build.Text(name, Look.Text, TextSize.Caption, 500); label.Margin = new Thickness(0, 4, 0, 0);
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
        foreach (var (name, c) in new[] { ("Text: main text", Look.Text), ("Text 2: labels and supporting text", Text2), ("Text 3: placeholders, metadata, hints", Text3), ("Disabled", Disabled) })
            text.Children.Add(Build.Text(name, c, TextSize.Body));
        var accent = Stack(12, Wrap(Swatch("Accent", Accent, true), Swatch("Tint", AccentTint), Swatch("Accent text", AccentText)),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new ActionButton("On accent", ButtonKind.Primary), new Tag("Selected", Accent) } });
        var pickers = Blocks(520, 2,
            FormLayout.Block("Accent picker", "A settings row: the accent, a Custom colour, the curated accents. The field opens the colour helper.", new AccentPicker { Width = 440, HorizontalAlignment = HorizontalAlignment.Left }),
            FormLayout.Block("Colour helper", "The app's one colour: every control follows (Look.SetAccent).", Picker(interactive)),
            FormLayout.Block("Background picker", "Row and cell backgrounds: faded swatches are too low in contrast.", new ColorGrid(Hex("#83CEA5"), background: true)));
        pickers.Margin = new Thickness(0, 20, 0, 0);
        return Stack(0, Blocks(340, 3,
            FormLayout.Block("Surfaces", "From the page to the lines on it.", Wrap(Swatch("Background", Look.Background), Swatch("Raised", Raised), Swatch("Fill", Fill), Swatch("Separator", Look.Separator), Swatch("Outline", Outline))),
            FormLayout.Block("Text", "Three steps of emphasis, and Disabled.", text),
            FormLayout.Block("Accent", "Primary action, selection, focus, the active tab. The small bars: Tint and Hover, by rule.", accent),
            FormLayout.Block("Status", "Fixed per mode; never the accent.", Wrap(Swatch("Success", Success, true), Swatch("Warning", Warn, true), Swatch("Danger", Danger, true), Swatch("Info", Info, true))),
            FormLayout.Block("Status in use", "A colour always comes with a word.", Stack(8, Wrap(new StatusBadge("OK", "ok"), new StatusBadge("Retry at 14:30", "warn"), new StatusBadge("Failed", "err")),
                Wrap(new Tag("3 new", Success), new Tag("2 updated", Warn), new Tag("1 failed", Danger), new Tag("Inherited", Info))))),
            pickers);
    }

    /// <summary>The table designer, and the same one mid-drag (Meter ID on its way between Time and Price).</summary>
    static Control Designers(bool interactive)
    {
        var heads = new List<TableDesigner.Column> { new("Time"), new("Price", true), new("Meter ID"), new("Status") };
        var rows = new[] { new[] { "2026-10-21", "124.5", "000123", "OK" }, new[] { "2026-10-21", "126.7", "000124", "OK" }, new[] { "2026-10-22", "130", "000125", "Late" } };
        TableDesigner Make() => new TableDesigner { Columns = heads.ToList(), Rows = rows, Selected = 0, HorizontalAlignment = HorizontalAlignment.Left };
        var live = Make();
        if (interactive)
        {
            live.Moved += (from, to) => { var c = heads[from]; heads.RemoveAt(from); heads.Insert(to, c); foreach (var r in rows) { var v = r[from]; var l = r.ToList(); l.RemoveAt(from); l.Insert(to, v); l.CopyTo(r); } live.Columns = heads.ToList(); live.Rows = rows; };
            live.RemoveAsked += i => { heads.RemoveAt(i); live.Columns = heads.ToList(); };
        }
        var dragging = Make(); dragging.Margin = new Thickness(0, 0, 0, 8);  // room for the line's lower dot
        dragging.AttachedToVisualTree += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(() => dragging.ShowDrop(1, 2, "Meter ID", new Point(122, 50)), Avalonia.Threading.DispatcherPriority.Loaded);
        return Stack(12, live, Captioned("While dragging", dragging));
    }

    /// <summary>A date field, and its month as it shows open (the 28th picked, the 21st today); a range, and its month with the
    /// days between its ends coloured.</summary>
    static Control Dates()
    {
        var field = new DateField { Value = new DateTime(2026, 10, 28) };
        static Border Card(MonthGrid grid) => new Border
        {
            Background = B(Raised),
            BorderBrush = B(Outline),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(CardRadius),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = grid
        };
        var grey = new DateField { Value = new DateTime(2026, 10, 28), Weekends = WeekendMark.Grey };
        return Stack(12, Stack(4, field, Card(new MonthGrid(field, field.Value!.Value))), Captioned("Weekends on a grey band (WeekendMark.Grey)", Card(new MonthGrid(grey, grey.Value!.Value))));
    }

    static Control Ranges()
    {
        var range = new DateRangeField { Start = new DateTime(2026, 10, 12), End = new DateTime(2026, 10, 23) };
        var grid = new MonthGrid(new DateTime(2026, 10, 23), _ => true, DayOfWeek.Monday, WeekendMark.Red, range: true) { Start = range.Start, End = range.End };
        var card = new Border { Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(CardRadius), HorizontalAlignment = HorizontalAlignment.Left, Child = grid };
        return Stack(12, Stack(4, range, card), Captioned("Empty: placeholders show the blocks", new DateField()));
    }

    static Control Picker(bool interactive)
    {
        var grid = new ColorGrid(Accent);
        grid.Bind(ColorGrid.ValueProperty, new Avalonia.Data.Binding("Color") { Source = B(Accent), Mode = Avalonia.Data.BindingMode.OneWay });
        if (interactive) { grid.Picked += (c, _) => SetAccent(c); grid.PickedCurated += i => SetAccent(Accents[i].dark, Accents[i].light); }
        return grid;
    }

    // ---- 02 Typography -----------------------------------------------------------------------------

    static Control Typography(bool interactive)
    {
        var scale = new StackPanel { Spacing = 10 };
        foreach (var (name, px, weight, use) in new[] { ("Title", TextSize.Title, 600, "Page and dialog titles"), ("Section", TextSize.Section, 600, "Block and card headings"),
                     ("Body", TextSize.Body, 400, "Fields, table cells, running text"), ("Label", TextSize.Label, 500, "Field labels, table headers, buttons"), ("Caption", TextSize.Caption, 400, "Help under a field, metadata") })
        {
            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(new TextBlock { Text = "Quarterly total 1 234,56 €", FontSize = px, FontWeight = Weight(weight), Foreground = B(Look.Text) });
            row.Children.Add(Build.Text($"{name} · {px} px · {weight} · {use}", Text3, TextSize.Caption));
            scale.Children.Add(row);
        }
        var mono = Stack(8, Build.MonoText("a3f9c21 · v1.4.0", Look.Text, 12), Build.MonoText("192.168.1.20:8080 · 2026-10-14T09:30Z", Text2, 12),
            new InlineCode("{ \"project\": \"website\", \"limit\": 5 }"));
        var weights = new StackPanel { Spacing = 4 };
        foreach (var w in new[] { 400, 500, 600, 700 }) weights.Children.Add(new TextBlock { Text = $"Inter {w} · Publish", FontSize = TextSize.Body, FontWeight = Weight(w), Foreground = B(Look.Text) });
        var writing = Stack(6, Build.Text("Sentence case everywhere: “Create project”, not “Create Project”.", Text2, TextSize.Body), Build.Text("Actions are verbs and never end in dots: “Sync”.", Text2, TextSize.Body),
            Build.Text("Work in progress keeps them: “Connecting…”.", Text2, TextSize.Body));
        foreach (TextBlock t in writing.Children) t.TextWrapping = TextWrapping.Wrap;
        return Blocks(340, 3,
            FormLayout.Block("Scale", "Five sizes in px; every text takes one.", scale),
            FormLayout.Block("Weights", "Inter, embedded: the same on every machine.", weights),
            FormLayout.Block("Mono", "Cascadia Code without ligatures: addresses, versions, code.", mono),
            FormLayout.Block("Writing", "How labels read.", writing));
    }

    // ---- 03 Components ----------------------------------------------------------------------------

    /// <summary>The components, in three chapters (part 0, 1, 2).</summary>
    static Control Components(bool interactive, int part)
    {
        ActionButton Off(ActionButton b) { b.IsEnabled = false; return b; }
        var invalid = new Field("Invalid value") { Text = "abc", Width = 140 }; invalid.Invalid = true;
        var fields = Stack(12, Build.Labelled("Name", new Field("Search projects")), Build.Labelled("Address", new HostPortField("Server or IP")),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new Field("Filled") { Text = "Acme Ltd.", Width = 140 }, invalid } });
        var multiline = Workbench.Lines("Optional report content"); multiline.Height = 72; fields.Children.Add(Build.Labelled("Content", multiline));
        fields.Children.Add(Build.Labelled("Required name", new Field { Error = "Enter a name", Width = 220, HorizontalAlignment = HorizontalAlignment.Left }));

        var methodTones = new Dictionary<string, Tone> { ["Design"] = new Tone(Color.Parse("#2FA874")), ["Research"] = new Tone(Color.Parse("#C99A1E")), ["Marketing"] = new Tone(Color.Parse("#3D8BD6")), ["Sales"] = new Tone(Color.Parse("#D2554A")) };
        Tone Method(string m) => methodTones[m];  // a category's colours: fixed, the same in both modes
        var methods = new[] { "Design", "Research", "Marketing", "Sales" };
        var open = new ChoiceButton { Width = 200, ItemColor = Method, HorizontalAlignment = HorizontalAlignment.Left }; open.SetItems(methods, "Design");
        open.ShowOpen = true;
        var list = Shade.Menu.Panel(methods, i => { }, () => { }, 0, 200, i => Method(methods[i]), attached: true); list.HorizontalAlignment = HorizontalAlignment.Left;
        var closed = new ChoiceButton { Width = 200, HorizontalAlignment = HorizontalAlignment.Left }; closed.SetItems(new[] { "Europe/Prague", "Europe/London", "America/New York" }, "Europe/Prague");
        var dropdowns = Stack(12, Captioned("Closed", closed), Captioned("Open: field and list are one shape", Stack(0, open, list)), Captioned("Several from a list", new MultiChoice { Items = new[] { "Anna", "Ben", "Chloe" }, EmptyText = "Everyone", Width = 200, HorizontalAlignment = HorizontalAlignment.Left }));

        var searchable = SearchableEmployees();
        dropdowns.Children.Add(Captioned("Search all options", searchable));
        var searchDisabled = SearchableEmployees(); searchDisabled.SelectedIndex = 1; searchDisabled.IsEnabled = false;
        dropdowns.Children.Add(Captioned("Searchable, disabled", searchDisabled));
        var menu = Shade.Menu.Panel(new[] { "Edit", "Duplicate", Shade.Menu.Divider, "Delete task" }, i => { }, () => { }, -1, 200);
        menu.HorizontalAlignment = HorizontalAlignment.Left;
        var records = Shade.Menu.Panel(new[] { new MenuEntry("Duplicate") { Icon = Icons.Copy, Shortcut = "Ctrl+D" }, new MenuEntry("Export") { Icon = Icons.Download, IsEnabled = false }, new MenuEntry("Compact view") { IsChecked = true }, new MenuEntry("Delete task") { Icon = Icons.Trash2, IsDanger = true } }, _ => { }, () => { }, 220);
        records.HorizontalAlignment = HorizontalAlignment.Left;
        var suggest = new SuggestionField("Type a name", new[] { "Annual report", "Annual budget", "Brand guide" }) { Width = 220, Text = "ann", Open = true, HorizontalAlignment = HorizontalAlignment.Left };
        var hints = Shade.Menu.Panel(new[] { "Annual budget", "Annual report" }, i => { }, () => { }, 0, 220, attached: true); hints.HorizontalAlignment = HorizontalAlignment.Left;

        var toggles = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,16,*"), RowDefinitions = new RowDefinitions("Auto,8,Auto,8,Auto,8,Auto") };
        void Line(int row, Control a, string what) { Grid.SetRow(a, row); a.VerticalAlignment = VerticalAlignment.Center; var t = Build.Text(what, Text2, TextSize.Body); Grid.SetRow(t, row); Grid.SetColumn(t, 2); toggles.Children.Add(a); toggles.Children.Add(t); }
        Line(0, new Switch(true), "Switch: applies at once"); Line(2, new Switch(false), "Switch off");
        Line(4, new Tick { IsChecked = true }, "Tick: applies on Save"); Line(6, new Tick { IsChecked = null }, "Tick, mixed");
        var chipOn = new FilterChip("Engineering"); chipOn.IsChecked = true;
        var states = Wrap(Captioned("Unknown", new StateToggle { Width = M(104), Height = ControlHeight }), Captioned("Off", new StateToggle { Width = M(104), Height = ControlHeight, IsChecked = false }),
            Captioned("On", new StateToggle { Width = M(104), Height = ControlHeight, IsChecked = true }), Captioned("Pending", new StateToggle { Width = M(104), Height = ControlHeight, IsChecked = true, Pending = true, ShowUndo = true }),
            Captioned("Shown, not changed here", new StateLook { Width = M(104), Height = ControlHeight, On = true }));
        var pairs = new PairTable("Header", "Value"); pairs.Set(new Dictionary<string, string> { ["Accept"] = "application/json", ["X-Team"] = "Design" });
        var drafts = new PairTable { CaseSensitive = false, Entries = new System.Collections.ObjectModel.ObservableCollection<PairTable.Entry> { new("Accept", "application/json"), new("accept", "text/plain") } };
        var lists = Stack(12, Build.Labelled("Recipients", new ListField("anna@example.com, ben@example.com", "Add a recipient")), Build.Labelled("Headers", pairs), Build.Labelled("Duplicate draft (kept for editing)", drafts));
        var log = new LogText { Height = 96 };
        log.Add("14:02:11  Sync started", Text2); log.Add("14:02:12  3 files uploaded", Success); log.Add("14:02:13  Retrying brief-v2.pdf", Warn); log.Add("14:02:15  brief-v2.pdf failed: the server refused it", Danger);

        var table = Workbench.Table(new[] { "Date", "Orders", "Revenue (€)" }, Enumerable.Range(1, 5).Select(i => new[] { "2026-10-14", i.ToString(), (80 + i * 3.25).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) }).ToList());
        table.Height = 172;
        var compact = new CompactTable(new[] { "Action", "When" }, new List<string[]> { new[] { "Send a summary", "On success" }, new[] { "Alert the owner", "On failure" } }, color: (r, c) => c == 1 ? (r == 0 ? Success : Danger) : null, faded: r => r == 1);

        var tip = new Border { Background = B(Fill), BorderBrush = B(Outline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius), Padding = new Thickness(8, 5), HorizontalAlignment = HorizontalAlignment.Left, Child = Build.Text("Collapse navigation", Look.Text, TextSize.Label) };
        var hover = new ActionButton("Hover me") { HorizontalAlignment = HorizontalAlignment.Left }; ToolTip.SetTip(hover, "Tooltips: Fill, Outline and Label text");

        if (part == 0) return Blocks(340, 3,
            FormLayout.Block("Buttons", "One Primary per area; Danger for deletes and outside-world actions.", Stack(12,
                Wrap(new ActionButton("Publish", ButtonKind.Primary), new ActionButton("Save"), new ActionButton("Cancel", ButtonKind.Ghost), new ActionButton("Delete", ButtonKind.Danger)),
                Wrap(Off(new ActionButton("Save")), new ActionButton("+"), new ActionButton("Add", ButtonKind.Ghost, icon: Icons.Plus), new Capsule("Add", Capsule.Plus)),
                Wrap(new IconButton(IconButton.Export), new IconButton(IconButton.Undo)))),
            FormLayout.Block("Fields", "Raised fill; the frame changes on hover, focus and error.", fields),
            FormLayout.Block("Search", "A magnifier, × once typed (Esc clears it too), an optional count.", Searches()),
            FormLayout.Block("Dropdowns", "Items keep their colours in the list.", dropdowns),
            FormLayout.Block("Date", "Year, month and day are blocks to type in; a click anywhere opens the month: today ringed, the day filled, weekends marked.", Dates()),
            FormLayout.Block("Date range", "A start and an end in one field (DateRangeField); the month colours the days between them.", Ranges()),
            FormLayout.Block("Suggestions", "Up to eight matches under the field, as one shape with it.", Stack(0, suggest, hints)),
            FormLayout.Block("Mode switch", "Light, Dark or System (follows the computer's setting as it changes); a pick switches at once.", new ModeSwitch()),
            FormLayout.Block("Switches and ticks", "A switch acts now; a tick is a choice for Save.", toggles),
            FormLayout.Block("Several from a short list", "Tick boxes in columns; the order of ticking is kept.", new TickGrid(new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" }, new[] { "Monday", "Thursday" }, 110)),
            FormLayout.Block("Lists and pairs", "One field per value; × removes a row, + adds one.", lists),
            FormLayout.Block("State switches", "Field-sized ON/OFF for a setting on another system.", states));
        if (part == 1) return Stack(20, FormLayout.Block("Summary cards", "Monitoring at a glance; a card that filters gets an accent frame (click Incidents).", Cards()), Blocks(340, 3,
            FormLayout.Block("Data table", "28 px rows, numbers right, a tooltip for cut text.", table),
            FormLayout.Block("Configuration table", "Natural height; a disabled row fades.", compact),
            FormLayout.Block("Status and tags", "A dot and a word; a tag shows a value.", Stack(10, Wrap(new StatusBadge("OK", "ok"), new StatusBadge("Syncing…", "busy"), new StatusBadge("Scheduled", ""), new StatusBadge("Disabled", "off")),
                Wrap(new Tag("3 new", Success), new Tag("37 up to date"), new Tag("brief-v2.pdf", null, true)))),
            FormLayout.Block("Filter chips", "A chip per value; on, it narrows the list.", Wrap(new FilterChip("Design"), chipOn, new FilterChip("Failed", Danger) { IsChecked = true })),
            FormLayout.Block("Progress", "Known, unknown length, a countdown; the status bar has a small one.", Progresses()),
            FormLayout.Block("Log", "Mono lines coloured by level; a double-click takes a whole line.", log)),
            FormLayout.Block("Editable table", "Cells edit in place; invalid numbers replace the focus border (Escape cancels); a unit cycles on a click; Total = Quantity × Price, Net = Total − Discount + Shipping; the footer adds up. Drag a heading's edge to resize; row numbers select; right-click a row for its menu.", Editing()),
            FormLayout.Block("Editable table · column ribbon", "The integrated native editor uses shared controls. Pick a header; Column, Text, Width and Table follow it.", Editing(true)),
            FormLayout.Block("Editable table · formula", "One term per row, shared dropdowns above the grid, and Add term beside the label.", FormulaEditing()),
            FormLayout.Block("Editable table · toolbar layout", "Placement, visibility and extra actions are properties: here the toolbar is below the table without Remove column and Calculated column, Add rows is above it, and each bar carries an app action.", ToolbarEditing()),
            FormLayout.Block("Editable table · trimmed ribbon", "RibbonTabs, HiddenRibbonFields and PaperFormats trim the ribbon; ColumnFields adds an app setting (Export name) to the Column tab.", RibbonEditing()),
            FormLayout.Block("Editable table · own content", "CellTemplate, HeaderTemplate and FooterTemplate put the app's own controls in a column: a Shade StatusBadge per cell, an accent dot in the heading, a badge in the footer. Type a new status; the badge follows.", TemplatedEditing()),
            FormLayout.Block("Editable table · paper format", "A4 landscape document settings with full-width editing; Viewport switches to a physical paper preview.", PaperEditing()),
            FormLayout.Block("Table designer", "Ctrl+drag a header to a gap; the accent line marks where it lands. Right-click a header for its menu; Ctrl+arrows move it.", Designers(interactive)));
        return Blocks(340, 3,
            FormLayout.Block("Menus", "The same rows as a dropdown, as a free card; danger last, below a line. Item records add an icon, a shortcut hint, a disabled or checked state.", Stack(12, menu, records)),
            FormLayout.Block("Tray menu", "The tray icon's right-click menu: every item at its natural height.", TrayCard()),
            FormLayout.Block("Tooltip", "Hover the button in the gallery; this is how it looks.", Stack(10, hover, tip)),
            FormLayout.Block("Dialog", "8 px corners, Cancel then the action, named for what it does.", DialogPreview()),
            FormLayout.Block("Reference card", "Documentation beside a form; a click on an example uses it.", Reference()),
            FormLayout.Block("Progressive help", "Reference help opens on demand.", FormLayout.Disclosure("Formatting help", () => Build.Text("Use *italic*, **bold** and `code`.", Text2))),
            FormLayout.Block("Empty state", "What's missing, why, and the action that fixes it.", Build.Empty("No projects yet", "A project groups tasks, files and people.", "New project", () => { })));
    }

    /// <summary>The editable table with the live page's order: a value with a unit, a price, two computed columns, a footer.</summary>
    static Control FormulaEditing()
    {
        var table = (EditableTable)Editing(true); table.CurrentColumn = table.Columns.Last(); return table;
    }
    internal static Control ToolbarEditing()
    {
        var table = (EditableTable)Editing(true);
        table.ToolbarPlacement = EditableTable.BarPlacement.Bottom; table.AddRowsPlacement = EditableTable.BarPlacement.Top;
        table.ToolbarActions = EditableTable.ToolbarAction.All & ~(EditableTable.ToolbarAction.RemoveColumn | EditableTable.ToolbarAction.CalculatedColumn);
        table.ToolbarItems.Add(new ActionButton("Import columns", ButtonKind.Ghost)); table.AddRowsItems.Add(new ActionButton("Import rows", ButtonKind.Ghost));
        return table;
    }
    internal static Control RibbonEditing()
    {
        var table = (EditableTable)Editing(true);
        var export = new Dictionary<EditableTable.Column, string>();
        table.RibbonTabs = EditableTable.RibbonTab.Column | EditableTable.RibbonTab.Width;
        table.HiddenRibbonFields = new[] { "Footer", "Alignment" };
        table.ColumnFields.Add(new EditableTable.ColumnField("Export name", c => export.GetValueOrDefault(c, c.Key), (c, v) => export[c] = v));
        return table;
    }
    internal static Control TemplatedEditing()
    {
        var table = new EditableTable();
        var item = new EditableTable.Column("item", "Item") { Grow = true };
        var status = new EditableTable.Column("status", "Status", EditableTable.Kind.General) { Width = 150, Footer = EditableTable.Total.Count };
        status.CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<EditableTable.Row>((r, _) => new StatusBadge(r.Values.GetValueOrDefault("status") ?? "", r.Values.GetValueOrDefault("kind") ?? ""));
        status.HeaderTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<EditableTable.Column>((c, _) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(6), VerticalAlignment = VerticalAlignment.Bottom, Children = { new Avalonia.Controls.Shapes.Ellipse { Width = M(8), Height = M(8), Fill = B(Accent), VerticalAlignment = VerticalAlignment.Center }, Build.Text(c.Title, Look.Text, TextSize.Label, 600) } });
        status.FooterTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<EditableTable.Column>((c, _) => new StatusBadge("Count " + table.Footer(c), "ok"));
        var rows = new List<EditableTable.Row>();
        foreach (var (name, state, kind) in new[] { ("Invoice 1041", "Paid", "ok"), ("Invoice 1042", "Open", "warn"), ("Invoice 1043", "Overdue", "err") })
        {
            var r = new EditableTable.Row(); r.Values["item"] = name; r.Values["status"] = state; r.Values["kind"] = kind; rows.Add(r);
        }
        table.Columns = new List<EditableTable.Column> { item, status }; table.Rows = rows;
        return table;
    }
    internal static Control PaperEditing()
    {
        var table = (EditableTable)Editing(true); table.PaperDimensionsPlacement = EditableTable.PaperDimensionsPosition.FormatLabel; table.PaperFormat = EditableTable.PaperKind.A4; table.PaperLandscape = true; table.ConstrainToPaper = false;
        table.AttachedToVisualTree += (_, _) => table.GetVisualDescendants().OfType<TextTabs>().Single().SetActive(3);
        return table;
    }

    internal static Control WideEditing()
    {
        var table = (EditableTable)FormulaEditing(); table.PaperDimensionsPlacement = EditableTable.PaperDimensionsPosition.FormatLabel; table.PaperFormat = EditableTable.PaperKind.A4; table.PaperLandscape = true; table.ConstrainToPaper = false;
        table.Columns[0].MinWidth = 330.0354228182; table.Refresh();
        return table;
    }

    static Control Editing(bool design = false)
    {
        EditableTable.Column C(string key, string title, EditableTable.Kind kind = EditableTable.Kind.Text, string subtitle = "", string suffix = "") =>
            new EditableTable.Column(key, title, kind) { Subtitle = subtitle, Suffix = suffix == "" ? null : suffix, Decimals = kind is EditableTable.Kind.Number or EditableTable.Kind.Computed ? 2 : null };
        var qty = C("qty", "Quantity", EditableTable.Kind.Unit, "amount · unit"); qty.Units = new[] { "pcs", "box", "pallet" }; qty.Footer = EditableTable.Total.Count;
        var price = C("price", "Price per unit", EditableTable.Kind.Number, "€", "€"); price.Footer = EditableTable.Total.Average;
        var total = C("total", "Total", EditableTable.Kind.Computed, "€"); total.Footer = EditableTable.Total.Sum; total.Terms.Add(new EditableTable.Term("qty")); total.Terms.Add(new EditableTable.Term("price", EditableTable.Op.Multiply));
        var discount = C("discount", "Discount", EditableTable.Kind.Number, "€", "€"); discount.Footer = EditableTable.Total.Sum;
        var shipping = C("shipping", "Shipping", EditableTable.Kind.Number, "€", "€"); shipping.Footer = EditableTable.Total.Sum;
        var net = C("net", "Net", EditableTable.Kind.Computed, "€"); net.Bold = true; net.Footer = EditableTable.Total.Sum;
        net.Terms.Add(new EditableTable.Term("total")); net.Terms.Add(new EditableTable.Term("discount", EditableTable.Op.Subtract)); net.Terms.Add(new EditableTable.Term("shipping"));
        var item = C("item", "Item"); item.Grow = true;
        var codeColumn = C("sku", "Code", EditableTable.Kind.General); codeColumn.Alignment = TextAlignment.Left;
        var rows = new List<EditableTable.Row>();
        foreach (var (name, code, q, unit, p, d, sh) in new[] { ("Copy paper A4, 80 g", "PAP-A4-80", "12", "box", "4.90", "2", "1.20"), ("Ballpoint pens, blue", "PEN-BL-10", "40", "pcs", "0.85", "0", "0"), ("Archive boxes", "BOX-ARC", "1", "pallet", "129.00", "15", "18"), ("Whiteboard markers, assorted colours for the meeting rooms", "MRK-WB-4", "6", "pcs", "3.20", "0", "0") })
        {
            var r = new EditableTable.Row(); r.Values["item"] = name; r.Values["sku"] = code; r.Values["qty"] = q; r.Units["qty"] = unit; r.Values["price"] = p; r.Values["discount"] = d; r.Values["shipping"] = sh;
            rows.Add(r);
        }
        return new EditableTable { ShowColumnEditor = true, Design = design, Columns = new List<EditableTable.Column> { item, codeColumn, qty, price, total, discount, shipping, net }, Rows = rows, RowMenuItems = { new EditableTable.RowMenuItem(new MenuEntry("Open record"), _ => { }) } };
    }

    static Control Cards()
    {
        var cards = new SummaryCards();
        var items = new List<SummaryCards.Item>
        {
            new() { Key = "services", Title = "Services", Value = "11/12", Unit = "healthy", Share = 11 / 12.0, Tone = Warn, Line = "Degraded: billing-api", LineTone = Warn, Corner = "checked 14:02" },
            new() { Key = "uptime", Title = "Uptime", Value = "99.94 %", Unit = "30 d", Share = 0.9994, Tone = Success, Line = "SLO 99.9 % · met", Corner = "30 days" },
            new() { Key = "latency", Title = "Latency p95", Value = "182 ms", Trend = new double[] { 140, 150, 146, 160, 158, 171, 166, 182 }, Line = "▲ 14 % on the last hour", LineTone = Warn, Corner = "api-gw" },
            new() { Key = "incidents", Title = "Incidents", Value = "2", Unit = "open", Tone = DangerText, Line = "Oldest 42 min · api-gw", LineTone = Warn, Corner = "P1 · P2", Active = true },
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
        return Stack(12, Build.Text("Compact · 84 px", Text3, TextSize.Caption), cards, Build.Text("Detailed · 112 px, with a subtitle", Text3, TextSize.Caption), detailed);
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
        return Stack(8, menu, Build.Text("TrayMenu.Show(x, y, items, picked): by the pointer, kept on screen.", Text3, TextSize.Caption));
    }

    static Control Tabs() { var t = new TextTabs("Summary", "Tasks", "Files"); t.SetActive(1); return t; }

    /// <summary>A real confirmation dialog's content, shown in its frame.</summary>
    static Control DialogPreview()
    {
        var ask = new Ask("Delete “Website refresh”?\n\nIts files stay in the library.", "Delete project", true, 330, true);
        var body = (Control)ask.Content; ask.Content = null;
        return new Border { Background = B(Look.Background), BorderBrush = B(Look.Separator), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(CardRadius), Child = body, HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = true };
    }

    // ---- 04 Spacing · Radii ------------------------------------------------------------------------

    static Control Spacing(bool interactive)
    {
        var spaces = new StackPanel { Spacing = 8 };
        foreach (var s in Spaces)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new Border { Width = 48, Child = Build.MonoText(s + " px", Text2, 11) });
            row.Children.Add(new Border { Width = s * 4, Height = 12, Background = B(AccentTint), BorderBrush = B(AccentTintLine), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) });
            spaces.Children.Add(row);
        }
        var uses = new StackPanel { Spacing = 4 };
        foreach (var (what, px) in new[] { ("Label and its field", "4"), ("Buttons in a row", "8"), ("Fields stacked", "12"), ("Page edge, fields side by side", "16"), ("Dialog edge", "24"), ("Columns of blocks", "32") })
        {
            var d = new DockPanel(); var v = Build.MonoText(px, Text3, 11); DockPanel.SetDock(v, Dock.Right); d.Children.Add(v); d.Children.Add(Build.Text(what, Text2, TextSize.Body)); uses.Children.Add(d);
        }
        Control Corner(string name, double r) => Stack(6, new Border { Width = 72, Height = 48, Background = B(Fill), BorderBrush = B(Outline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(r) },
            Build.Text(name, Look.Text, TextSize.Caption, 500), Build.MonoText(r + " px", Text3, 10));
        var heights = Stack(10, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new Field("28 px") { Width = 120 }, new ActionButton("28 px", ButtonKind.Primary) } },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new FilterChip("24 px chip"), new Tag("24 px tag") } });
        var lines = Stack(10, new Border { Height = 1, Background = B(Look.Separator), Width = 240, HorizontalAlignment = HorizontalAlignment.Left },
            Build.Text("1 px Separator: every resting line", Text2, TextSize.Body),
            new Border { Width = 160, Height = ControlHeight, BorderBrush = B(Accent), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius), Background = B(Raised), HorizontalAlignment = HorizontalAlignment.Left },
            Build.Text("1 px Accent: keyboard focus; nothing glows", Text2, TextSize.Body));
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
                m.Children.Add(FormLayout.Block(t, null, new Border { Height = h, Background = B(Fill), CornerRadius = new CornerRadius(Radius) }));
            return new Border { Padding = new Thickness(16), Background = B(Look.Background), BorderBrush = B(Look.Separator), BorderThickness = new Thickness(1), Child = m, HorizontalAlignment = HorizontalAlignment.Left };
        }
        var rules = new StackPanel { Spacing = 6 };
        foreach (var r in new[] { "A Masonry fits as many columns as reach MinColumn, up to MaxColumns; each block goes to the shortest column.",
                     "Page headers keep their title left and actions right; the hint wraps.", "Tables scroll inside their area; headers stay.",
                     "The sidebar is 180 px, 54 px collapsed (icons only, tooltips for names).", "Desktop only: the window's minimum is the content's, nothing is hidden." })
        { var t = Build.Text("• " + r, Text2, TextSize.Body); t.TextWrapping = TextWrapping.Wrap; rules.Children.Add(t); }
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
        return new Border { Height = height, HorizontalAlignment = HorizontalAlignment.Left, BorderBrush = B(Look.Separator), BorderThickness = new Thickness(1), Child = sidebar };
    }

    static Control Navigation(bool interactive)
    {
        var sidebar = (Border)SidebarSample(false, 400);
        var nav = (Sidebar)sidebar.Child; sidebar.Child = null;
        var status = new StatusBar(); status.Show("Warning · Sync retries at 14:30", Warn, loud: true); status.Count("2 people online", true); status.Busy("Uploading 3 files · 64 %", 0.64);
        var page = new StackPanel { Margin = new Thickness(16) };
        page.Children.Add(FormLayout.Header("Overview", "Everything in this project at a glance.", new ActionButton("Export", ButtonKind.Ghost), new ActionButton("Publish", ButtonKind.Primary)));
        var tabs = Tabs(); tabs.Margin = new Thickness(0, 8, 0, 0); page.Children.Add(tabs);
        var shell = new Border { Height = 400, BorderBrush = B(Look.Separator), BorderThickness = new Thickness(1), Background = B(Look.Background), Child = Shell.Layout(nav, page, status) };
        var history = new NavigationButtons(); history.Update(true, false);
        var sub = FormLayout.SubHeader("Projects", () => { }, "Website refresh", "4 tasks · due Friday", new ActionButton("Save", ButtonKind.Primary));
        return Stack(20, FormLayout.Block("App shell", "Sidebar, page, status bar (Shell.Layout). The sidebar's mark collapses it.", shell),
            Blocks(340, 3,
                FormLayout.Block("Back and Forward", "In the sidebar footer; mouse side buttons and Alt+←/→ use the same history.", history),
                FormLayout.Block("Subpage header", "A page opened from another names its parent and goes back to it.", sub),
                FormLayout.Block("Tabs", "Views of one thing; the line slides to the active tab. Never pages.", Tabs()),
                FormLayout.Block("Sidebar rows", "An app with its own sidebar builds it from NavItem: the same rows, current and child.", Left(Stack(0,
                    new NavItem("Projects", Icons.Folder, false) { ParentActive = true, Width = 180 }, new NavItem("Overview", null, true) { Active = true, Width = 180 }, new NavItem("Tasks", Icons.ListChecks, false) { Width = 180 })))));
    }
}
