// Shade's checks without a screen: the colour rules, the parsers, and the controls in XAML with bindings.
// Exits 1 when one fails.
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
        static int failed;
        static void Ok(bool cond, string what) { if (!cond) failed++; Console.WriteLine((cond ? "ok   " : "FAIL ") + what); }

        static void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }

        static int Scale(int count, int columnCount, bool unique, bool fixedWidth, bool footers)
        {
            long heapBefore = GC.GetTotalMemory(true);
            var start = System.Diagnostics.Stopwatch.StartNew();
            var before = GC.GetTotalAllocatedBytes(true);
            var columns = Enumerable.Range(0, columnCount).Select(i => new EditableTable.Column("c" + i, "Column " + i, EditableTable.Kind.Number) { Width = fixedWidth ? 100 : null, Footer = footers ? EditableTable.Total.Sum : EditableTable.Total.None }).ToArray();
            var rows = Enumerable.Range(0, count).Select(i => { var row = new EditableTable.Row(); for (int c = 0; c < columns.Length; c++) row.Values[columns[c].Key] = (unique ? (long)i * columnCount + c : i).ToString(System.Globalization.CultureInfo.InvariantCulture); return row; }).ToList();
            var table = new EditableTable { Columns = columns, Rows = rows };
            var window = new Window { Width = 900, Height = 600, Content = table }; window.Show(); Dispatcher.UIThread.RunJobs();
            long elapsed = start.ElapsedMilliseconds, allocated = GC.GetTotalAllocatedBytes(true) - before;
            long retained = Math.Max(0, GC.GetTotalMemory(true) - heapBefore);
            Console.WriteLine($"{count} rows, {columnCount} columns ({(unique ? "unique" : "repeated")}, {(fixedWidth ? "fixed" : "fit")}, totals={footers}): {elapsed} ms; {allocated / 1024 / 1024} MiB allocated; {retained / 1024 / 1024} MiB managed heap increase; {table.GetVisualDescendants().Count()} visuals");
            int visuals = table.GetVisualDescendants().Count();
            window.Close(); return visuals < 2000 + 200 * columnCount ? 0 : 1;
        }

        static int Main(string[] args)
        {
            if (args.Contains("--host-theme"))
            {
                AppBuilder.Configure<HostThemeApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
                Ok(Application.Current!.Styles.OfType<Avalonia.Themes.Fluent.FluentTheme>().Count() == 1, "Shade can compose with a host's existing Fluent theme");
                var field = new Field { PlaceholderText = "Host input" }; var button = new ActionButton("Save"); var toggle = new ToggleSwitch();
                var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { field, button, toggle } } }; window.Show(); Dispatcher.UIThread.RunJobs();
                Ok(field.Box.Bounds.Height > 0 && button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Save") && toggle.Bounds.Width == 32, "host composition retains native editors and Shade action/toggle presentation");
                window.Close(); return failed == 0 ? 0 : 1;
            }
            AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            if (args.Contains("--native-feedback")) { NativeFeedback(); Editing(); TableInteractionGeometry(); return failed == 0 ? 0 : 1; }
            if (args.Contains("--search-choice-scale")) return SearchChoiceScale();
            if (Array.IndexOf(args, "--scale") is int scaleIndex && scaleIndex >= 0)
                return Scale(int.TryParse(args.ElementAtOrDefault(scaleIndex + 1), out int count) ? Math.Clamp(count, 1, 100000) : 1000,
                    int.TryParse(args.ElementAtOrDefault(scaleIndex + 2), out int columns) ? Math.Clamp(columns, 1, 50) : 1, args.Contains("--unique"), args.Contains("--fixed"), args.Contains("--footers"));
            Colours();
            Parsers();
            Xaml();
            Marks();
            Focus();
            AccentPicking();
            Dates();
            Designing();
            Modes();
            Editing();
            ColumnRibbon();
            ToolbarLayout();
            RibbonExtension();
            TableTemplates();
            SidebarSlots();
            DialogChoices();
            SummarySelection();
            ColumnRibbonFeedback();
            ColumnViewportAndLayout();
            ColumnRibbonLayouts();
            Money();
            Mixed();
            Permissions();
            Words();
            Adaptability();
            VirtualRows();
            AccessibleEditors();
            NativeAutomation();
            CalendarLimits();
            RibbonTabVisibility();
            PopupContracts();
            LiveLanguage();
            LanguageLifetime();
            Activation();
            MetricCoverage();
            SummaryAndCalendarAutomation();
            FocusContrastAndTabSemantics();
            FooterRefreshCosts();
            MultiSelection();
            BindingRegressions();
            NativeActions();
            SearchableChoices();
            IntegrationFeedback();
            NativeFeedback();
            TableInteractionGeometry();
            PairRows();
            NativeSelections();
            SuggestionLifecycle();
            var pairs = new PairTable("Name", "Value"); pairs.Set(new Dictionary<string, string> { ["A"] = "kept" });
            pairs.Ensure(new[] { "A", "B", "B" }); pairs.Ensure(new[] { "B" });
            Ok(pairs.Pairs().Count == 2 && pairs.Pairs()["A"] == "kept", "PairTable.Ensure adds repeated new names once and preserves existing values");
            Highlighting();
            Documented();
            Console.WriteLine(failed == 0 ? "all passed" : failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
