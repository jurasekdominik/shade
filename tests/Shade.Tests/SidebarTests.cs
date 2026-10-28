// Sidebar checks.
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
        /// <summary>The sidebar opens through properties: entries (hidden, disabled, badged, headings), header and footer.</summary>
        static void SidebarSlots()
        {
            var tasks = new NavEntry("Tasks", "tasks", Icons.ListChecks);
            var reports = new NavEntry("Reports", "reports", Icons.Activity);
            var sidebar = new Sidebar { Brand = "App" };
            sidebar.Entries.Add(tasks); sidebar.Entries.Add(new NavEntry { Label = "Section", IsHeading = true }); sidebar.Entries.Add(reports);
            var window = new Window { Width = 400, Height = 500, Content = sidebar }; window.Show();
            List<NavItem> Rows() => sidebar.GetVisualDescendants().OfType<NavItem>().ToList();
            bool Has(string text) => sidebar.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text && t.IsEffectivelyVisible);
            Settle();
            Ok(Rows().Count == 2 && Has("Section") && Has("App"), "sidebar: entries added in code show as rows, a heading as a title");
            reports.IsVisible = false; Settle();
            Ok(Rows().Count == 1, "sidebar: an entry hides when its IsVisible clears");
            reports.IsVisible = true; reports.IsEnabled = false; tasks.Badge = new NavBadge("3", Danger); Settle();
            Ok(Rows().Count == 2 && !Rows()[1].IsEffectivelyEnabled && Rows()[0].IsEffectivelyEnabled && Has("3"), "sidebar: an entry greys out and a badge shows");
            string? went = null; sidebar.Navigate += r => went = r;
            foreach (var row in new[] { Rows()[1], Rows()[0] }) { var at = row.TranslatePoint(new Point(40, 10), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle(); }
            Ok(went == "tasks", "sidebar: a disabled entry does not navigate");
            sidebar.Collapsed = true; Settle();
            Ok(!Has("3") && !Has("Section"), "sidebar: collapsed, the badge and heading text give way");
            sidebar.Collapsed = false; Settle();
            var history = sidebar.History;
            Ok(history.IsEffectivelyVisible, "sidebar: Back and Forward show by default");
            sidebar.ShowHistory = false; sidebar.Footer = new TextBlock { Text = "Account" }; sidebar.Header = new TextBlock { Text = "Own header" }; Settle();
            Ok(!sidebar.GetVisualDescendants().Contains(history) && Has("Account") && Has("Own header") && !Has("App"), "sidebar: the footer and header slots replace History and the name");
            sidebar.Entries.Remove(reports); Settle();
            Ok(Rows().Count == 1, "sidebar: removing an entry removes its row");
            window.Close();
        }

        /// <summary>NavItem and TextTabs state comes from pseudo-classes through their themes.</summary>
        static void NavStates()
        {
            var item = new NavItem("Tasks", Icons.ListChecks, false); var selected = new NavItem("Home", Icons.ListChecks, false) { Active = true };
            var parent = new NavItem("Reports", Icons.ListChecks, false) { ParentActive = true }; var off = new NavItem("Archive", Icons.ListChecks, false) { IsEnabled = false };
            var tabs = new TextTabs("Read", "Write");
            var window = new Window { Width = 400, Height = 400, Content = new StackPanel { Children = { item, selected, parent, off, tabs } } }; window.Show(); Settle();
            T Part<T>(Control c, string name) where T : Control => c.GetVisualDescendants().OfType<T>().First(p => p.Name == name);
            bool Looks(NavItem n, Tone surface, Tone ink, bool marker) => Part<Border>(n, "PART_Surface").Background == B(surface) && Part<TextBlock>(n, "PART_Label").Foreground == B(ink)
                && ((Avalonia.Controls.Shapes.Path)Part<Control>(n, "PART_Glyph")).Stroke == B(ink) && Part<Border>(n, "PART_Marker").IsVisible == marker;
            Ok(Looks(item, Raised, Text2, false) && Looks(selected, AccentTint, Look.Text, true) && Looks(parent, Raised, Look.Text, false) && Looks(off, Raised, Disabled, false), "nav items: rest, selected, parent of the selected and disabled come from the theme");
            item.Active = true; selected.Active = false; Settle();
            Ok(Looks(item, AccentTint, Look.Text, true) && Looks(selected, Raised, Text2, false), "a nav item follows Active both ways");
            item.Focus(Avalonia.Input.NavigationMethod.Tab); Settle(); bool ring = Part<Border>(item, "PART_Focus").IsVisible;
            selected.Focus(Avalonia.Input.NavigationMethod.Pointer); Settle();
            Ok(ring && !Part<Border>(selected, "PART_Focus").IsVisible, "the focus ring shows for keyboard focus only (:focus-visible)");
            TextBlock Label(int i) => tabs.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text is "Read" or "Write").ElementAt(i);
            bool first = Label(0).Foreground == B(Look.Text) && Label(1).Foreground == B(Text3);
            tabs.Active = 1; Settle();
            Ok(first && Label(0).Foreground == B(Text3) && Label(1).Foreground == B(Look.Text), "text tabs: the active tab's text is bright, the others Text3, following Active");
            window.Close();
        }
    }
}
