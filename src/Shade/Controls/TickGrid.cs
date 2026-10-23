// Shade - shared control: several picks from a short known list as tick boxes in columns (DESIGN_SYSTEM.md §6).
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Transformation;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using static Shade.Look;

namespace Shade
{
    /// <summary>Several picks from a short known list as tick boxes in columns (DESIGN_SYSTEM.md §6). Picked keeps the order
    /// they were ticked in, the first ones as given.</summary>
    public sealed class TickGrid : WrapPanel
    {
        readonly List<string> picked;
        /// <summary>The ticked items, in the order they were ticked.</summary>
        public IReadOnlyList<string> Picked => picked;
        public event Action? Changed;

        /// <param name="name">What people read for an item (a display name); the item itself is its tooltip.</param>
        /// <param name="switches">Switches instead of tick boxes (settings that are on or off).</param>
        /// <param name="detail">A caption under an item (an address, a type, a unit).</param>
        public TickGrid(IEnumerable<string> items, IEnumerable<string> picked, double column = 150, Func<string, string>? name = null, bool switches = false, Func<string, string>? detail = null)
        {
            this.picked = picked.ToList();
            foreach (var item in this.picked.Concat(items).Distinct())
            {
                void Set(bool on) { this.picked.Remove(item); if (on) this.picked.Add(item); Changed?.Invoke(); }
                Control box; Action flip;
                if (switches) { var sw = new Switch(Picked.Contains(item)); sw.Toggled += Set; box = sw; flip = sw.Toggle; }
                else { var tick = new Tick { State = Picked.Contains(item) }; tick.Toggled += Set; box = tick; flip = tick.Toggle; }
                box.VerticalAlignment = VerticalAlignment.Center;
                var label = Build.Text(name?.Invoke(item) ?? item, Look.Text, TextSize.Body);
                var words = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
                words.Children.Add(label);
                string? more = detail?.Invoke(item);
                if (!string.IsNullOrEmpty(more)) { label.Width = 160; var d = Build.Text(more, Text2, TextSize.Label); d.FontFamily = Mono; words.Children.Add(d); }  // one line: name, then the details in a column
                var line = new StackPanel { Orientation = Orientation.Horizontal, Width = column, Height = 28, Cursor = Build.Hand, Background = Brushes.Transparent };
                line.Children.Add(box); line.Children.Add(words);
                if (name != null) ToolTip.SetTip(line, item);
                words.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) flip(); };  // the label switches too
                Children.Add(line);
            }
        }
    }
}
