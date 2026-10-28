// Popup checks.
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
        // Popup interaction coverage. This and the popup matrix in gallery/page-check.mjs exercise the same families: recheck the
        // whole matrix when shared popup ownership changes, since a fix to one picker does not prove the others.
        // Menu.OwnPopup owns native outside-click pass-through, optional trigger dismissal, focus leaving the owner/popup,
        // ancestor hide/disable, detach and subscription cleanup (web: watchPopup and ownList in docs/shade.js). Reused native
        // popups register ownership on each opening and release their handlers on each closing. Floating web lists also check
        // actual anchor movement: a scroll event queued before opening must not dismiss the new list, while moving its anchor
        // or resizing still does.
        //
        // Family                      Trigger and selection                      Focus and dismissal
        // ChoiceButton, Menu.Show     Trigger toggles; one pick closes. Context  Mouse opening has no keyboard ring; arrows reveal it.
        //                             menus open at the pointer.                 Escape restores the anchor; Tab/Shift+Tab leave toward the host.
        // MultiChoice                 Trigger toggles; checking keeps it open.   Empty selection means all. Pointer focus has no ring;
        //                                                                        keyboard/typeahead selection and checkbox automation stay native.
        // SearchChoice                Trigger toggles; committing closes.        The editable search header keeps focus. Filtering never changes
        //                                                                        the committed object/ID; search resets on reopening.
        // SuggestionField             Editing opens matches; clicking the        Keep the editor text/caret. Escape dismisses; only a pick
        //                             editor keeps editing.                      commits. Read-only input dismisses suggestions.
        // DateField, DateRangeField   Clicking a block opens the calendar or     Keep partial/invalid drafts and range picking. Tab may move
        //                             changes the active block; the icon toggles.between blocks before leaving; the calendar keeps its own cursor.
        // AccentPicker                Focusing/clicking Custom opens the helper; Colour changes apply live. Escape dismisses without reopening
        //                             clicking the editor keeps it open.         on focus restoration; Tab traverses the helper/editor targets.
        // ModeSwitch                  No popup; native radio segments apply.     Neutral outer frame; only keyboard focus has an accent ring.
        //
        // For each family cover repeated trigger clicks, outside click action/focus, Escape, Tab/Shift+Tab, hide, disable, detach and
        // reattachment; dismissal must not synthesize selection/edit events. Run native cases in both modes at 1x/2x text scale (browser:
        // both modes at 1x/2x zoom). Control-specific tests own bindings, commits, search/typeahead, automation, drafts and caret
        // preservation: keep them when moving shared mechanics. Headless checks do not replace real Windows pointer testing.
        // The browser matrix also writes docs/preview/{dark,light}/multi-choice-web.png (`node gallery/page-check.mjs`).
        static void PopupContracts()
        {
            object? Read(object obj, string name) => obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(obj);
            try
            {
                foreach (var scale in new[] { 1d, 2d })
                    foreach (var mode in new[] { Mode.Dark, Mode.Light })
                        foreach (var kind in new[] { "Choice", "Multi", "Search", "Suggestions", "Date", "Range", "Accent" })
                        {
                            Look.Configure(new ShadeOptions { TextScale = scale, ReducedMotion = true }); SetMode(mode);
                            Control control = kind switch
                            {
                                "Choice" => new ChoiceButton { Items = new[] { "Alpha", "Beta" }, Current = "Alpha" },
                                "Multi" => new MultiChoice(new[] { "Alpha", "Beta" }, "All"),
                                "Search" => new SearchChoice { ItemsSource = new[] { "Alpha", "Beta" }, SelectedItem = "Alpha" },
                                "Suggestions" => new SuggestionField("", new[] { "Alpha", "Alpine" }),
                                "Date" => new DateField { Value = new DateTime(2026, 10, 21) },
                                "Range" => new DateRangeField(),
                                _ => new AccentPicker()
                            };
                            control.Width = (kind == "Accent" ? 600 : 320) * scale;
                            int edits = 0;
                            switch (control)
                            {
                                case ChoiceButton choice: choice.Picked += _ => edits++; break;
                                case MultiChoice multi: multi.Changed += () => edits++; break;
                                case SearchChoice search: search.SelectionCommitted += (_, _) => edits++; break;
                                case SuggestionField suggestions: suggestions.Picked += _ => edits++; break;
                                case DateField date: date.Changed += () => edits++; break;
                                case DateRangeField range: range.Changed += () => edits++; break;
                                case AccentPicker accent: accent.Changed += () => edits++; break;
                            }
                            var before = new ActionButton("Before"); var outside = new ActionButton("Outside") { Margin = new Thickness(0, 800 * scale, 0, 0) };
                            int clicks = 0; outside.Click += (_, _) => clicks++;
                            var parent = new StackPanel { Children = { before, control, outside } };
                            var window = new Window { Width = 1200 * scale, Height = 1600 * scale, Content = parent }; window.Show();
                            void Click(Control target) { var at = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle(); }
                            Settle();
                            Control trigger = kind switch
                            {
                                "Suggestions" => ((SuggestionField)control).Box,
                                "Date" or "Range" => (Control)Read(Read(control, "calendar")!, "button")!,
                                "Accent" => ((Field)Read(control, "custom")!).Box,
                                _ => control
                            };
                            Avalonia.Controls.Primitives.Popup? Popup() => (Avalonia.Controls.Primitives.Popup?)Read(kind is "Date" or "Range" ? Read(control, "calendar")! : control, kind == "Accent" ? "helper" : "popup");
                            void Open()
                            {
                                Click(trigger);
                                if (control is SuggestionField suggestions) { suggestions.Box.Text = ""; Settle(); suggestions.Box.Text = "Al"; Settle(); }
                            }
                            string name = $"{kind} {mode} {scale}x";
                            Open(); Ok(Popup()?.IsOpen == true, name + ": pointer opening creates a popup");
                            Click(trigger);
                            bool toggles = kind is not ("Suggestions" or "Accent");
                            Ok((Popup()?.IsOpen == true) != toggles, name + ": repeated trigger click preserves the control's toggle/editor contract");
                            if (toggles) Open();
                            Click(outside);
                            Ok(Popup()?.IsOpen != true && outside.IsFocused && clicks == 1, name + ": outside click closes without stealing focus or swallowing the action");
                            Open(); window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                            Ok(Popup()?.IsOpen != true, name + ": Escape dismisses without selecting");
                            foreach (var backwards in new[] { false, true })
                            {
                                Open(); var target = backwards ? before : outside;
                                for (int step = 0; step < 32 && !target.IsFocused; step++)
                                {
                                    var modifiers = backwards ? Avalonia.Input.RawInputModifiers.Shift : Avalonia.Input.RawInputModifiers.None;
                                    window.KeyPress(Avalonia.Input.Key.Tab, modifiers, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(Avalonia.Input.Key.Tab, modifiers, Avalonia.Input.PhysicalKey.None, null); Settle();
                                }
                                Ok(target.IsFocused && Popup()?.IsOpen != true, name + ": " + (backwards ? "Shift+Tab" : "Tab") + " leaves toward the host without trapping focus");
                            }
                            foreach (var state in new[] { "disable", "hide", "detach" })
                            {
                                Open(); var shown = Popup();
                                if (state == "disable") parent.IsEnabled = false;
                                else if (state == "hide") parent.IsVisible = false;
                                else parent.Children.Remove(control);
                                Settle(); Ok(shown?.IsOpen == false, name + ": " + state + " releases the popup");
                                parent.IsEnabled = true; parent.IsVisible = true;
                                if (state == "detach") parent.Children.Insert(1, control);
                                Settle();
                            }
                            Open(); var reopened = Popup(); Click(outside);
                            Ok(reopened?.IsOpen == false && clicks == 2, name + ": reopening after reattachment has no stale click handlers");
                            Ok(edits == 0, name + ": dismissal and lifecycle changes do not commit selection or duplicate edits");
                            window.Close();
                        }
            }
            finally { Look.Configure(new ShadeOptions()); SetMode(Mode.Dark); }
        }

        static void IntegrationFeedback()
        {
            void Key(Window w, Avalonia.Input.Key key) { w.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); w.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            void Click(Window w, Control c) { var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value; w.MouseDown(p, Avalonia.Input.MouseButton.Left); w.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle(); }
            T FieldOf<T>(object obj, string name) => (T)obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(obj)!;
            var choice = new ChoiceButton { Items = new[] { "One", "Two" }, Current = "One" }; var next = new TextBox();
            var w = new Window { Width = 500, Height = 500, Content = new StackPanel { Children = { choice, next } } }; w.Show(); Settle();
            Control FocusedRow() => w.GetVisualDescendants().OfType<Control>().First(b => b.IsFocused && (b is ComboBoxItem || b is Button { Tag: int }));
            int picked = 0; choice.Picked += _ => picked++;
            Click(w, choice); Ok(!FocusedRow().Classes.Contains(":focus-visible"), "pointer-opened selected menus keep native focus without a keyboard outline");
            Key(w, Avalonia.Input.Key.Down); Ok(FocusedRow().Classes.Contains(":focus-visible"), "arrows after a pointer opening reveal keyboard focus");
            Key(w, Avalonia.Input.Key.Enter); Ok(picked == 1 && choice.Current == "Two", "keyboard activation after mouse opening selects exactly once");
            Key(w, Avalonia.Input.Key.Down); Ok(FocusedRow().Classes.Contains(":focus-visible"), "keyboard-opened pickers show their focus outline");
            Key(w, Avalonia.Input.Key.Escape); Ok(choice.IsFocused && choice.Classes.Contains(":focus-visible"), "keyboard dismissal restores keyboard focus modality");
            choice.Current = null; Click(w, choice); Ok(!FocusedRow().Classes.Contains(":focus-visible"), "pointer-opened menus without selection do not force keyboard modality");
            Key(w, Avalonia.Input.Key.Tab); Ok(next.IsFocused, "Tab still leaves the shared popup toward the next host field");
            var context = Shade.Menu.Show(choice, new[] { "Open", "Delete" }, _ => picked++, atPointer: true); Settle();
            Ok(!FocusedRow().Classes.Contains(":focus-visible"), "shared pointer context menus also preserve pointer modality"); Key(w, Avalonia.Input.Key.Escape);
            Click(w, choice); Click(w, FocusedRow()); Key(w, Avalonia.Input.Key.Space);
            Ok(FocusedRow().Classes.Contains(":focus-visible"), "Space after a pointer selection opens with keyboard focus indication");
            Key(w, Avalonia.Input.Key.Escape); w.Close();

            foreach (var scale in new[] { 1d, 1.5d })
                foreach (var mode in new[] { Mode.Dark, Mode.Light })
                    foreach (var markKind in new[] { "default", "hidden", "custom" })
                    {
                        Look.Configure(new ShadeOptions { TextScale = scale, ReducedMotion = true }); SetMode(mode);
                        Control? mark = markKind == "default" ? null : new Border { IsVisible = markKind != "hidden" };
                        var sidebar = new Sidebar("Potraviny levně", "Objednávky", new[] { new NavEntry("Home", "home", Icons.Monitor) }, mark) { ExpandedWidth = 260 * scale };
                        var host = new Window { Width = 900, Height = 700, Content = Shell.Layout(sidebar, new Border(), new StatusBar()) }; host.Show(); Settle();
                        var title = sidebar.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Potraviny levně");
                        var brandBounds = title.TranslatePoint(default, sidebar)!.Value;
                        var logo = FieldOf<Panel>(sidebar, "markHost"); var logoAt = logo.TranslatePoint(default, sidebar)!.Value;
                        Ok(title.TextLayout.TextLines.All(line => !line.HasCollapsed) && (markKind == "hidden" || brandBounds.X + title.Bounds.Width <= logoAt.X), $"{mode}, {scale}, {markKind}: full localized branding fits without logo overlap");
                        sidebar.Collapsed = true; sidebar.Collapsed = false; Settle();
                        Ok(sidebar.Width == 260 * scale, "configured sidebar width survives collapse and expand");
                        sidebar.ExpandedWidth = 180 * scale; Settle(); Ok(title.TextLayout.TextLines.All(line => !line.HasCollapsed) && title.Bounds.Height > TextSize.Title, "default-width branding wraps without silently shrinking");
                        host.Close();
                    }
            Look.Configure(new ShadeOptions()); SetMode(Mode.Dark);
            var animated = new Sidebar("Long localized business name", "Workspace", Array.Empty<NavEntry>()) { ExpandedWidth = 280 };
            var animatedHost = new Window { Width = 600, Height = 400, Content = animated }; animatedHost.Show(); Settle();
            animated.Collapsed = true; animated.Collapsed = false; System.Threading.Thread.Sleep(450); Settle();
            Ok(animated.Width == 280 && !animated.Collapsed, "rapid sidebar reversal settles at the configured expanded width"); animatedHost.Close();

            var c = new EditableTable.Column("n", "Quantity", EditableTable.Kind.Unit) { Units = new[] { "pcs" }, Width = 60, MaxWidth = 60, HeadingSize = 18, SubheadingSize = 14, Subtitle = "Amount", Italic = true };
            var r = new EditableTable.Row(); r.Values["n"] = "12";
            var table = new EditableTable { Columns = new List<EditableTable.Column> { c }, Rows = new List<EditableTable.Row> { r }, Design = true };
            var outside = new TextBox(); DockPanel.SetDock(outside, Dock.Bottom);
            var tableHost = new Window { Width = 500, Height = 500, Content = new DockPanel { Children = { outside, table } } }; tableHost.Show(); Settle();
            var footGrid = FieldOf<Grid>(table, "footGrid");
            var foot = footGrid.GetVisualDescendants().OfType<TextBlock>().Single();
            Ok(foot.Inlines!.OfType<Avalonia.Controls.Documents.Run>().All(run => string.IsNullOrEmpty(run.Text)) && foot.TextTrimming == TextTrimming.None, "None footers lay out no transparent caption or truncation marker");
            var footerAt = foot.TranslatePoint(new Point(10, 8), tableHost)!.Value; tableHost.MouseMove(footerAt); Settle();
            Ok(foot.Inlines!.OfType<Avalonia.Controls.Documents.Run>().First().Text == "ƒ", "narrow None footer hover uses a compact calculation affordance");
            tableHost.MouseMove(new Point(490, 490)); Settle();
            Ok(foot.Inlines!.OfType<Avalonia.Controls.Documents.Run>().First().Text == "", "calculation affordance disappears without leaving text or ellipsis");
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Quantity" && t.FontSize == 18) && table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Amount" && t.FontSize == 14), "per-column heading and subtitle sizes override table defaults");
            var value = table.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "12");
            Ok(value.FontStyle == FontStyle.Italic, "per-column italic cells use native text styling");
            var editAt = value.TranslatePoint(new Point(4, 8), tableHost)!.Value; tableHost.MouseDown(editAt, Avalonia.Input.MouseButton.Left); tableHost.MouseUp(editAt, Avalonia.Input.MouseButton.Left); Settle(); var editor = table.GetVisualDescendants().OfType<TextBox>().Single(t => t.IsFocused);
            Ok(editor.FontStyle == FontStyle.Italic, "the cell editor preserves italic typography");
            editor.Text = "letters"; Settle();
            var ring = FieldOf<Border>(table, "ring");
            Ok(r.Values["n"] == "12" && table.LastValidationError == "Enter a number" && DataValidationErrors.GetHasErrors(editor) && ring.BorderBrush == B(Danger), "invalid numeric drafts retain the valid model and show the native validation border");
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Text == "Enter a number" && t.IsEffectivelyVisible) == 1, "numeric errors have one visible message outside the cell, without a duplicate template message");
            Key(tableHost, Avalonia.Input.Key.Tab); Ok(editor.IsFocused && editor.Text == "letters", "invalid numeric drafts cannot disappear through Tab");
            SetMode(Mode.Light); Settle(); Ok(editor.Text == "letters" && table.LastValidationError != null, "live theme changes retain the invalid draft and its error");
            outside.Focus(); Settle();
            SetMode(Mode.Dark); Settle(); Ok(!editor.IsFocused && editor.Text == "letters" && table.LastValidationError != null, "invalid drafts survive blur and live theme changes without stealing focus");
            editor.Focus();
            editor.Text = "12"; Settle(); Ok(!DataValidationErrors.GetHasErrors(editor) && table.LastValidationError == null, "correcting back to the existing value clears numeric validation");
            editor.Text = "bad"; Key(tableHost, Avalonia.Input.Key.Escape); Ok(r.Values["n"] == "12" && table.LastValidationError == null, "Escape cancels an invalid draft without overwriting the saved value");
            var showDrop = typeof(EditableTable).GetMethod("ShowDrop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            foreach (int count in new[] { 0, 2, 100 })
            {
                table.Rows = Enumerable.Range(0, count).Select(_ => new EditableTable.Row()).ToList(); Settle();
                showDrop.Invoke(table, new object[] { 0, 1, "Quantity", new Point(20, 20) }); Settle();
                var marker = FieldOf<Border>(table, "line"); var overlay = FieldOf<Canvas>(table, "overlay"); var footer = FieldOf<Grid>(table, "footGrid");
                double bottom = footer.TranslatePoint(new Point(0, footer.Bounds.Height), overlay)!.Value.Y;
                Ok(Math.Abs(Canvas.GetTop(marker) + marker.Height - bottom) < 0.01, $"{count} rows: reorder marker ends at the rendered footer in a tall host");
            }
            tableHost.Close(); SetMode(Mode.Dark);

            var label = new TextBlock(); Build.Mark(label, "Malá Malá", "lá alá");
            Ok(label.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Where(run => run.Foreground == B(AccentText)).Select(run => run.Text).SequenceEqual(new[] { "alá", "alá" }), "accent-insensitive highlighting merges overlapping word matches without duplicating text");
            Build.Mark(label, "🙂 Mala\u0301", "lá");
            Ok(label.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Any(run => run.Text == "la\u0301" && run.Foreground == B(AccentText)), "highlighting retains decomposed accents and original Unicode positions");
            var small = new AccentPicker(); var grid = new ColorGrid(); grid.Bind(ColorGrid.ValueProperty, new Avalonia.Data.Binding("Color") { Source = B(Accent) });
            var colors = new Window { Content = new StackPanel { Children = { small, grid } } }; colors.Show(); Settle();
            small.Value = "Pink"; Settle(); Ok(grid.Value == Accents[4].dark, "bound color helpers follow the settings accent picker");
            SetAccent(Accents[6].dark, Accents[6].light); small.Restyle(); Settle();
            Ok(small.Value == "Lime" && grid.Value == Accents[6].dark, "external color-helper picks update the compact picker and its selected value");
            SetMode(Mode.Light); small.Restyle(); Settle(); Ok(grid.Value == Accents[6].light && small.Value == "Lime", "synchronized accent controls retain curated identity across modes");
            colors.Close(); SetMode(Mode.Dark); SetAccent(Accents[0].dark, Accents[0].light);
        }
    }
}
