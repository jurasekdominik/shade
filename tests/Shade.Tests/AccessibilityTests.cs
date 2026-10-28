// Accessibility checks.
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
        static void AccessibleEditors()
        {
            Avalonia.Automation.Peers.AutomationPeer Peer(Control control) => Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(control)!;
            var date = new DateField { Min = new DateTime(2026, 1, 1), Max = new DateTime(2026, 12, 31) };
            var datePeer = (Avalonia.Automation.Provider.IValueProvider)Peer(date); int changes = 0; date.Changed += () => changes++;
            datePeer.SetValue("2026-10-25");
            bool rejected = false; try { datePeer.SetValue("2027-01-01"); } catch (ArgumentException) { rejected = true; }
            Ok(datePeer.Value == "2026-10-25" && changes == 1 && rejected, "date automation reads and edits the date while enforcing limits");
            Build.Labelled("Delivery", date);
            Ok(Avalonia.Automation.AutomationProperties.GetLabeledBy(date) != null, "date labels target the accessible date rather than its unused text box");
            date.IsReadOnly = true; rejected = false;
            try { datePeer.SetValue("2026-10-26"); } catch (InvalidOperationException) { rejected = true; }
            Ok(rejected && datePeer.IsReadOnly, "date automation respects read-only state");
            var range = new DateRangeField { Min = date.Min, Max = date.Max };
            var rangePeer = (Avalonia.Automation.Provider.IValueProvider)Peer(range);
            rangePeer.SetValue("2026-10-25/2026-10-28");
            rejected = false; try { rangePeer.SetValue("2026-10-25/2027-01-01"); } catch (ArgumentException) { rejected = true; }
            Ok(rejected && rangePeer.Value == "2026-10-25/2026-10-28", "invalid range automation edits leave both stored dates unchanged");
            var icon = new IconButton(IconButton.Export); int clicks = 0; icon.Click += (_, _) => clicks++;
            ((Avalonia.Automation.Provider.IInvokeProvider)Peer(icon)).Invoke();
            Ok(clicks == 1 && icon.Focusable, "icon actions expose invoke automation and keyboard focus");
            var state = new StateToggle(); var statePeer = (Avalonia.Automation.Provider.IToggleProvider)Peer(state);
            Ok(statePeer.ToggleState == Avalonia.Automation.Provider.ToggleState.Indeterminate, "unknown state exposes an indeterminate value");
            statePeer.Toggle();
            Ok(state.IsChecked == true, "state automation follows the same toggle path as the pointer");
            var keys = new HashSet<string>();
            Look.Translate = key => { keys.Add(key); return key; };
            try
            {
                var drawing = new Avalonia.Media.DrawingGroup(); using var context = drawing.Open();
                var shown = new StateLook(); shown.Arrange(new Rect(0, 0, 100, 28)); shown.Render(context);
                state.Arrange(new Rect(0, 0, 100, 28)); state.Render(context);
                Ok(keys.Contains("OFF") && keys.Contains("ON"), "drawn on/off labels use the application localizer");
            }
            finally { Look.Translate = null; }
            var number = new EditableTable.Column("n", "Number", EditableTable.Kind.Number);
            var row = new EditableTable.Row(); row.Values["n"] = "2";
            var table = new EditableTable { Columns = new[] { number }, Rows = new[] { row } };
            var window = new Window { Content = table }; window.Show(); Dispatcher.UIThread.RunJobs();
            var cell = table.GetVisualDescendants().OfType<Border>().First(c => c.GetType().Name == "DataCell");
            var cellPeer = (Avalonia.Automation.Provider.IValueProvider)Peer(cell);
            cellPeer.SetValue("3.5"); rejected = false;
            try { cellPeer.SetValue("bad"); } catch (ArgumentException) { rejected = true; }
            Ok(rejected && cellPeer.Value == "3.5" && row.Values["n"] == "3.5", "cell automation uses validated edits and preserves stored values on failure");
            var other = new EditableTable.Row();
            Ok(!table.TrySetValue(other, number, "1") && table.LastValidationError == null, "an unknown row does not reuse an unrelated validation error");
            window.Close();
        }

        /// <summary>Role, name and pattern of the native controls Shade themes and of the ones that still draw by hand: a screen
        /// reader gets the same answers whatever the look.</summary>
        static void NativeAutomation()
        {
            Avalonia.Automation.Peers.AutomationPeer Peer(Control c) => Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(c)!;
            var check = new CheckBox { Content = "Include headers", IsChecked = null }; var toggle = new ToggleSwitch { Content = "Dark theme" };
            var bar = new ProgressBar { Maximum = 1, Value = .25 }; Avalonia.Automation.AutomationProperties.SetName(bar, "Upload");
            var state = new StateToggle { IsChecked = null }; Avalonia.Automation.AutomationProperties.SetName(state, "Pump");
            var date = new DateField { Value = new DateTime(2026, 10, 25) }; Avalonia.Automation.AutomationProperties.SetName(date, "Start");
            var colour = new ColorGrid(); Avalonia.Automation.AutomationProperties.SetName(colour, "Accent colour");
            var window = new Window { Width = 600, Height = 500, Content = new StackPanel { Children = { check, toggle, bar, state, date, colour } } }; window.Show(); Settle();
            Ok(Peer(check).GetAutomationControlType() == Avalonia.Automation.Peers.AutomationControlType.CheckBox && Peer(check).GetName() == "Include headers", "a check box is a check box named by its Content");
            var checkToggle = (Avalonia.Automation.Provider.IToggleProvider)Peer(check);
            Ok(checkToggle.ToggleState == Avalonia.Automation.Provider.ToggleState.Indeterminate, "a mixed check box reports the indeterminate state");
            checkToggle.Toggle(); Ok(check.IsChecked == false && checkToggle.ToggleState == Avalonia.Automation.Provider.ToggleState.Off, "toggling a mixed check box follows Avalonia's cycle");
            var switchToggle = (Avalonia.Automation.Provider.IToggleProvider)Peer(toggle);
            Ok(Peer(toggle).GetName() == "Dark theme" && switchToggle.ToggleState == Avalonia.Automation.Provider.ToggleState.Off, "a switch is named by its Content and exposes the toggle pattern");
            switchToggle.Toggle(); Ok(toggle.IsChecked == true && switchToggle.ToggleState == Avalonia.Automation.Provider.ToggleState.On, "toggling a switch by automation turns it on");
            var range = (Avalonia.Automation.Provider.IRangeValueProvider)Peer(bar);
            Ok(Peer(bar).GetAutomationControlType() == Avalonia.Automation.Peers.AutomationControlType.ProgressBar && Peer(bar).GetName() == "Upload" && range.Value == .25 && range.Maximum == 1, "a progress bar is a progress bar with its name and range");
            Ok(((Avalonia.Automation.Provider.IToggleProvider)Peer(state)).ToggleState == Avalonia.Automation.Provider.ToggleState.Indeterminate && Peer(state).GetName() == "Pump", "a state toggle named by the app reports unknown as indeterminate");
            Ok(Peer(date).GetName() == "Start" && ((Avalonia.Automation.Provider.IValueProvider)Peer(date)).Value == "2026-10-25", "a date field has its name and its ISO value");
            var colourPeer = Peer(colour); var colourValue = (Avalonia.Automation.Provider.IValueProvider)colourPeer;
            Ok(colourPeer.GetName() == "Accent colour" && colourValue.Value == HexOf(colour.Value), "the colour helper has a name and its hex value");
            colourValue.SetValue("#336699"); Ok(colour.Value == Color.Parse("#336699"), "setting the colour helper's value by automation changes the colour");
            bool refused = false; try { colourValue.SetValue("not a colour"); } catch (ArgumentException) { refused = true; }
            Ok(refused, "the colour helper refuses an unparseable value"); window.Close();
        }

        static void SummaryAndCalendarAutomation()
        {
            var cards = new SummaryCards { Items = new[] { new SummaryCards.Item { Key = "a", Title = "A", Value = "1" }, new SummaryCards.Item { Key = "b", Title = "B", Value = "2" } } };
            string? picked = null; cards.Picked += key => picked = key;
            var date = new DateField { Value = new DateTime(2026, 10, 25) };
            var window = new Window { Width = 800, Height = 600, Content = new StackPanel { Children = { cards, date } } }; window.Show();
            Settle();
            var buttons = cards.Children.OfType<Button>().ToArray();
            Ok(cards.GetVisualDescendants().OfType<Control>().Where(c => c.GetType().Name == "CardFace").All(c => c.Bounds.Width > 0 && c.Bounds.Height >= 84), "summary drawing surfaces receive the full visible card bounds");
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(buttons[1])!).Invoke();
            Ok(picked == "b", "summary automation invokes the same stable card key as the pointer");
            buttons[0].Focus(Avalonia.Input.NavigationMethod.Tab);
            window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(buttons[1].IsFocused, "summary cards support directional keyboard navigation");
            cards.Items = new[] { new SummaryCards.Item { Key = "b", Title = "B", Value = "3" } }; Settle();
            Ok(cards.Children.OfType<Button>().Single().IsFocused, "summary refresh preserves keyboard focus by card key");
            var blocks = date.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "DateBlocks"); blocks.Focus();
            window.KeyPress(Avalonia.Input.Key.F4, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            var calendar = window.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "MonthGrid");
            string day = new DateTime(2026, 10, 26).ToString("D", Look.Culture);
            var target = calendar.GetVisualDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == day);
            Ok(target.Bounds.Width > 0 && target.Bounds.Height > 0, "calendar days expose named automation targets at their visible bounds");
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(target)!).Invoke(); Settle();
            Ok(date.Value == new DateTime(2026, 10, 26), "a calendar day can be selected through native invoke automation");
            window.Close();
        }

        static void FocusContrastAndTabSemantics()
        {
            try
            {
                foreach (var mode in new[] { Look.Mode.Light, Look.Mode.Dark })
                    foreach (var accent in new[] { Colors.White, Colors.Black })
                    {
                        Look.SetMode(mode); Look.SetAccent(accent);
                        Ok(Look.Contrast(Look.FocusBorder, Look.Raised) >= 3 && Look.Contrast(Look.FocusBorder, Look.Background) >= 3, "focus borders remain visible with extreme custom accents in " + mode);
                    }
            }
            finally { Look.SetAccent(Look.Accents[0].dark, Look.Accents[0].light); Look.SetMode(Look.Mode.Dark); }
            var tabs = new TextTabs("A", "B"); int changes = 0; tabs.Changed += _ => changes++;
            tabs.SetValue(TextTabs.ActiveProperty, 1);
            var peer = (Avalonia.Automation.Provider.ISelectionProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(tabs)!;
            Ok(changes == 0 && peer.IsSelectionRequired && !peer.CanSelectMultiple && peer.GetSelection().Count == 1, "tab bindings update selection without reporting a user action");
            var selected = (Avalonia.Automation.Provider.ISelectionItemProvider)peer.GetSelection()[0];
            Ok(selected.IsSelected && ReferenceEquals(selected.SelectionContainer, peer), "tab items expose their selected state and selection container");
            var first = tabs.GetVisualDescendants().OfType<StackPanel>().First(c => c.Focusable);
            ((Avalonia.Automation.Provider.ISelectionItemProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(first)!).Select();
            Ok(tabs.Active == 0 && changes == 1, "tab automation raises the same selection event as a user action");
            var button = new ActionButton("Disabled") { Command = new Command { Can = false } };
            var disabledWindow = new Window { Content = button }; disabledWindow.Show(); Dispatcher.UIThread.RunJobs();
            Ok(!button.IsEffectivelyEnabled && button.Background is SolidColorBrush background && background.Color == (Color)Look.Raised, "command-disabled actions use the disabled visual state"); disabledWindow.Close();
        }

        /// <summary>The focus with a pointer: a click on the background leaves a field; a dropdown neither moves nor shows the
        /// keyboard's focus ring when its list opens and closes by the pointer.</summary>
        static void Focus()
        {
            var field = new Field("Name") { Width = 200 };
            var choice = new ChoiceButton { Width = 200 }; choice.SetItems(new[] { "One", "Two", "Three" }, "One");
            var panel = new StackPanel { Background = Brushes.Transparent, Spacing = 8, Margin = new Thickness(16), Children = { field, choice } };
            var window = new Window { Width = 400, Height = 300, Content = panel }; window.Show();
            void Click(Point p) { Settle(); window.MouseDown(p, Avalonia.Input.MouseButton.Left); window.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle(); }
            Point At(Control c, double x, double y) => c.TranslatePoint(new Point(x, y), window)!.Value;
            Settle();

            Click(At(field, 100, 14)); Ok(field.Box.IsFocused, "a click on a field focuses it");
            Click(At(field, 3, 14)); Ok(field.Box.IsFocused, "a click on the field's edge keeps it focused");
            Click(new Point(300, 250)); Ok(!field.Box.IsFocused, "a click on the background leaves the field");

            var label = choice.GetVisualDescendants().OfType<TextBlock>().First();
            var before = label.TranslatePoint(new Point(0, 0), window)!.Value;
            Click(At(choice, 100, 14));
            var open = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "Three");
            Ok(open != null, "a click opens the dropdown's list");
            Ok(label.TranslatePoint(new Point(0, 0), window)!.Value == before, "the dropdown's text stays put while its list is open");
            if (open != null) Click(At(open, 4, 4));
            Ok(choice.Current == "Three" && choice.IsFocused, "a pick sets the dropdown and gives it the focus back");
            Ok(choice.BorderBrush is ISolidColorBrush b && b.Color != Accent.Color, "after a pick by the pointer no focus ring shows");
            Click(At(choice, 100, 14)); Ok(window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two"), "the list opens again");
            Settle(); window.MouseDown(At(choice, 100, 14), Avalonia.Input.MouseButton.Left); Settle();  // the closing press, as Windows delivers it: no move or hover update after it
            Ok(!window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two") && choice.BorderBrush is ISolidColorBrush h && h.Color == Outline.Color,
                "closed by a click on it, the dropdown keeps its hover border at once (no resting border until the pointer moves)");
            window.MouseUp(At(choice, 100, 14), Avalonia.Input.MouseButton.Left); Settle();
            Ok(!window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two"), "the closing click's release doesn't open the list again");
            window.MouseMove(new Point(380, 280)); Settle();
            Ok(choice.BorderBrush is ISolidColorBrush rest && rest.Color != Outline.Color, "moved away in one jump, the dropdown drops its hover border");
            window.Close();
        }
    }
}
