// Date checks.
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
        /// <summary>The date field: typed in two ways, picked in the calendar with the pointer and the keyboard, limited by Min..Max.</summary>
        static void Dates()
        {
            Ok(DateField.Read("2026-10-24") == new DateTime(2026, 10, 24) && DateField.Read("24.10.2026") == new DateTime(2026, 10, 24) && DateField.Read("4. 1. 2026") == new DateTime(2026, 1, 4),
                "a day reads as yyyy-MM-dd, d.M.yyyy or d. M. yyyy");
            Ok(DateField.Read("2026-13-01") == null && DateField.Read("tomorrow") == null && DateField.Read("31.2.2026") == null, "anything else isn't a day");
            var field = new DateField { Min = new DateTime(2026, 10, 5), Max = new DateTime(2026, 11, 20), Value = new DateTime(2026, 10, 24) };
            int changed = 0; field.Changed += () => changed++;
            var range = new DateRangeField { Start = new DateTime(2026, 10, 20), End = new DateTime(2026, 10, 28) };
            int spans = 0; range.Changed += () => spans++;
            var window = new Window { Width = 500, Height = 460, Content = new StackPanel { Margin = new Thickness(16), Spacing = 8, Children = { field, range } } }; window.Show();
            void Click(Point p) { Settle(); window.MouseDown(p, Avalonia.Input.MouseButton.Left); window.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle(); }
            void Key(Avalonia.Input.Key k, Avalonia.Input.RawInputModifiers m = Avalonia.Input.RawInputModifiers.None) { window.KeyPress(k, m, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(k, m, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            void Type(string t) { foreach (var c in t) { window.KeyTextInput(c.ToString()); Settle(); } }
            Control? Grid() => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.GetType().Name == "MonthGrid");  // internal: by its name
            string Shown(Control blocks) => (string)blocks.GetType().GetProperty("Shown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(blocks)!;
            Control Blocks(Control f, string name = "Blocks") => (Control)f.GetType().GetProperty(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(f)!;
            Settle();

            var blocks = Blocks(field);
            Ok(Shown(blocks) == "2026-10-24" && field.Text == "2026-10-24", "the day shows as YYYY-MM-DD blocks");
            Click(blocks.TranslatePoint(new Point(blocks.Bounds.Width - 6, 13), window)!.Value);
            Ok(Grid() != null && blocks.IsFocused, "a click on the day block opens the month and gives the blocks the keyboard");
            Key(Avalonia.Input.Key.Up);
            Ok(field.Value == new DateTime(2026, 10, 25) && changed == 1, "Up raises the day block");
            Key(Avalonia.Input.Key.Left); Type("11");
            Ok(field.Invalid && field.Value == new DateTime(2026, 10, 25) && Shown(blocks) == "2026-11-25", "typing 11 in the month makes 25 November, after Max: marked, the value kept");
            Type("15");
            Ok(!field.Invalid && field.Value == new DateTime(2026, 11, 15), "the digits move on to the day: 15 November is allowed");
            Key(Avalonia.Input.Key.Left); Type("2");
            Ok(field.Value == new DateTime(2026, 11, 15) && Shown(blocks) == "2026-02-15" && field.Invalid, "2 in the month can only be February and moves on; before Min it is marked, the value kept");
            Key(Avalonia.Input.Key.Back);
            Ok(Shown(blocks) == "2026-02-DD" && field.Value == null, "Backspace empties a block, which shows its placeholder");
            Key(Avalonia.Input.Key.Escape);
            Ok(Grid() == null, "Esc closes the month");
            field.Value = new DateTime(2026, 10, 24);
            Click(blocks.TranslatePoint(new Point(4, 13), window)!.Value);
            var grid = Grid(); Ok(grid != null, "a click on the year block opens the month too");
            if (grid != null)
            {
                // 2026-10-01 is a Thursday: with Monday first the grid starts on 2026-09-28; the 30th sits at row 4, column 4
                Click(grid.TranslatePoint(new Point(12 + 4 * 32 + 14, 68 + 4 * 32 + 14), window)!.Value);
                Ok(field.Value == new DateTime(2026, 10, 30) && Shown(blocks) == "2026-10-30" && Grid() == null, $"a click on a day picks it and closes the month ({field.Value:yyyy-MM-dd}, {Shown(blocks)}, {(Grid() == null ? "closed" : "open")})");
            }
            blocks.Focus(); Key(Avalonia.Input.Key.Down, Avalonia.Input.RawInputModifiers.Alt);
            Ok(Grid() is { IsFocused: true }, "Alt+Down opens the month and gives it the keyboard");
            Key(Avalonia.Input.Key.Right); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Enter);
            Ok(field.Value == new DateTime(2026, 11, 7) && Grid() == null && blocks.IsFocused, "arrows move the day, Enter picks it, the blocks get the keyboard back");
            Key(Avalonia.Input.Key.F4); Key(Avalonia.Input.Key.PageDown); Key(Avalonia.Input.Key.PageDown); Key(Avalonia.Input.Key.Enter);
            Ok(field.Value == new DateTime(2026, 11, 7), "a day after Max can't be picked");
            Key(Avalonia.Input.Key.Escape); Ok(Grid() == null, "Esc closes the month, the day unchanged");
            var icon = field.TranslatePoint(new Point(field.Bounds.Width - 16, field.Bounds.Height / 2), window)!.Value;
            Click(icon); Ok(Grid() != null, "a click on the calendar icon opens the month, and it stays open");
            Click(icon); Ok(Grid() == null, "a second click on the icon closes it");

            // the range: the days between are coloured; a new start asks for its end; an end before the start swaps them
            Ok(DateRangeField.Read("2026-10-28/2026-10-20") == (new DateTime(2026, 10, 20), new DateTime(2026, 10, 28)) && range.Text == "2026-10-20/2026-10-28", "a range reads and shows as start/end");
            var from = Blocks(range, "From"); var until = Blocks(range, "Until");
            Click(from.TranslatePoint(new Point(4, 13), window)!.Value);
            grid = Grid(); Ok(grid != null && from.IsFocused, "a click on the start opens the month");
            if (grid != null)
            {
                Click(grid.TranslatePoint(new Point(12 + 0 * 32 + 14, 68 + 1 * 32 + 14), window)!.Value);  // Monday 5 October
                Ok(range.Start == new DateTime(2026, 10, 5) && range.End == null && until.IsFocused && Grid() != null, "a picked start clears the end and asks for it");
                Click(grid.TranslatePoint(new Point(12 + 2 * 32 + 14, 68 + 3 * 32 + 14), window)!.Value);  // Wednesday 21 October
                Ok(range.End == new DateTime(2026, 10, 21) && Grid() == null && spans == 3, "the end closes the month: 5 to 21 October");
            }
            until.Focus(); Key(Avalonia.Input.Key.Home); Type("2026"); Type("10"); Type("01");
            Ok(range.End == new DateTime(2026, 10, 1) && range.Invalid, "an end typed before the start marks the field");
            Key(Avalonia.Input.Key.Back); Type("09");
            Ok(range.End == new DateTime(2026, 10, 9) && !range.Invalid, "fixed, the mark goes");
            window.Close();
        }

        static void CalendarLimits()
        {
            try
            {
                foreach (var scale in new[] { 1.0, 2.0 })
                {
                    Look.Configure(new ShadeOptions { TextScale = scale });
                    foreach (var limit in new[] { DateTime.MinValue.Date, DateTime.MaxValue.Date })
                    {
                        var field = new DateField { Value = limit, FirstDay = DayOfWeek.Sunday };
                        var window = new Window { Width = 800, Height = 1100, Content = field }; window.Show();
                        var blocks = field.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "DateBlocks");
                        blocks.Focus(); window.KeyPress(Avalonia.Input.Key.F4, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                        var grid = window.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "MonthGrid");
                        var drawing = new Avalonia.Media.DrawingGroup(); using (var context = drawing.Open()) grid.Render(context);
                        grid.Focus();
                        window.KeyPress(limit.Year == 1 ? Avalonia.Input.Key.PageUp : Avalonia.Input.Key.PageDown, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                        using (var context = drawing.Open()) grid.Render(context);
                        Ok(field.Value == limit && blocks.Bounds.Height >= 26 * scale && grid.Bounds.Height >= 300 * scale, $"calendar safely renders and navigates year {limit.Year} at text scale {scale}");
                        field.IsReadOnly = true; Settle();
                        Ok(!window.GetVisualDescendants().OfType<Control>().Any(c => c.GetType().Name == "MonthGrid"), "switching an open date to read-only closes its calendar");
                        if (limit.Year == 1)
                        {
                            field.IsReadOnly = false; blocks.Focus();
                            window.KeyPress(Avalonia.Input.Key.Up, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                            Ok(field.Value?.Year == 2, "incrementing a four-digit year with leading zeros keeps a valid date");
                        }
                        window.Close();
                    }
                }
            }
            finally { Look.Configure(new ShadeOptions()); }
        }
    }
}
