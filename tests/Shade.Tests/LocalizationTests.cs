// Localization checks.
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
        /// <summary>Look.Translate words every built-in label; a danger item stays red in any language.</summary>
        static void Words()
        {
            Look.Translate = k => k switch { "Add" => "Přidat", "Delete row" => "Smazat řádek", "Delete {0} rows" => "Smazat {0} řádky", "Cancel" => "Zrušit", _ => k };
            try
            {
                var table = new EditableTable(); var w = new Window { Content = table }; w.Show(); Dispatcher.UIThread.RunJobs();
                Ok(table.GetVisualDescendants().OfType<ActionButton>().Any(b => b.Content?.ToString() == "Přidat"), "the table's Add is translated"); w.Close();
                TextBlock Label(Border panel, string text) => panel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text);
                var menu = Menu.Panel(new[] { "Open", "Delete row", Menu.Danger("Vysypat koš") }, _ => { }, () => { });
                Ok(Label(menu, "Smazat řádek").Foreground == B(DangerText), "a translated Delete stays a danger item");
                Ok(Label(menu, "Vysypat koš").Foreground == B(DangerText) && Label(menu, "Open").Foreground == B(Look.Text), "Menu.Danger marks an item in any words; the mark doesn't show");
                MenuEntry? got = null;
                var records = Menu.Panel(new[] { new MenuEntry("Off") { Key = "off", IsEnabled = false }, new MenuEntry("On") { Key = "on", Shortcut = "Ctrl+O", IsChecked = true } }, e => got = e, () => { }, 200);
                var rowButtons = records.GetVisualDescendants().OfType<Button>().ToArray();
                Ok(!rowButtons[0].IsEnabled && Label(records, "Off").Foreground == B(Text3), "a disabled menu entry is shown dimmed and cannot be picked");
                rowButtons[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Ok(got?.Key == "on", "an entry menu reports the picked item");
            }
            finally { Look.Translate = null; }
        }

        static void LiveLanguage()
        {
            var row = new EditableTable.Row(); row.Values["name"] = "Add";
            var table = new EditableTable { Columns = new[] { new EditableTable.Column("name", "Project") }, Rows = new[] { row } };
            var search = new SearchField(); var custom = new SearchField("Find projects") { Text = "Add" };
            var list = new ListField();
            var window = new Window { Content = new StackPanel { Children = { table, search, custom, list } } }; window.Show();
            Settle();
            var text = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Add" && t.Parent?.GetType().Name == "DataCell");
            var at = text.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
            var editor = table.GetVisualDescendants().OfType<TextBox>().First(t => t.IsFocused);
            table.Validate = (_, _, text) => text == "forbidden" ? "Choose another value" : null;
            editor.Text = "forbidden"; editor.CaretIndex = 3;
            int changes = 0; table.Changed += () => changes++;
            try
            {
                Look.Translate = key => key switch { "Add" => "Přidat", "rows" => "řádky", "Search" => "Hledat", "Select row {0}" => "Vybrat řádek {0}", _ => key };
                Look.Culture = System.Globalization.CultureInfo.GetCultureInfo("cs-CZ"); Look.RefreshLanguage(); Settle();
                Ok(table.GetVisualDescendants().OfType<ActionButton>().Any(b => b.Content?.ToString() == "Přidat") && list.GetVisualDescendants().OfType<ActionButton>().Any(b => b.Content?.ToString() == "Přidat" && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Přidat")), "language refresh updates existing table and list action labels");
                Ok(search.Box.PlaceholderText == "Hledat" && custom.Box.PlaceholderText == "Find projects" && custom.Text == "Add", "language refresh updates default prompts while preserving application placeholders and input");
                Ok(editor.IsFocused && editor.CaretIndex == 3 && editor.Text == "forbidden" && row.Values["name"] == "Add" && changes == 0, "language refresh preserves the active editor, caret, domain data and change notifications");
                Ok(table.GetVisualDescendants().OfType<Border>().Any(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Vybrat řádek 1"), "translated row automation retains its actual row index");
            }
            finally { Look.Translate = null; Look.Culture = System.Globalization.CultureInfo.InvariantCulture; Look.RefreshLanguage(); window.Close(); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference DiscardLocalizedControl() => new(new SearchField());

        static void LanguageLifetime()
        {
            var discarded = DiscardLocalizedControl();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Ok(!discarded.IsAlive, "live-language callbacks do not keep discarded controls alive");
            Look.RefreshLanguage();
        }
    }
}
