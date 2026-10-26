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
        public static readonly StyledProperty<string> HintProperty = AvaloniaProperty.Register<ListField, string>(nameof(Hint), "");
        public static readonly DirectProperty<ListField, string> TextProperty = AvaloniaProperty.RegisterDirect<ListField, string>(nameof(Text), o => o.Text, (o, v) => o.Text = v, "", Avalonia.Data.BindingMode.TwoWay);
        public string Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }
        public event Action? Changed;
        string shown = "";
        bool replacing;
        void Notify(bool userEdit)
        {
            if (replacing) return;
            var now = Text;
            if (now == shown) return;
            var before = shown; shown = now; RaisePropertyChanged(TextProperty, before, now);
            if (userEdit) Changed?.Invoke();
        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HintProperty) foreach (var field in Fields) field.Box.PlaceholderText = Hint;
        }
        public ListField() : this("", "") { }
        readonly ActionButton add = Words(new ActionButton(T("Add"), ButtonKind.Ghost, icon: Icons.Plus) { HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 0, 8, 0) }, button => { button.Disarm(); button.Content = T("Add"); });  // the + under the fields' left edge
        public IEnumerable<Field> Fields => Children.OfType<DockPanel>().Select(d => d.Children.OfType<Field>().Single());

        public ListField(string text, string hint)
        {
            Hint = hint; Spacing = 4;
            Children.Add(add);
            add.Click += (_, _) => Row("").Box.Focus();
            Text = text;
        }

        public string Text
        {
            get => string.Join(", ", Fields.Select(f => f.Text.Trim()).Where(t => t != ""));
            set
            {
                if (Fields.Any() && Text == (value ?? "")) return;
                replacing = true;
                try
                {
                    foreach (var d in Children.OfType<DockPanel>().ToList()) Children.Remove(d);
                    foreach (var v in (value ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x != "")) Row(v);
                    if (!Fields.Any()) Row("");
                }
                finally { replacing = false; }
                Notify(false);  // one empty row to type into
            }
        }

        Field Row(string value)
        {
            var f = new Field(Hint) { Text = value };
            f.PropertyChanged += (_, e) => { if (e.Property == Field.TextProperty) Notify(true); };
            var remove = new ActionButton("", ButtonKind.Ghost, icon: Icons.X) { Width = 28, MinWidth = 28, Margin = new Thickness(4, 0, 0, 0) };
            Words(remove, word => ToolTip.SetTip(word, T("Remove")), "tip");
            var line = new DockPanel(); DockPanel.SetDock(remove, Dock.Right); line.Children.Add(remove); line.Children.Add(f);
            remove.Click += (_, _) => { Children.Remove(line); if (!Fields.Any()) Row(""); Notify(true); };
            Children.Insert(Children.Count - 1, line);  // above the + button
            return f;
        }
    }
}
