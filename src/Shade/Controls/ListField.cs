// Shade - shared control: a list of short values (email recipients) as one field per row.
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
    /// <summary>A list of short values (email recipients) as one field per row: × removes a row, + adds one. Text is the
    /// values comma separated, as they are stored.</summary>
    public sealed class ListField : StackPanel
    {
        readonly string hint;
        readonly ActionButton add = new ActionButton("Add", ButtonKind.Ghost, icon: Icons.Plus) { HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 0, 8, 0) };  // the + under the fields' left edge
        public IEnumerable<Field> Fields => Children.OfType<DockPanel>().Select(d => d.Children.OfType<Field>().Single());

        public ListField(string text, string hint)
        {
            this.hint = hint; Spacing = 4;
            Children.Add(add);
            add.Click += () => Row("").Box.Focus();
            Text = text;
        }

        public string Text
        {
            get => string.Join(", ", Fields.Select(f => f.Text.Trim()).Where(t => t != ""));
            set
            {
                foreach (var d in Children.OfType<DockPanel>().ToList()) Children.Remove(d);
                foreach (var v in (value ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x != "")) Row(v);
                if (!Fields.Any()) Row("");  // one empty row to type into
            }
        }

        Field Row(string value)
        {
            var f = new Field(hint) { Text = value };
            var remove = new ActionButton("", ButtonKind.Ghost, icon: Icons.X) { Width = 28, MinWidth = 28, Margin = new Thickness(4, 0, 0, 0) };
            ToolTip.SetTip(remove, "Remove");
            var line = new DockPanel(); DockPanel.SetDock(remove, Dock.Right); line.Children.Add(remove); line.Children.Add(f);
            remove.Click += () => { Children.Remove(line); if (!Fields.Any()) Row(""); };
            Children.Insert(Children.Count - 1, line);  // above the + button
            return f;
        }
    }
}
