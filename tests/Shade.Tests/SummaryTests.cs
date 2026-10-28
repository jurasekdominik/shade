// Summary checks.
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
        /// <summary>Summary cards select through SelectedKey, take a tooltip, a disabled state and a command per card.</summary>
        static void SummarySelection()
        {
            var command = new Command();
            var cards = new SummaryCards
            {
                Selectable = true,
                Items = new[]
                {
                    new SummaryCards.Item { Key = "a", Title = "A", Value = "1", ToolTip = "First", Command = command, CommandParameter = "x" },
                    new SummaryCards.Item { Key = "b", Title = "B", Value = "2", IsEnabled = false, ToolTip = "Off for now" },
                    new SummaryCards.Item { Title = "C", Value = "3" },
                }
            };
            var window = new Window { Width = 800, Height = 300, Content = cards }; window.Show();
            var seen = new List<string>(); cards.Picked += seen.Add;
            Settle();
            var buttons = cards.Children.OfType<Button>().ToArray();
            Ok(buttons.Length == 3 && buttons[0].IsEnabled && !buttons[1].IsEnabled, "summary cards: Selectable makes them buttons, a disabled one is off");
            Ok(buttons.All(b => b.GetVisualDescendants().OfType<Control>().Any(c => c.GetType().Name == "CardFace" && ToolTip.GetTip(c) != null) == (b != buttons[2])), "summary cards: a card's tooltip shows");
            foreach (var b in new[] { buttons[0], buttons[1], buttons[2] }) { var at = b.TranslatePoint(new Point(20, 20), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle(); if (b == buttons[0]) Ok(cards.SelectedKey == "a" && command.Runs == 1, "summary cards: a click selects the card by key and runs its command"); }
            Ok(cards.SelectedKey == "C" && seen.SequenceEqual(new[] { "a", "C" }) && command.Runs == 1, "summary cards: a disabled card takes no click; a card with no key selects by its title");
            cards.SelectedKey = null; Settle();
            Ok(cards.SelectedKey == null, "summary cards: SelectedKey clears");
            var plain = new SummaryCards { Items = new[] { new SummaryCards.Item { Title = "P", Value = "1" } } };
            Ok(plain.Children.OfType<Button>().Count() == 0, "summary cards: without a handler, selection or command they only show");
            plain.Items = new[] { new SummaryCards.Item { Title = "P", Value = "1", Command = command } };
            Ok(plain.Children.OfType<Button>().Count() == 1, "summary cards: a card's command makes the cards clickable");
            window.Close();
        }

        static void MetricCoverage()
        {
            try
            {
                Look.Configure(new ShadeOptions { TextScale = 2, ReducedMotion = true });
                var summary = new SummaryCards { Items = new[] { new SummaryCards.Item { Title = "Services", Value = "123", Line = "Healthy" } } };
                summary.Measure(new Size(800, double.PositiveInfinity));
                Ok(summary.DesiredSize.Height == 168, "summary cards expand their geometry with text size");
                summary.Detailed = true; summary.Measure(new Size(800, double.PositiveInfinity)); Ok(summary.DesiredSize.Height == 224, "detailed summary cards expand without clipping their labels");
                var empty = new SummaryCards(); empty.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); Ok(empty.DesiredSize.Width == 0 && empty.DesiredSize.Height == 0, "an empty unbounded summary has a valid empty size");
                Ok(Look.ControlHeight == 56, "shared metrics cover inputs");
                // the switch theme reads the metrics when Look.Apply builds it; rebuild it for the scale this test sets after Apply
                Application.Current!.Resources[typeof(ToggleSwitch)] = typeof(Look).Assembly.GetType("Shade.ToggleSwitchTheme")!.GetMethod("Create", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null)!;
                var scaledBox = new CheckBox(); var scaledSwitch = new ToggleSwitch(); var boxHost = new Window { Content = new StackPanel { Children = { scaledBox, scaledSwitch } } }; boxHost.Show(); Settle();
                Ok(scaledBox.Bounds.Size == new Size(32, 32) && scaledSwitch.Bounds.Size == new Size(64, 36), "the native check box and switch scale with the metrics");
                var thumb = scaledSwitch.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Knob"); double resting = thumb.Bounds.Width;
                var press = scaledSwitch.TranslatePoint(new Point(10, 10), boxHost)!.Value; boxHost.MouseDown(press, Avalonia.Input.MouseButton.Left); Settle();
                Ok(thumb.Bounds.Width > resting, "a pressed switch stretches its thumb"); boxHost.MouseUp(press, Avalonia.Input.MouseButton.Left); Settle();
                Ok(scaledSwitch.IsChecked == true && thumb.Bounds.Width == resting, "a click turns the switch on and the thumb settles"); boxHost.Close();
                var sidebar = new Sidebar("App", "", new[] { new NavEntry("Home", "home", Icons.Monitor) });
                Ok(sidebar.Width == 360, "expanded navigation scales with its labels"); sidebar.Collapsed = true; Ok(sidebar.Width == 108, "collapsed navigation preserves scaled geometry");
                var ticks = new TickGrid(new[] { "Wednesday" }, Array.Empty<string>(), 110);
                Ok(((Control)ticks.Children[0]).Height == 56 && ((Control)ticks.Children[0]).Width >= 220, "tick-grid rows and columns scale without overlapping their labels");
                var designer = new TableDesigner { Columns = new[] { new TableDesigner.Column("Heading") }, Rows = new[] { new[] { "Value" } } };
                Ok(designer.GetLogicalDescendants().OfType<Border>().Count(b => b.Height == 56) == 2, "table-designer headers and preview rows follow scaled control heights");
                var palette = new ColorGrid(Look.Accent); Ok(palette.Width > 900 && palette.Height > 450, "the color helper expands its palette and mixer coordinate space");
            }
            finally { Look.Configure(new ShadeOptions()); }
        }
    }
}
