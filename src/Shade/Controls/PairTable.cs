// Shade - shared control: name/value pairs (query parameters, headers, variables) as a table.
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
    /// <summary>Name/value pairs (query parameters, headers, variables) as a table: a name and a value field per row, × removes a
    /// row, + Add adds one; with names, a menu adds a common one (headers). An empty name is left out; a name twice is refused.</summary>
    public sealed class PairTable : StackPanel
    {
        readonly string keyHint, valueHint;
        readonly StackPanel rows = new StackPanel { Spacing = 4 };
        readonly Grid head;
        public event Action? Changed;

        public PairTable(string keyHint, string valueHint, IReadOnlyList<string>? common = null)
        {
            this.keyHint = keyHint; this.valueHint = valueHint; Spacing = 6;
            head = Grid(Words(Build.Text("", Text2, TextSize.Label, 500), text => text.Text = T("Name")), Words(Build.Text("", Text2, TextSize.Label, 500), text => text.Text = T("Value")), new Border { Width = 28 }); head.IsVisible = false;  // shown over rows only
            Children.Add(head); Children.Add(rows);
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var add = Words(new ActionButton(T("Add"), ButtonKind.Ghost, icon: Icons.Plus), button => { button.Disarm(); button.Text = T("Add"); }); add.Click += () => Row("", "").key.Box.Focus(); tools.Children.Add(add);
            if (common != null)
            {
                var pick = Words(new ActionButton(T("Common ▾"), ButtonKind.Ghost), button => { button.Disarm(); button.Text = T("Common ▾"); }); Words(pick, word => ToolTip.SetTip(word, T("Add a well-known name")), "tip");
                pick.Click += () => Menu.Show(pick, common.ToList(), i => { var r = EmptyLine ?? Row("", ""); r.key.Text = common[i]; r.value.Box.Focus(); Changed?.Invoke(); }, width: 260);
                tools.Children.Add(pick);
            }
            Children.Add(tools);
        }

        static Grid Grid(Control key, Control value, Control end)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,8,3*,4,Auto") };
            Avalonia.Controls.Grid.SetColumn(value, 2); Avalonia.Controls.Grid.SetColumn(end, 4);
            g.Children.Add(key); g.Children.Add(value); g.Children.Add(end); return g;
        }

        IEnumerable<(Field key, Field value)> Lines => rows.Children.OfType<Grid>().Select(g => ((Field)g.Children[0], (Field)g.Children[1]));
        (Field key, Field value)? EmptyLine => Lines.Where(l => l.key.Text.Trim() == "" && l.value.Text.Trim() == "").Select(l => ((Field, Field)?)l).FirstOrDefault();

        (Field key, Field value) Row(string key, string value)
        {
            var k = new Field(keyHint) { Text = key }; var v = new Field(valueHint) { Text = value };
            var remove = new ActionButton("", ButtonKind.Ghost, icon: Icons.X) { Width = 28, MinWidth = 28 }; Words(remove, word => ToolTip.SetTip(word, T("Remove")), "tip");
            var line = Grid(k, v, remove);
            remove.Click += () => { rows.Children.Remove(line); head.IsVisible = rows.Children.Count > 0; Changed?.Invoke(); };
            k.Edited += () => Changed?.Invoke(); v.Edited += () => Changed?.Invoke();
            rows.Children.Add(line); head.IsVisible = true; return (k, v);
        }

        /// <summary>The rows as a dictionary; a name twice is refused (headers: names ignore case).</summary>
        public Dictionary<string, string> Pairs(bool caseSensitive = true)
        {
            var result = new Dictionary<string, string>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in Lines)
            {
                string k = key.Text.Trim(); if (k == "") continue;
                if (!result.TryAdd(k, value.Text)) throw new ArgumentException("Duplicate name: " + k);
            }
            return result;
        }

        /// <summary>The rows, like Pairs, but a repeated name keeps its first value (a draft, never refused).</summary>
        public Dictionary<string, string> Draft() { var d = new Dictionary<string, string>(StringComparer.Ordinal); foreach (var (k, v) in Lines) if (k.Text.Trim() != "") d.TryAdd(k.Text.Trim(), v.Text); return d; }

        public void Set(IDictionary<string, string> pairs)
        {
            rows.Children.Clear(); head.IsVisible = false;
            foreach (var p in pairs ?? new Dictionary<string, string>()) Row(p.Key, p.Value);
        }

        /// <summary>Adds a row for each of these names that has none (the {{Name}} a body uses).</summary>
        public void Ensure(IEnumerable<string> names)
        {
            var have = new HashSet<string>(Lines.Select(l => l.key.Text.Trim()), StringComparer.Ordinal);
            foreach (var n in names.Where(n => !have.Contains(n))) Row(n, "");
        }
    }
}
