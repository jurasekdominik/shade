// Shade - shared control: EditableTable's cell editor: one box moved onto the cell being edited, its validation and focus.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade
{
    public sealed partial class EditableTable
    {
        // ---- the editor -----------------------------------------------------------------------------------------------

        void ShowValidation()
        {
            validationMessage.Text = LastValidationError;
            validationMessage.IsVisible = LastValidationError != null;
            ring.BorderBrush = B(LastValidationError != null ? Danger : FocusBorder);
            ToolTip.SetTip(editor, LastValidationError);
            Avalonia.Automation.AutomationProperties.SetHelpText(editor, LastValidationError ?? "");
            DataValidationErrors.SetErrors(editor, LastValidationError == null ? null : new[] { LastValidationError });
        }

        void Editor()
        {
            editor.TextWrapping = TextWrapping.Wrap; editor.AcceptsReturn = false; editor.VerticalAlignment = VerticalAlignment.Stretch;
            editor.VerticalContentAlignment = VerticalAlignment.Top; editor.ZIndex = 1;
            editor.FocusAdorner = null;
            editor.Styles.Add(new Style(x => x.OfType<TextBox>().Template().OfType<DataValidationErrors>())
            {
                // The table owns the error message; Fluent's error presenter reserves space even with empty content.
                Setters = { new Setter(DataValidationErrors.TemplateProperty, new FuncControlTemplate<DataValidationErrors>((errors, _) =>
                    new ContentPresenter { [!ContentPresenter.ContentProperty] = errors[!ContentControl.ContentProperty] })) }
            });
            editor.ContextFlyout = null; editor.ContextMenu = null;
            // written as it changes (not on TextChanged, which comes later): a key and a Tab straight after it can't lose the key
            editor.PropertyChanged += (s, e) =>
            {
                if (e.Property != TextBox.TextProperty || editing == null || restoringDraft) return;
                var c = columns[editCol]; var t = editor.Text ?? "";
                var active = editing;
                if (!TrySetValue(active.Row, c, t)) { ShowValidation(); return; }
                if (editing != active) return;
                ShowValidation();
                editing.Texts[editCol].Text = t;
                editor.TextAlignment = editing.Texts[editCol].TextAlignment = Alignment(editing.Row, c, t);
            };
            editor.AddHandler(KeyDownEvent, (s, e) =>
            {
                if (editing == null) return;
                bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (e.Key == Key.Escape && LastValidationError != null) { e.Handled = true; Park(); return; }
                if (LastValidationError != null && e.Key is Key.Enter or Key.Tab) { e.Handled = true; return; }
                if (e.Key == Key.Enter)
                {
                    int at = shown.IndexOf(editing) + (shift ? -1 : 1);  // no new lines: Enter goes down the column, Shift+Enter up
                    if (at >= 0 && at < shown.Count) Edit(shown[at], editCol);
                }
                else if (e.Key == Key.Tab && Next(shift) is var (l, i) && l != null) Edit(l, i);
                else if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt && editing.Units[editCol] != null) Cycle(editing, editCol);
                else if (e.Key == Key.Apps || e.Key == Key.F10 && shift) RowMenu(editing, editing.Cells[editCol], false);
                else return;
                e.Handled = true;
            }, RoutingStrategies.Tunnel);
        }

        /// <summary>The editable cell after (or before) the one being edited, across then down; none at either end (Tab leaves).</summary>
        (Line? line, int col) Next(bool back)
        {
            int n = columns.Count, at = shown.IndexOf(editing!) * n + editCol;
            for (int k = at + (back ? -1 : 1); k >= 0 && k < shown.Count * n; k += back ? -1 : 1)
                if (Editable(columns[k % n])) return (shown[k / n], k % n);
            return (null, 0);
        }

        /// <summary>The editor moves to a cell: at a point, the caret goes where it was clicked; from the keyboard, the value is selected.</summary>
        void Edit(Line l, int col, Point? at = null)
        {
            if (editing != null && LastValidationError != null) { editor.Focus(); return; }
            var c = columns[col];
            EnsureVisible(l);
            Unedit();
            editing = l; editCol = col; editKey = c.Key;
            LastValidationError = null; validationKey = null;
            ShowValidation();
            var t = l.Texts[col];
            editor.Text = l.Row.Values.GetValueOrDefault(c.Key) ?? "";
            editor.FontSize = valueSize; editor.FontStyle = c.Italic ? FontStyle.Italic : FontStyle.Normal; editor.FontWeight = c.Bold ? FontWeight.SemiBold : FontWeight.Normal; editor.TextAlignment = t.TextAlignment;
            // the editor's text sits exactly on the cell's: same padding, clear of the grid lines and of the unit (still clickable)
            bool unit = l.Units[col] != null;
            editor.Margin = new Thickness(0, 0, 1 + (unit ? rooms[col] : 0), 1); editor.Padding = new Thickness(8, 4, unit ? 0 : rooms[col], 4);
            Avalonia.Automation.AutomationProperties.SetName(editor, c.Title);
            Grid.SetColumn(editor, col + 1); Move(editor, l.Panel);
            t.Text = editor.Text; Face(l, col, true);  // under the editor, the text as typed: the row keeps the editor's height
            editor.Focus(at == null ? NavigationMethod.Tab : NavigationMethod.Pointer);
            int len = (editor.Text ?? "").Length;
            if (at is Point p && t.TextLayout is { } layout)
            {
                int caret = Math.Clamp(layout.HitTestPoint(new Point(p.X - t.Padding.Left, p.Y - t.Padding.Top)).TextPosition, 0, len);
                editor.SelectionStart = editor.SelectionEnd = editor.CaretIndex = caret;
            }
            else { editor.SelectionStart = 0; editor.SelectionEnd = editor.CaretIndex = len; }
            Settle();
            Current(c.Key);
        }

        /// <summary>The cell being edited shows its text again, as Display shows it.</summary>
        void Unedit()
        {
            if (editing == null || editCol >= editing.Texts.Length) return;
            Face(editing, editCol, false);
            var column = columns.FirstOrDefault(c => c.Key == editKey);
            if (column != null) editing.Texts[editCol].Text = Shown(editing.Row, column);
        }

        /// <summary>The editor leaves its cell (the cell's text shows again).</summary>
        void Park(bool layout = true)
        {
            if (editing == null) return;
            Unedit();
            editing = null;
            Move(editor, null);
            LastValidationError = null; validationKey = null; ShowValidation();
            if (layout) Layout();  // structural rebuilds replace the width caches before measuring again
        }

        /// <summary>After focus moves: the outline goes to the focused cell or hides; an editor that lost the keyboard leaves.</summary>
        void Settle()
        {
            var f = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            if (editing != null && f != editor && LastValidationError == null) Park();
            var cell = editing != null && LastValidationError != null || f == editor ? editor : f?.Parent is Grid { Tag: Line } ? f : null;
            ring.IsVisible = cell != null;
            if (cell == null) return;
            Grid.SetColumn(ring, Grid.GetColumn(cell)); Move(ring, (Grid)cell.Parent!);
            if (Grid.GetColumn(cell) - 1 is int i && i >= 0 && i < columns.Count) Current(columns[i].Key);
        }

        void Cycle(Line l, int col)
        {
            var c = columns[col]; var units = c.Units.ToList(); var next = units[(units.IndexOf(UnitOf(l.Row, c)) + 1) % units.Count];
            l.Row.Units[c.Key] = next; l.Units[col]!.Content = next; Recount(l); Changed?.Invoke();
        }

        void FocusCell(Row row, string key)
        {
            int i = columns.ToList().FindIndex(c => c.Key == key);
            if (i < 0 || !lines.TryGetValue(row, out var l)) return;
            if (Editable(columns[i])) Edit(l, i); else { EnsureVisible(l); l.Cells[i].Focus(NavigationMethod.Tab); }
        }

        void Current(string key)
        {
            if (current == key) return;
            current = key;
            for (int i = 0; i < heads.Count && i < columns.Count; i++) HeadBack(i);
            SyncColumnEditor();
            CurrentColumnChanged?.Invoke(CurrentColumn);
        }

        /// <summary>After an edit: the row's computed cells, the footers and the widths, without drawing the table again (the caret stays).</summary>
        void Recount(Line l)
        {
            footerText.Clear();
            l.Forget();
            for (int i = 0; l.Realized && i < columns.Count; i++) { if (columns[i].Kind == Kind.Computed) l.Texts[i].Text = Shown(l.Row, columns[i]); Retemplate(l, i); }
            Totals(); Layout();
        }

        void Totals()
        {
            foreach (var c in columns) if (totals.TryGetValue(c.Key, out var f)) f.Text = ShownFooter(c);
            foreach (var (c, face) in footFaces) face.Child = c.FooterTemplate?.Build(c) ?? face.Child;
        }

        /// <summary>A templated cell's content, built again from its row; the text stays under it unless the editor is open.</summary>
        void Retemplate(Line l, int i)
        {
            if (i >= columns.Count || columns[i].CellTemplate is not { } template || l.Cells[i]?.Child is not Panel face) return;
            if (face.Children.Count == 2) face.Children.RemoveAt(1);
            if (template.Build(l.Row) is Control content) face.Children.Add(new Border { Padding = new Thickness(8, 4), Child = content });
            Face(l, i, l == editing && i == editCol);
        }
        /// <summary>The text of a cell shows unless the editor is on it or the app's content covers it, which hides while editing.</summary>
        void Face(Line l, int i, bool editorOpen)
        {
            var content = i < columns.Count && columns[i].CellTemplate != null && l.Cells[i]?.Child is Panel { Children.Count: 2 } face ? face.Children[1] : null;
            l.Texts[i].Opacity = editorOpen || content != null ? 0 : 1;
            if (content != null) content.IsVisible = !editorOpen;
        }

        /// <summary>Rows were added or taken out: their lines follow, the footers and widths too.</summary>
        void Reflow() { footerText.Clear(); Sync(); Totals(); Layout(); }
    }
}
