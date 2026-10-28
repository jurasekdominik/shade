// Picker checks.
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
        static void MultiSelection()
        {
            var source = new[] { "Anna", "Ben", "Chloe", "Anna" };
            var picker = new MultiChoice(source, "Everyone") { Width = 240 };
            source[0] = "Changed outside";
            Ok(picker.Items.SequenceEqual(new[] { "Anna", "Ben", "Chloe" }) && picker.Items is not string[], "multi-choice copies its options and removes duplicate ordinal names");
            int changes = 0, notifications = 0;
            picker.Changed += () => changes++;
            picker.PropertyChanged += (_, e) => { if (e.Property == MultiChoice.SelectedProperty) notifications++; };
            picker.Set(new[] { "Anna", "missing" }); picker.Set(picker.Selected);
            Ok(picker.Selected.SetEquals(new[] { "Anna" }) && notifications == 1 && changes == 0, "multi-choice selection copies safely from itself and programmatic updates notify once without Changed");
            var external = new HashSet<string> { "Ben" }; picker.Selected = external; external.Add("Chloe");
            Ok(picker.Selected.SetEquals(new[] { "Ben" }) && picker.Selected is not HashSet<string>, "multi-choice selection snapshots cannot be changed through the caller's mutable set");
            Ok(Throws(() => picker.Items = new[] { "valid", null! }) && picker.Items[0] == "Anna", "invalid option replacement leaves the existing list installed");
            var before = new ActionButton("Before"); var after = new ActionButton("After") { Margin = new Thickness(0, 200, 0, 0) };
            var content = new StackPanel { Children = { before, picker, after } };
            var window = new Window { Width = 500, Height = 500, Content = content }; window.Show();
            void Key(Avalonia.Input.Key key, Avalonia.Input.RawInputModifiers modifiers = Avalonia.Input.RawInputModifiers.None) { window.KeyPress(key, modifiers, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, modifiers, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            CheckBox[] Options() => window.GetVisualDescendants().OfType<CheckBox>().Where(option => option.Tag is string).ToArray();
            var peer = (Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(picker)!;
            Settle();
            void ClickPicker() { var at = picker.TranslatePoint(new Point(10, 10), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle(); }
            ClickPicker();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Expanded && Options().All(option => !option.Classes.Contains(":focus-visible")), "pointer opening multi-choice focuses an option without a keyboard accent ring");
            ClickPicker();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "clicking the open multi-choice trigger closes it without reopening");
            Key(Avalonia.Input.Key.Escape);
            Settle(); picker.Focus(Avalonia.Input.NavigationMethod.Tab); Key(Avalonia.Input.Key.Down);
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Expanded && Options()[1].IsFocused, "keyboard opening focuses the first selected native checkbox");
            Ok(Options()[1].Classes.Contains(":focus-visible"), "keyboard opening multi-choice keeps its visible option focus ring");
            Ok(Options().All(option => option.Bounds.Height == Look.ControlHeight), "native option targets retain Shade row metrics instead of Fluent minimum heights");
            Key(Avalonia.Input.Key.End); Key(Avalonia.Input.Key.Space);
            Ok(picker.Selected.SetEquals(new[] { "Ben", "Chloe" }) && changes == 1 && peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Expanded, "End and Space select an option while keeping multi-choice open");
            Key(Avalonia.Input.Key.Home);
            var toggle = (Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Options()[0])!;
            toggle.Toggle(); Settle();
            Ok(picker.Selected.Count == 3 && changes == 2 && toggle.ToggleState == Avalonia.Automation.Provider.ToggleState.On, "native option automation uses the same selection notification path");
            picker.Set(new[] { "Chloe" });
            Ok(Options().Select(option => option.IsChecked == true).SequenceEqual(new[] { false, false, true }) && changes == 2, "programmatic selection updates an already-open popup without user change events");
            window.KeyTextInput("B"); Settle();
            Ok(Options()[1].IsFocused, "typing an option prefix moves focus without changing selection");
            Key(Avalonia.Input.Key.Escape);
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed && picker.IsFocused && !window.GetVisualDescendants().OfType<CheckBox>().Any(), "Escape restores the trigger and detaches the popup");
            Key(Avalonia.Input.Key.Up); Ok(Options()[^1].IsFocused, "Up opens on the last option"); Key(Avalonia.Input.Key.Tab);
            Ok(after.IsFocused && peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "Tab closes multi-choice and moves to the next application control");
            picker.Focus(); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.Shift);
            Ok(before.IsFocused, "Shift+Tab closes multi-choice and moves to the previous application control");
            picker.Focus(); Key(Avalonia.Input.Key.Down); picker.Items = new[] { "Chloe", "Dora" }; Settle();
            Ok(picker.Selected.SetEquals(new[] { "Chloe" }) && peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed && picker.IsFocused, "option replacement closes safely and keeps surviving selections");
            peer.Expand(); Settle(); content.IsEnabled = false; Settle();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "disabling a parent closes multi-choice");
            content.IsEnabled = true; peer.Expand(); Settle(); content.IsVisible = false; Settle();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "hiding a parent closes multi-choice");
            content.IsVisible = true; peer.Expand(); Settle();
            var outside = after.TranslatePoint(new Point(after.Bounds.Width / 2, after.Bounds.Height / 2), window)!.Value;
            window.MouseDown(outside, Avalonia.Input.MouseButton.Left); window.MouseUp(outside, Avalonia.Input.MouseButton.Left); Settle();
            Ok(after.IsFocused && peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "light dismissal preserves focus on the control clicked outside");
            peer.Expand(); Settle(); content.Children.Remove(picker); Settle();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed && !window.GetVisualDescendants().OfType<CheckBox>().Any(), "detaching multi-choice releases its popup");
            content.Children.Add(picker); picker.Items = Array.Empty<string>(); peer.Expand(); Settle();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed && picker.Selected.Contains("Chloe"), "empty options retain selection while loading and do not open an empty popup");
            try
            {
                picker.Items = new[] { "A", "B", "C" }; picker.Set(picker.Items);
                Look.Localize = (key, args) => key == "{0} selected" ? args[0] + " vybráno" : key;
                Look.RefreshLanguage();
                Ok(picker.GetVisualDescendants().OfType<TextBlock>().Single().Text == "3 vybráno", "multi-choice count uses count-aware live localization");
                picker.Label = "People"; Look.RefreshLanguage();
                Ok(picker.GetVisualDescendants().OfType<TextBlock>().Single().Text == "People", "a fixed application caption wins over the localized selection count");
            }
            finally { Look.Localize = null; Look.RefreshLanguage(); window.Close(); }
        }

        static void SuggestionLifecycle()
        {
            var field = new SuggestionField { Items = new[] { "Anna", "Annual report", "Ben" } }; int picks = 0; string? snapshot = null;
            field.Picked += _ => { picks++; snapshot = field.Text; };
            var search = new SearchField(); var parent = new StackPanel { Children = { field, search } };
            var window = new Window { Width = 500, Height = 300, Content = parent }; window.Show();
            void Key(Avalonia.Input.Key key) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Button[] Options() => window.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is int).ToArray();
            Settle(); field.Box.Focus(); field.Box.Text = "an"; Settle(); var rows = Options();
            Ok(rows.Length == 2 && field.Box.IsFocused, "bindable suggestion options open without taking editor focus");
            Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Down);
            Ok(Options().Length == 2 && ReferenceEquals(rows[0], Options()[0]) && field.Box.IsFocused, "suggestion keyboard movement retains the popup rows and native editor focus");
            Key(Avalonia.Input.Key.Enter); Ok(field.Text == "Annual report" && picks == 1, "suggestion Enter commits the highlighted result once");
            field.ApplySelection = false; field.Box.Text = "ann"; Settle(); rows = Options();
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(rows[0])!).Invoke(); Settle();
            Ok(field.Text == "ann" && snapshot == "ann" && picks == 2, "suggestion actions can snapshot current text without replacing it");
            field.Box.Text = "a"; Settle(); parent.IsEnabled = false; Settle(); Ok(Options().Length == 0, "disabling a parent closes suggestion actions");
            parent.IsEnabled = true; field.Box.Focus(); field.Box.Text = "b"; Settle(); field.IsReadOnly = true; Settle(); Ok(Options().Length == 0 && picks == 2, "read-only suggestion fields close without committing a choice");
            field.IsReadOnly = false; field.Box.Focus(); field.Items = new[] { "Bob", "Brian" }; Settle(); Ok(Options().Length == 2, "replacing bound suggestions refreshes the result snapshot");
            parent.IsVisible = false; Settle(); Ok(Options().Length == 0, "hiding an ancestor releases the suggestion popup"); parent.IsVisible = true;
            search.Box.Text = "query"; search.IsReadOnly = true; Settle(); search.Box.Focus(); Key(Avalonia.Input.Key.Escape);
            Ok(search.Text == "query" && search.GetVisualDescendants().OfType<IconButton>().Single().IsEffectivelyEnabled == false, "read-only search cannot clear through Escape or its native clear action");
            window.Close();
        }

        static void SearchableChoices()
        {
            var employees = new System.Collections.ObjectModel.ObservableCollection<Employee>
            {
                new(1, "Jan Novák", "Mgr. Research"), new(2, "Petra Šílá", "Bc. HR"), new(3, "Jan Novák", "Ing. Engineering")
            };
            var vm = new Vm { EmployeeId = 2 };
            var picker = new SearchChoice { ItemsSource = employees, DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Employee.Name)), SearchMemberBinding = new Avalonia.Data.Binding(nameof(Employee.SearchText)), SelectedValueBinding = new Avalonia.Data.Binding(nameof(Employee.Id)), PlaceholderText = "Choose employee", Width = 320 };
            picker.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedValueProperty, new Avalonia.Data.Binding(nameof(Vm.EmployeeId)) { Source = vm, Mode = Avalonia.Data.BindingMode.TwoWay });
            var before = new TextBox(); var after = new TextBox(); var parent = new StackPanel { Children = { before, picker, after } };
            var window = new Window { Width = 600, Height = 500, Content = parent }; window.Show();
            void Key(Avalonia.Input.Key key, Avalonia.Input.RawInputModifiers modifiers = Avalonia.Input.RawInputModifiers.None) { window.KeyPress(key, modifiers, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, modifiers, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            void Click(Control control, Avalonia.Input.MouseButton button = Avalonia.Input.MouseButton.Left) { var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value; window.MouseDown(at, button); window.MouseUp(at, button); Settle(); }
            SearchField Search() => window.GetVisualDescendants().OfType<SearchField>().Single();
            ListBox Results() => window.GetVisualDescendants().OfType<ListBox>().Single();
            Button Row(int id) => Results().GetVisualDescendants().OfType<Button>().Single(button => button.Content is Employee employee && employee.Id == id);
            Settle(); int committed = 0, changed = 0; picker.SelectionCommitted += (_, _) => committed++; picker.SelectionChanged += (_, _) => changed++;
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(picker)!;
            Ok(peer is Avalonia.Automation.Peers.ComboBoxAutomationPeer && ReferenceEquals(picker.SelectedItem, employees[1]) && vm.EmployeeId == 2, "search choices use native ComboBox object selection and ID bindings");
            var trigger = picker.GetVisualDescendants().OfType<Button>().First(button => Avalonia.Automation.AutomationProperties.GetAccessibilityView(button) == Avalonia.Automation.AccessibilityView.Raw);
            Ok(trigger.Bounds.Width >= picker.Bounds.Width - picker.Padding.Left - picker.Padding.Right - 2, "search-choice trigger fills its frame so the chevron aligns at the trailing edge");
            var captionPosition = trigger.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible).TranslatePoint(default, window);
            ((Avalonia.Automation.Provider.IExpandCollapseProvider)peer).Expand(); Settle();
            Ok(trigger.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible).TranslatePoint(default, window) == captionPosition, "opening a native searchable dropdown keeps the caption on the same pixels");
            Ok(picker.IsDropDownOpen && Results().ItemCount == 3 && Search().Box.IsFocused, "search choices open every option and focus the fixed search header");
            Search().Box.Text = "NOVAK mgr"; Settle();
            Ok(Results().ItemCount == 1 && Results().GetVisualDescendants().OfType<Button>().Any(button => ReferenceEquals(button.Content, employees[0])), "each search word matches independently across custom fields, without case or accents");
            Ok(ReferenceEquals(picker.SelectedItem, employees[1]) && vm.EmployeeId == 2 && committed == 0 && changed == 0, "filtering out the selected employee does not change the object, ID or selection events");
            Search().Box.Text = "no such employee"; Settle(); Key(Avalonia.Input.Key.Enter);
            Ok(Results().ItemCount == 0 && picker.IsDropDownOpen && vm.EmployeeId == 2 && committed == 0 && window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "No matches" && text.IsEffectivelyVisible), "no results is visible and Enter cannot clear the committed selection");
            Key(Avalonia.Input.Key.Escape); Ok(!picker.IsDropDownOpen && picker.IsFocused && vm.EmployeeId == 2, "Escape dismisses search without changing selection and restores the trigger");
            Key(Avalonia.Input.Key.Down); Search().Box.Text = "jan"; Settle(); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Enter);
            Ok(vm.EmployeeId == 3 && ReferenceEquals(picker.SelectedItem, employees[2]) && committed == 1 && changed == 1, "duplicate display labels remain distinct objects and keyboard commit returns the correct ID once");
            Key(Avalonia.Input.Key.Enter); Ok(picker.SearchText == "" && Results().ItemCount == 3, "reopening resets the filter and shows all options");
            Click(Row(1), Avalonia.Input.MouseButton.Right); Ok(committed == 1 && vm.EmployeeId == 3, "result rows ignore right-click selection");
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Row(1))!).Invoke(); Settle();
            Ok(vm.EmployeeId == 1 && committed == 2 && !picker.IsDropDownOpen, "native result invoke automation commits an employee object");
            Key(Avalonia.Input.Key.Enter); Key(Avalonia.Input.Key.Tab); Ok(after.IsFocused && !picker.IsDropDownOpen, "Tab closes search choices and continues to the next application control");
            picker.Focus(); Key(Avalonia.Input.Key.Space); Key(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.Shift); Ok(before.IsFocused && !picker.IsDropDownOpen, "Shift+Tab leaves search choices toward the previous application control");
            picker.Focus(); Key(Avalonia.Input.Key.Enter); employees.Add(new(4, "Žofie Malá", "Support")); Settle(); Search().Box.Text = "zofie"; Settle();
            Ok(Results().ItemCount == 1, "observable source updates refresh searchable items while open");
            Search().Box.Text = "lá"; Settle();
            Ok(Results().GetVisualDescendants().OfType<TextBlock>().Any(label => label.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(run => run.Text == "lá" && run.Foreground == B(AccentText)) == true), "searchable options highlight accented label matches with the readable accent role");
            Search().Box.Text = "zofie"; Settle();
            picker.ClearValue(ItemsControl.DisplayMemberBindingProperty);
            Avalonia.Controls.Primitives.TextSearch.SetTextBinding(picker, new Avalonia.Data.Binding(nameof(Employee.Name)));
            picker.SelectionBoxItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Employee>((employee, _) => new TextBlock { Text = employee?.Name });
            picker.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Employee>((employee, _) => employee == null ? null : new StackPanel { Children = { new TextBlock { Text = employee.Name }, new TextBlock { Text = employee.Title } } }); Settle();
            Ok(Results().GetVisualDescendants().OfType<TextBlock>().Any(label => label.Text == "Support"), "search choices accept rich native item templates while keeping searchable metadata separate");
            var at = Row(4).TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(590, 490)); window.MouseUp(new Point(590, 490), Avalonia.Input.MouseButton.Left); Settle();
            Ok(committed == 2 && vm.EmployeeId == 1, "dragging a result press outside cancels activation");
            var stale = Row(4); employees.RemoveAt(3);
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(stale)!).Invoke();
            Ok(vm.EmployeeId == 1 && committed == 2, "removing an option before its queued refresh cannot commit a stale row or clear selection");
            parent.IsEnabled = false; Settle(); Ok(!picker.IsDropDownOpen && committed == 2, "disabling a parent closes searchable choices without committing");
            parent.IsEnabled = true; picker.Focus(); Key(Avalonia.Input.Key.Enter); parent.IsVisible = false; Settle(); Ok(!picker.IsDropDownOpen, "hiding an ancestor closes native searchable popups");
            parent.IsVisible = true; picker.Focus(); Key(Avalonia.Input.Key.Enter); parent.Children.Remove(picker); Settle(); Ok(!picker.IsDropDownOpen, "detaching search choices releases their popup");
            window.Close();
            var record = new PairTable.Entry("Jan Novák", "Old department");
            var live = new SearchChoice { ItemsSource = new[] { record }, SelectedItem = record, DisplayMemberBinding = new Avalonia.Data.Binding(nameof(PairTable.Entry.Name)), SearchMemberBinding = new Avalonia.Data.Binding(nameof(PairTable.Entry.Value)) };
            var liveWindow = new Window { Width = 500, Height = 400, Content = live }; liveWindow.Show(); Settle(); live.IsDropDownOpen = true; Settle(); live.SearchText = "new"; Settle();
            var liveList = liveWindow.GetVisualDescendants().OfType<ListBox>().Single(); record.Value = "New department"; live.RefreshSearch(); Settle();
            Ok(liveList.ItemCount == 1 && ReferenceEquals(live.SelectedItem, record), "explicit refresh updates in-place search fields without changing the selected object");
            live.SearchText = ""; record.Name = "Jan Updated"; Settle();
            Ok(liveWindow.GetVisualDescendants().OfType<TextBlock>().Any(label => label.Text == "Jan Updated"), "native display bindings still observe item property changes");
            liveWindow.Close();
        }

        static void NativeSelections()
        {
            var tick = new CheckBox { IsChecked = null }; int clicks = 0; tick.Click += (_, _) => clicks++;
            var state = new StateToggle { IsChecked = true, ShowUndo = true, Width = 150 }; var undo = new Command(); state.UndoCommand = undo; int toggles = 0; state.Click += (_, e) => { if (ReferenceEquals(e.Source, state)) toggles++; };
            var grid = new TickGrid(new[] { "One", "Two" }, Array.Empty<string>()); int changes = 0; grid.Changed += () => changes++;
            var tag = new Tag("Report", removable: true); int removes = 0; tag.Removed += () => removes++;
            var choice = new ChoiceButton { Current = "Two", Items = new[] { "One", "Two", "Three" } }; int picks = 0; choice.Picked += _ => picks++;
            var before = new Button { Content = "Before" }; var after = new Button { Content = "After" };
            var content = new StackPanel { Children = { tick, state, grid, tag, before, choice, after } }; var window = new Window { Width = 600, Height = 500, Content = content }; window.Show();
            void Click(Control control, Avalonia.Input.MouseButton button = Avalonia.Input.MouseButton.Left) { var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value; window.MouseDown(at, button); window.MouseUp(at, button); Settle(); }
            void Key(Avalonia.Input.Key key) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Settle(); Click(tick, Avalonia.Input.MouseButton.Right); Ok(tick.IsChecked == null && clicks == 0, "native ticks ignore right-click activation");
            Click(tick); Ok(tick.IsChecked == false && clicks == 1, "a mixed check box follows Avalonia's cycle on a primary click");
            tick.IsChecked = false; Ok(clicks == 1, "programmatic toggle changes do not raise user Click");
            tick.Focus(); Key(Avalonia.Input.Key.Space); Ok(tick.IsChecked == true && clicks == 2, "native toggle keyboard activation raises one click");
            var undoButton = state.GetVisualDescendants().OfType<IconButton>().Single(); Click(undoButton);
            Ok(undo.Runs == 1 && state.IsChecked == true && toggles == 0, "the separate native undo command does not toggle its parent state");
            var row = grid.Children.OfType<CheckBox>().First(); var glyph = row.GetVisualDescendants().OfType<CheckBox>().Single();
            Ok(glyph.Bounds.Size == new Size(16, 16), $"native tick-grid faces retain their square metrics ({glyph.Bounds.Size}; height {glyph.Height}; minimum {glyph.MinHeight}; template {glyph.Template != null})");
            Click(row, Avalonia.Input.MouseButton.Right); Ok(changes == 0, "native tick-grid labels ignore right-click");
            Click(row); Ok(grid.Picked.SequenceEqual(new[] { "One" }) && changes == 1, "whole native tick-grid rows update ordered selection once");
            var remove = tag.GetVisualDescendants().OfType<IconButton>().Single(); Click(remove, Avalonia.Input.MouseButton.Right); Ok(removes == 0, "tag removal ignores right-click");
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(remove)!).Invoke(); Ok(removes == 1, "tag removal supports native invoke automation");
            var triggerCommand = new Command(); choice.Command = triggerCommand; Click(choice);
            Ok(triggerCommand.Runs == 1 && choice.Expanded, "single-picker triggers retain native Button commands");
            var peer = (Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(choice)!;
            peer.Collapse(); Settle();
            peer.Expand(); Settle();
            var actions = window.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is int).ToArray();
            Ok(actions.Length == 3 && actions[1].IsFocused && Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(actions[0]) is Avalonia.Automation.Peers.ButtonAutomationPeer, "single pickers focus the selected native menu action");
            Key(Avalonia.Input.Key.End); Key(Avalonia.Input.Key.Enter); Ok(choice.Current == "Three" && picks == 1 && !choice.Expanded && choice.IsFocused, "native menu keyboard selection closes, notifies once and restores its trigger");
            peer.Expand(); Settle(); Key(Avalonia.Input.Key.Tab); Ok(!choice.Expanded && after.IsFocused, "Tab leaves a single-picker popup and continues through the host");
            peer.Expand(); Settle(); choice.Items = new[] { "One" }; Settle(); Ok(!choice.Expanded && choice.Current == "Three", "replacing single-picker options closes stale actions and retains its explicit value");
            choice.Items = Array.Empty<string>(); choice.Extra = "Create"; int extra = 0; choice.ExtraPicked += () => extra++; peer.Expand(); Settle(); Key(Avalonia.Input.Key.Enter);
            Ok(extra == 1 && choice.Current == "Three", "an empty picker can expose its extra action without changing selection");
            choice.Items = new[] { "One", "Two" }; choice.Extra = null; peer.Expand(); Settle(); content.IsEnabled = false; Settle();
            Ok(!choice.Expanded, "parent disabling closes native picker actions"); content.IsEnabled = true;
            peer.Expand(); Settle(); content.Children.Remove(choice); Settle(); Ok(!choice.Expanded, "detaching a native picker releases its popup");
            window.Close();
            var masonry = new Masonry(); bool rejected = false; try { masonry.MinColumn = double.NaN; } catch (ArgumentException) { rejected = true; }
            Ok(rejected && masonry.MinColumn > 0, "masonry rejects invalid layout metrics before layout");
        }

        static int SearchChoiceScale()
        {
            var employees = Enumerable.Range(0, 10000).Select(id => new Employee(id, "Employee " + id, "Team " + id % 10)).ToArray();
            var picker = new SearchChoice { ItemsSource = employees, DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Employee.Name)), SearchMemberBinding = new Avalonia.Data.Binding(nameof(Employee.SearchText)), Width = 400 };
            var window = new Window { Width = 600, Height = 600, Content = new StackPanel { Children = { picker } } }; window.Show(); Dispatcher.UIThread.RunJobs();
            long start = GC.GetTotalAllocatedBytes(true); var clock = System.Diagnostics.Stopwatch.StartNew(); picker.IsDropDownOpen = true;
            Settle(); var list = window.GetVisualDescendants().OfType<ListBox>().Single(); int all = list.ItemCount;
            picker.SearchText = "employee 9999"; Settle(); int matches = list.ItemCount;
            picker.SearchText = ""; Settle(); list.ScrollIntoView(9999); Settle();
            int containers = list.GetVisualDescendants().OfType<ListBoxItem>().Count(), visuals = list.GetVisualDescendants().Count();
            Console.WriteLine($"SearchChoice: {all} items; {matches} exact-word result; {containers} realized rows; {visuals} visuals; {clock.ElapsedMilliseconds} ms including open/filter/scroll; {(GC.GetTotalAllocatedBytes(true) - start) / 1024 / 1024} MiB allocated");
            window.Close(); return all == 10000 && matches == 1 && containers is > 0 and < 80 && visuals < 1200 ? 0 : 1;
        }
    }
}
