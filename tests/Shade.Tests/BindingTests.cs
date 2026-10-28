// Binding checks.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade.Tests
{
    static partial class Program
    {
        static void BindingRegressions()
        {
            var deferred = new MultiChoice { Selected = new HashSet<string> { "Ben" }, Items = new[] { "Anna", "Ben" } };
            Ok(deferred.Selected.Contains("Ben"), "selection may arrive before picker options");
            deferred.Items = Array.Empty<string>(); Ok(deferred.Selected.Contains("Ben"), "empty loading snapshots keep selected keys");
            deferred.Items = new[] { "Anna", "Chloe" }; Ok(deferred.Selected.Count == 0, "a populated replacement prunes unavailable selected keys");
            var search = new SearchField(); var field = new Field { Error = "Name is required" };
            var labelled = FormLayout.Field("Name", field);
            Ok(labelled.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == field.Error) && Avalonia.Automation.AutomationProperties.GetLabeledBy(field.Box) != null, "form layout preserves the shared validation message and accessible label");
            var choice = new ChoiceButton(); choice.SetItems(new[] { "One", "Two" }, "One"); int picks = 0; choice.Picked += _ => picks++;
            var source = new List<string[]> { new[] { "Alpha" }, new[] { "Beta" } }; var table = Workbench.Table(new[] { "Name" }, source);
            var parent = new StackPanel { Children = { choice, search, labelled, table } }; var window = new Window { Width = 700, Height = 500, Content = parent }; window.Show();
            void Key(Avalonia.Input.Key key) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Settle(); search.Box.Text = "abc"; Settle(); int edits = 0; search.Edited += () => edits++; search.Box.Focus(); Key(Avalonia.Input.Key.Escape);
            Ok(edits == 1 && search.Text == "", "clearing search emits exactly one edit notification");
            var list = table.GetVisualDescendants().OfType<ListBox>().Single(); list.SelectedIndex = 0; var item = list.GetVisualDescendants().OfType<ListBoxItem>().First(); int selectionChanges = 0; list.SelectionChanged += (_, _) => selectionChanges++;
            Workbench.SetMark(table, "a"); Settle();
            Ok(list.SelectedIndex == 0 && selectionChanges == 0 && ReferenceEquals(item, list.GetVisualDescendants().OfType<ListBoxItem>().First()), "highlight changes keep selection and native item containers");
            Workbench.SetMark(table, null); Settle(); Ok(list.SelectedIndex == 0 && table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Alpha"), "clearing highlights restores text without resetting the items source");
            double wide = Look.Measure("WWWW", Look.Ui(TextSize.Body)) + 24;
            Ok(Workbench.FitWidth(new[] { "iiiiiiiiiiii", "WWWW" }, 0, 1000) >= wide, "FitWidth measures rendered glyph width rather than character count");
            choice.Focus(); Key(Avalonia.Input.Key.Down); parent.IsEnabled = false; Settle(); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Enter);
            Ok(choice.Current == "One" && picks == 0 && !window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two"), "disabling a picker parent dismisses its menu and prevents stale selection commits");
            parent.IsEnabled = true; choice.Focus(); Key(Avalonia.Input.Key.Down); parent.IsVisible = false; Settle();
            Ok(!window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two" && t.IsEffectivelyVisible), "hiding a picker parent dismisses its menu"); parent.IsVisible = true;
            var designer = new TableDesigner { Columns = new[] { new TableDesigner.Column("A"), new TableDesigner.Column("B") }, Selected = 1 }; int notices = 0;
            designer.PropertyChanged += (_, e) => { if (e.Property == TableDesigner.SelectedProperty) notices++; }; designer.Columns = new[] { new TableDesigner.Column("A") };
            Ok(designer.Selected == -1 && notices == 1, "column replacement notifies the designer's corrected selection");
            var fixedRows = new EditableTable { Columns = new[] { new EditableTable.Column("name", "Name") }, Rows = new[] { new EditableTable.Row() } }; parent.Children.Add(fixedRows); Settle();
            Ok(!fixedRows.GetVisualDescendants().OfType<ActionButton>().Any(b => b.Content?.ToString() == "Add" && b.IsEffectivelyVisible), "fixed-size row sources do not advertise Add");
            window.Close();
        }

        static void PairRows()
        {
            var entries = new System.Collections.ObjectModel.ObservableCollection<PairTable.Entry> { new("Accept", "text/plain"), new("accept", "application/json"), new("", "unfinished draft") };
            var pairs = new PairTable { Entries = entries, CaseSensitive = false }; int changes = 0; pairs.Changed += () => changes++;
            var window = new Window { Width = 600, Height = 600, Content = pairs }; window.Show();
            Settle();
            Ok(pairs.HasErrors && entries.Take(2).All(entry => entry.Error != null) && !pairs.TryGetPairs(out _), "pair duplicate validation marks both rows without exporting a lossy dictionary");
            bool refused = false; try { pairs.Pairs(); } catch (ArgumentException) { refused = true; }
            Ok(refused && entries.Count == 3, "pair dictionary adapters use the configured duplicate policy by default");
            Ok(entries.Count == 3 && entries[2].Value == "unfinished draft" && pairs.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Duplicate name:", StringComparison.Ordinal) == true), "duplicate and blank pair drafts stay visible and editable");
            pairs.CaseSensitive = true; Ok(!pairs.HasErrors && pairs.TryGetPairs(out var sensitive) && sensitive.Count == 2, "pair duplicate comparison can be case-sensitive");
            pairs.CaseSensitive = false; entries[1].Name = "X-Token"; Settle();
            Ok(!pairs.HasErrors && entries.All(entry => entry.Error == null) && pairs.TryGetPairs(out var valid) && valid.Count == 2 && changes == 0, "external row edits refresh pair validation without emitting user changes");
            var key = pairs.GetVisualDescendants().OfType<Field>().Single(field => field.Text == "Accept"); key.Box.Focus(); key.Box.CaretIndex = 2;
            entries.Add(new("Extra", "value")); Settle();
            Ok(key.Box.IsFocused && key.Box.CaretIndex == 2 && pairs.GetVisualDescendants().Contains(key), "adding pair rows retains existing editors, focus and caret");
            key.Box.Text = "Renamed"; Settle();
            Ok(entries[0].Name == "Renamed" && changes == 1, "pair typing writes to the row model and emits one user change");
            entries.RemoveAt(2); pairs.Set(pairs.Pairs()); Settle();
            Ok(key.Box.IsFocused && pairs.GetVisualDescendants().Contains(key), "setting unchanged exported pairs preserves the active editor");
            var remove = pairs.GetVisualDescendants().OfType<ActionButton>().First(button => button.Icon == Icons.X);
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(remove)!).Invoke(); Settle();
            Ok(entries.Count == 2 && entries.All(entry => entry.Name != "Renamed") && changes == 2, "native pair removal updates the bound collection and emits one user change");
            window.Content = null; entries.Add(new("X-Token", "duplicate after detach")); window.Content = pairs; Settle();
            Ok(pairs.HasErrors && entries.Where(entry => entry.Name == "X-Token").All(entry => entry.Error != null), "reattaching pair rows reconciles changes made while detached");
            pairs.Entries = Array.AsReadOnly(entries.ToArray()); Settle();
            Ok(!pairs.GetVisualDescendants().OfType<ActionButton>().Any(button => button.IsEffectivelyVisible && (button.Content?.ToString() == "Add" || button.Icon == Icons.X)), "fixed-size pair structures hide structural actions while retaining editors");
            window.Close();
        }

        static void Adaptability()
        {
            var validating = new ValidationVm();
            var boundField = new Field();
            boundField.Bind(Field.TextProperty, new Avalonia.Data.Binding(nameof(ValidationVm.Name)) { Source = validating, Mode = Avalonia.Data.BindingMode.TwoWay });
            var boundGroup = Build.Labelled("Name", boundField);
            var boundWindow = new Window { Content = boundGroup }; boundWindow.Show(); Dispatcher.UIThread.RunJobs();
            Ok(boundGroup.Children.OfType<TextBlock>().Any(t => t.Text == "Name is required"), "INotifyDataErrorInfo errors reach field validation");
            validating.Name = "Valid"; Dispatcher.UIThread.RunJobs();
            Ok(!boundGroup.Children.OfType<TextBlock>().Last().IsVisible, "correcting a bound value clears validation");
            boundWindow.Close();
            var edgeDate = new DateField { Value = new DateTime(9999, 10, 25) };
            var edgeWindow = new Window { Content = edgeDate }; edgeWindow.Show();
            var edgeBlocks = edgeDate.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "DateBlocks");
            edgeBlocks.Focus(); edgeWindow.KeyPress(Avalonia.Input.Key.Up, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Dispatcher.UIThread.RunJobs();
            Ok(edgeDate.Value?.Year == 9999, "incrementing the maximum date year does not overflow"); edgeWindow.Close();
            var field = new Field { Error = "Enter a name", IsReadOnly = true };
            var group = Build.Labelled("Name", field);
            Ok(group.Children.OfType<TextBlock>().Any(t => t.Text == "Enter a name" && t.IsVisible), "field error is visible and associated with its input");
            Ok(field.Box.IsReadOnly && Avalonia.Automation.AutomationProperties.GetLabeledBy(field.Box) != null, "read-only and accessible label reach the editor");
            field.Error = null;
            Ok(!group.Children.OfType<TextBlock>().Last().IsVisible, "clearing validation hides the error");
            field.SetValue(Field.InvalidProperty, true); field.SetValue(Field.PendingProperty, true);
            Ok(field.Invalid && field.Pending, "validation and pending state use Avalonia properties");
            var column = new EditableTable.Column("n", "Number", EditableTable.Kind.Number) { InputCulture = new System.Globalization.CultureInfo("en-US") };
            var row = new EditableTable.Row(); row.Values["n"] = "1,234.50";
            var table = new EditableTable { Columns = new[] { column }, Rows = new List<EditableTable.Row> { row } };
            Ok(table.Exact(row, column) == 1234.50m, "English input preserves thousands and decimal separators");
            column.InputCulture = new System.Globalization.CultureInfo("cs-CZ"); row.Values["n"] = "1234,50";
            Ok(table.Exact(row, column) == 1234.50m, "Czech input uses a decimal comma");
            column.Parse = text => text == "half" ? 0.5m : null; row.Values["n"] = "half";
            Ok(table.Exact(row, column) == 0.5m, "a project parser overrides numeric culture");
            column.Parse = null;
            table.Validate = (_, _, text) => text == "bad" ? "Enter a number" : null;
            int edits = 0; table.CellEdited += (_, _, before, after) => { if (before == "half" && after == "2") edits++; };
            Ok(!table.TrySetValue(row, column, "bad") && row.Values["n"] == "half", "rejected cell edit preserves the previous value");
            Ok(table.TrySetValue(row, column, "2") && edits == 1 && table.LastValidationError == null, "accepted edit reports old and new values");
            table.IsReadOnly = true;
            Ok(!table.TrySetValue(row, column, "3") && row.Values["n"] == "2", "read-only table rejects programmatic edits through its edit API");
            string Localize(string key, object[] args) => (string)typeof(Look).GetMethod("T", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(object[]) }, null)!.Invoke(null, new object[] { key, args })!;
            Look.Localize = (key, args) => key == "Delete {0} rows" ? ((int)args[0] == 1 ? "Smazat řádek" : "Smazat řádky") : key;
            Ok(Localize("Delete {0} rows", new object[] { 1 }) == "Smazat řádek" && Localize("Delete {0} rows", new object[] { 4 }) == "Smazat řádky", "localizer receives arguments for plural rules");
            Look.Localize = null;
            var button = new ActionButton("Run"); int runs = 0; button.Click += (_, _) => runs++;
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(button);
            ((Avalonia.Automation.Provider.IInvokeProvider)peer!).Invoke();
            Ok(runs == 1, "button automation invokes the same action as the keyboard");
            var toggle = new Switch(); var togglePeer = (Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(toggle)!;
            togglePeer.Toggle();
            Ok(toggle.IsChecked == true && togglePeer.ToggleState == Avalonia.Automation.Provider.ToggleState.On, "switch automation exposes and changes its state");
            var tick = new Tick { IsChecked = null }; var tickPeer = (Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(tick)!;
            Ok(tickPeer.ToggleState == Avalonia.Automation.Provider.ToggleState.Indeterminate, "tick automation exposes mixed state");
            var completion = new System.Threading.Tasks.TaskCompletionSource(); int executions = 0;
            button.AsyncAction = _ => { executions++; return completion.Task; };
            var pending = button.RunAsync(); var duplicate = button.RunAsync();
            Ok(button.IsBusy && !button.IsEffectivelyEnabled && executions == 1 && duplicate.IsCompleted, "async action prevents duplicate execution");
            completion.SetResult();
            while (!pending.IsCompleted) Dispatcher.UIThread.RunJobs();
            Ok(!button.IsBusy && button.IsEffectivelyEnabled, "async completion restores the button");
            button.AsyncAction = _ => throw new InvalidOperationException("Failed");
            button.RunAsync().GetAwaiter().GetResult();
            Ok(button.LastError?.Message == "Failed" && !button.IsBusy, "async failures are retained and restore button state");
            button.AsyncAction = token => System.Threading.Tasks.Task.Delay(-1, token);
            pending = button.RunAsync(); button.Cancel();
            while (!pending.IsCompleted) Dispatcher.UIThread.RunJobs();
            Ok(button.LastError == null && !button.IsBusy, "cancelled action restores state without reporting a failure");
            var observable = new System.Collections.ObjectModel.ObservableCollection<EditableTable.Row>();
            table = new EditableTable { Columns = new List<EditableTable.Column> { column }, Rows = observable };
            var window = new Window { Content = table }; window.Show();
            observable.Add(row); Dispatcher.UIThread.RunJobs();
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "2"), "observable row additions update the view");
            observable.Clear(); Dispatcher.UIThread.RunJobs(); window.Close();
            try
            {
                Look.Configure(new ShadeOptions { Density = 1.25, TextScale = 1.25, ReducedMotion = true });
                var larger = new Field();
                Ok(larger.Height == 35 && larger.Box.FontSize == 16.25, "application metrics expand field height and text together");
                var still = new Switch();
                Ok(still.Transitions == null, "reduced motion disables switch transitions");
                bool refused = false; try { Look.Configure(new ShadeOptions { TextScale = double.NaN }); } catch (ArgumentOutOfRangeException) { refused = true; }
                Ok(refused, "invalid application metrics are rejected");
            }
            finally { Look.Configure(new ShadeOptions()); }
        }

        static void Xaml()
        {
            var vm = new Vm();
            var view = new Bound { DataContext = vm };
            var window = new Window { Content = view }; window.Show();
            Dispatcher.UIThread.RunJobs();
            T Get<T>(string name) where T : Control => view.FindControl<T>(name) ?? throw new Exception(name + " missing");

            Ok(Get<SearchChoice>("EmployeePicker").SelectedItem is Employee { Id: 2 }, "search choice supports compiled object, search-text and selected-ID bindings");
            var sw = Get<Switch>("Toggle");
            vm.Flag = true; Ok(sw.IsChecked == true, "Switch: the view model turns it on");
            ((Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(sw)!).Toggle(); Ok(!vm.Flag, "Switch: a toggle reaches the view model");
            Ok(Get<StateToggle>("State").IsChecked == false, "StateToggle follows the same value");

            var tick = Get<Tick>("Box");
            ((Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(tick)!).Toggle(); Ok(vm.Ticked == true, "Tick: a click reaches the view model");
            vm.Ticked = null; Ok(tick.IsChecked == null, "Tick: the view model sets it to some");

            vm.ActionText = "Apply";
            Ok(Get<ActionButton>("Save").Content?.ToString() == "Apply", "action labels support compiled XAML binding");
            var multi = Get<MultiChoice>("PeoplePicker");
            Ok(multi.Items.SequenceEqual(vm.People) && multi.Selected.SetEquals(new[] { "Ben" }), "multi-choice items and selection support compiled XAML bindings");
            multi.Set(new[] { "Anna" }); Ok(vm.SelectedPeople.SetEquals(new[] { "Anna" }), "programmatic selection notifies the two-way XAML binding");
            vm.SelectedPeople = new HashSet<string> { "Chloe" }; Ok(multi.Selected.Contains("Chloe"), "multi-choice follows replacement view-model selections");
            vm.People = new[] { "Anna", "Dora" }; Ok(multi.Items.SequenceEqual(vm.People) && vm.SelectedPeople.Count == 0, "option replacement prunes missing selections through the binding");
            var shortChoices = Get<TickGrid>("ShortChoices");
            vm.ShortPicks = new[] { "Dora" }; Ok(shortChoices.Picked.SequenceEqual(new[] { "Dora" }), "short-choice selection supports replacement bindings");
            shortChoices.Picked = new[] { "Anna", "Dora" }; Ok(vm.ShortPicks.SequenceEqual(shortChoices.Picked), "short-choice selection notifies its two-way binding");
            Ok(ReferenceEquals(Get<PairTable>("Pairs").Entries, vm.PairRows), "pair rows bind as editable objects without dictionary conversion");
            vm.Name = "Bound";
            Ok(Get<HostPortField>("Address").Box.PlaceholderText == "Bound" && Get<StatusBadge>("Badge").Text == "Bound" && Get<Tag>("FileTag").Text == "Bound" && Get<FilterChip>("Chip").Content?.ToString() == "Bound" && Get<TableDesigner>("Designer").EmptyText == "Bound", "composite labels, placeholders and native content support compiled bindings");
            vm.Designing = true; Ok(Get<TableDesigner>("Designer").Locked, "table designer options support compiled bindings");
            vm.Flag = false; Ok(Get<Sidebar>("Side").Brand == "Bound" && Get<Sidebar>("Side").Entries.Count == 2 && !Get<Sidebar>("Side").ShowHistory && !Get<Sidebar>("Side").Entries[1].IsEnabled, "a sidebar is made in XAML: brand and history bind, entries are children");
            vm.ActionIcon = Icons.Download; vm.ActionKind = ButtonKind.Danger;
            Ok(Get<ActionButton>("Save").Kind == ButtonKind.Danger && Get<IconButton>("ExportAction").Icon == Icons.Download && Get<Capsule>("PresetAction").Content?.ToString() == "Apply", "native action variants support compiled content, icon and kind bindings");
            vm.ActionKind = ButtonKind.Primary;
            var field = Get<Field>("NameField");
            field.Box.Text = "Anna"; Dispatcher.UIThread.RunJobs(); Ok(vm.Name == "Anna", "Field: typing reaches the view model");
            vm.Name = "Ben"; Ok(field.Text == "Ben", "Field: the view model sets the text");
            var recipients = Get<ListField>("Recipients");
            vm.Recipients = "one@example.com, two@example.com"; Dispatcher.UIThread.RunJobs();
            Ok(recipients.Fields.Count() == 2, "ListField: view-model text creates editable rows");
            var recipient = recipients.Fields.First(); recipient.Box.Text = "edited@example.com"; Dispatcher.UIThread.RunJobs();
            Ok(vm.Recipients.StartsWith("edited@example.com", StringComparison.Ordinal), "ListField: row edits flow back through its two-way binding");
            recipients.SetValue(ListField.HintProperty, "Recipient");
            Ok(recipients.Fields.All(f => f.Box.PlaceholderText == "Recipient"), "ListField: bound hints update existing row editors");
            var find = Get<SearchField>("Find");
            find.Box.Text = "rep"; Dispatcher.UIThread.RunJobs(); Ok(vm.Query == "rep", "SearchField binds like a field");

            var bar = Get<Progress>("Bar");
            Ok(bar.Value == 0.25 && bar.Label == "Uploading", "Progress: value bound, label set in XAML");
            vm.Share = null; Ok(bar.Value == null, "Progress: null = unknown length");
            vm.Share = 2; Ok(bar.Value == 1, "Progress: the value is clamped to 1");

            var cards = Get<SummaryCards>("Cards");
            Ok(cards.Items.Count == 1 && cards.Detailed, "SummaryCards: items bound, detailed set in XAML");
            vm.Cards = vm.Cards.Append(new SummaryCards.Item { Title = "Backups", Value = "6/6" }).ToList();
            Ok(cards.Items.Count == 2, "SummaryCards: new items follow");
            vm.CardKey = "Checks"; cards.SelectedKey = "Backups"; Ok(cards.SelectedKey == "Backups" && vm.CardKey == "Backups", "SummaryCards: SelectedKey binds two-way from XAML");

            var save = Get<ActionButton>("Save");
            Ok(save.Content?.ToString() == "Apply" && save.Kind == ButtonKind.Primary, "ActionButton: text and kind from XAML");
            save.PerformClick(); Ok(vm.Save.Runs == 1, "ActionButton: a click runs the command");
            vm.Save.Can = false; Ok(!save.IsEffectivelyEnabled, "ActionButton: the command's CanExecute disables it");
            save.PerformClick(); Ok(vm.Save.Runs == 1, "ActionButton: a disabled button doesn't run it");

            var choice = Get<ChoiceButton>("Choice");
            choice.SetItems(new[] { "One", "Two", "Three" }, vm.Pick);
            Ok(choice.Current == "Two", "ChoiceButton: the current item from the view model");
            vm.Pick = "Three"; Ok(choice.Current == "Three", "ChoiceButton: the view model picks");

            var colourHelper = Get<ColorGrid>("ColourHelper");
            Ok(colourHelper.Value == vm.SwatchColour, "ColorGrid: compiled XAML creates the helper and binds its colour");
            colourHelper.SetCurrentValue(ColorGrid.ValueProperty, Hex("#2DD4BF"));
            Ok(vm.SwatchColour == Hex("#2DD4BF"), "ColorGrid: user value changes flow back through the native two-way property");
            vm.SwatchColour = Hex("#3366FF");
            Ok(colourHelper.Value == Hex("#3366FF"), "ColorGrid: source updates keep working after a user pick");
            var picker = Get<AccentPicker>("Picker");
            Ok(picker.Label == "Theme colour", "AccentPicker: the label from XAML");
            vm.Accent = "Teal"; Ok(picker.Value == "Teal" && Look.Accent.Color == Accents.First(a => a.name == "Teal").dark, "AccentPicker: the view model sets the accent");
            vm.Accent = "Violet";

            var dayField = Get<DateField>("Day");
            Ok(dayField.Value == new DateTime(2026, 10, 24) && dayField.Text == "2026-10-24", "DateField: the view model's day, shown as yyyy-MM-dd");
            dayField.Text = "1.11.2026"; Dispatcher.UIThread.RunJobs(); Ok(vm.Day == new DateTime(2026, 11, 1), "DateField: a day set as text reaches the view model");
            var span = Get<DateRangeField>("Span");
            Ok(span.Start == new DateTime(2026, 11, 1) && span.End == new DateTime(2026, 10, 30), "DateRangeField: the view model's start and end");
            span.End = new DateTime(2026, 11, 9); Dispatcher.UIThread.RunJobs(); Ok(vm.Until == new DateTime(2026, 11, 9), "DateRangeField: a new end reaches the view model");
            var sheet = Get<EditableTable>("Sheet");
            vm.PaperPlacement = EditableTable.PaperDimensionsPosition.FormatLabel; Dispatcher.UIThread.RunJobs(); Ok(sheet.PaperDimensionsPlacement == vm.PaperPlacement, "EditableTable: compiled placement binding follows the view model");
            vm.ConstrainToPaper = false; Dispatcher.UIThread.RunJobs(); Ok(!sheet.ConstrainToPaper, "EditableTable: compiled ConstrainToPaper binding follows the view model");
            sheet.SetCurrentValue(EditableTable.ConstrainToPaperProperty, true); Dispatcher.UIThread.RunJobs(); Ok(vm.ConstrainToPaper, "EditableTable: the viewport property has a two-way binding default");
            Ok(sheet.ShowColumnEditor == vm.Flag && sheet.ValueSize == 14 && sheet.Sorting == vm.Flag && !sheet.CanResize && !sheet.CanEditFooter, "EditableTable: sizes, sorting and permissions from XAML and the view model");
            sheet.Design = true; Dispatcher.UIThread.RunJobs(); Ok(vm.Designing, "EditableTable: Design reaches the view model");
            Ok(sheet.RibbonTabs == (EditableTable.RibbonTab.Column | EditableTable.RibbonTab.Width) && sheet.ToolbarActions == (EditableTable.ToolbarAction.EditColumns | EditableTable.ToolbarAction.AddColumn) && sheet.ToolbarPlacement == EditableTable.BarPlacement.Bottom && sheet.AddRowsPlacement == EditableTable.BarPlacement.Top, "EditableTable: toolbar actions and bar placement from XAML");

            var modes = Get<ModeSwitch>("Modes");
            vm.Mode = ModeChoice.Light; Ok(modes.Value == ModeChoice.Light && Current == Look.Mode.Light, "ModeSwitch: the view model switches the mode");
            vm.Mode = ModeChoice.Dark; Ok(Current == Look.Mode.Dark, "ModeSwitch: and back");

            var blocks = Get<Masonry>("Blocks");
            Ok(blocks.MinColumn == 200 && blocks.MaxColumns == 2 && blocks.Gap == 32, "Masonry: settings from XAML, the rest default");
            window.Close();
        }

        static void NativeFeedback()
        {
            var menu = Menu.Panel(Enumerable.Range(0, 20).Select(i => "Item " + i).ToArray(), _ => { }, () => { }, 0);
            var page = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 500 }, menu, new Border { Height = 800 } } } };
            var window = new Window { Width = 600, Height = 400, Content = page }; window.Show(); Settle();
            page.Offset = new Vector(0, 550); Settle();
            var rows = menu.GetVisualDescendants().OfType<Button>().ToArray();
            double before = page.Offset.Y;
            rows[0].Focus(Avalonia.Input.NavigationMethod.Pointer); rows[0].BringIntoView(); Settle();
            Ok(page.Offset.Y == before, "menu row focus and reveal never scroll the containing app page");
            var inner = menu.GetVisualDescendants().OfType<ScrollViewer>().Single();
            rows[0].Focus(Avalonia.Input.NavigationMethod.Directional);
            window.KeyPress(Avalonia.Input.Key.End, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(rows[^1].IsFocused && inner.Offset.Y > 0 && page.Offset.Y == before, "menu keyboard navigation scrolls its own viewport only");
            window.Close();

            var number = new EditableTable.Column("n", "Price", EditableTable.Kind.Unit) { Width = 150, Units = new[] { "kg" } };
            var next = new EditableTable.Column("text", "Description") { Width = 150 };
            var row = new EditableTable.Row(); row.Values["n"] = "12";
            var table = new EditableTable { Columns = new List<EditableTable.Column> { number, next }, Rows = new List<EditableTable.Row> { row } };
            window = new Window { Width = 600, Height = 400, Content = table }; window.Show(); Settle();
            var cell = table.GetVisualDescendants().OfType<Border>().First(c => c.GetType().Name == "DataCell");
            double height = cell.Bounds.Height;
            var at = cell.TranslatePoint(new Point(5, 10), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
            var editor = table.GetVisualDescendants().OfType<TextBox>().Single(c => c.IsFocused);
            var presenter = editor.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().Single();
            var textOrigin = presenter.TranslatePoint(default, window); var textBounds = presenter.Bounds;
            var unit = table.GetVisualDescendants().OfType<ActionButton>().Single(b => b.Content?.ToString() == "kg");
            editor.Text = "bad"; Settle();
            Ok(cell.Bounds.Height == height && DataValidationErrors.GetHasErrors(editor), "invalid numeric input preserves row height and native error state");
            Ok(presenter.TranslatePoint(default, window) == textOrigin && presenter.Bounds == textBounds && editor.TranslatePoint(new Point(editor.Bounds.Width, 0), window)!.Value.X <= unit.TranslatePoint(default, window)!.Value.X, "invalid unit-number drafts retain their text origin and clearance before the unit");
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Text == "Enter a number" && t.IsEffectivelyVisible) == 1, "the numeric editor retains one visible validation message below the table");
            editor.Text = "12"; Settle();
            Ok(cell.Bounds.Height == height && !DataValidationErrors.GetHasErrors(editor), "correcting numeric input preserves row height");
            table.Design = true; Settle();
            var header = table.GetVisualDescendants().OfType<Border>().Single(c => Avalonia.Automation.AutomationProperties.GetName(c) == "Column · Price");
            foreach (double side in new[] { -4d, 4d })
            {
                table.CurrentColumn = next; Settle();
                var edge = header.TranslatePoint(new Point(header.Bounds.Width + side, header.Bounds.Height / 2), window)!.Value;
                double width = number.Width!.Value;
                window.MouseMove(edge); window.MouseDown(edge, Avalonia.Input.MouseButton.Left);
                window.MouseMove(edge.WithX(edge.X + 20), Avalonia.Input.RawInputModifiers.LeftMouseButton);
                window.MouseUp(edge.WithX(edge.X + 20), Avalonia.Input.MouseButton.Left); Settle();
                Ok(number.Width == width + 20 && next.Width == 150, $"resizing {side}px from the divider changes the left column only");
                Ok(table.CurrentColumn == number, $"pressing the resize grip at {side}px selects its owning column");
            }
            window.Close();
        }
    }
}
