// Field chrome: FieldTheme paints each state from pseudo-classes, for Field and the fields built on it.
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Media;
using static Shade.Look;

namespace Shade.Tests
{
    static partial class Program
    {
        static void FieldStates()
        {
            var field = new Field("Name"); var inherited = new Field("Inherited") { Inherit = true }; var date = new DateField(); var address = new HostPortField();
            var other = new TextBox();
            var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { field, inherited, date, address, other } } }; window.Show(); Settle();
            bool Is(Border b, Tone border) => b.BorderBrush == B(border);
            Ok(Is(field, Look.Separator) && field.Background == B(Raised) && Is(date, Look.Separator) && Is(address, Look.Separator), "a field and the fields built on it get FieldTheme's resting border and fill");
            field.Invalid = true; Settle(); bool invalid = Is(field, Danger);
            field.Invalid = false; field.Pending = true; Settle(); bool pending = Is(field, Warn);
            field.Pending = false; Settle(); bool back = Is(field, Look.Separator);
            Ok(invalid && pending && back, "Invalid and Pending paint the border and clear it again");
            field.Box.Focus(); Settle(); bool focused = Is(field, FocusBorder);
            field.Invalid = true; Settle(); bool invalidWins = Is(field, Danger);
            field.Invalid = false; other.Focus(); Settle();
            Ok(focused && invalidWins && Is(field, Look.Separator), "focus inside the field lights it (:focus-within); a wrong value outranks focus");
            inherited.Box.Text = "own"; Settle(); bool overridden = Is(inherited, Info);
            inherited.Box.Text = ""; Settle();
            Ok(overridden && Is(inherited, Look.Separator), "an override of the inherited value paints the info border until it is cleared");
            date.Invalid = true; Settle(); bool dateInvalid = Is(date, Danger); date.Invalid = false;
            field.IsEnabled = false; field.Invalid = true; Settle();
            Ok(dateInvalid && Is(field, Look.Separator) && field.Background == B(Look.Background), "the date field follows the same states; a disabled field shows neither state nor fill");
            window.Close();
        }

        static void TextBoxStates()
        {
            var box = new TextBox { PlaceholderText = "Native" }; var field = new Field("Field"); var other = new TextBox();
            var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { box, field, other } } }; window.Show(); Settle();
            Border Frame(TextBox t) => t.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
            var frame = Frame(box);
            Ok(frame.BorderBrush == B(Look.Separator) && frame.Background == B(Raised) && box.Bounds.Height >= ControlHeight && box.PlaceholderForeground == B(Text3), "a native TextBox is a Shade text field: frame, fill, height and a text-3 placeholder");
            box.Focus(); Settle(); bool focused = frame.BorderBrush == B(FocusBorder);
            other.Focus(); Settle();
            Ok(focused && frame.BorderBrush == B(Look.Separator), "focus lights a native TextBox's frame");
            var plain = Frame(field.Box); field.Box.Focus(); Settle();
            Ok(plain.BorderThickness == new Avalonia.Thickness(0) && plain.Background == Brushes.Transparent && field.BorderBrush == B(FocusBorder), "a field's own box draws no frame or fill, also focused; the field shows the focus");
            box.Text = "copy me"; box.SelectAll(); var menu = (MenuFlyout)box.ContextFlyout!; menu.ShowAt(box); Settle();
            var items = menu.Items.OfType<MenuItem>().ToList();
            Ok(items.Select(i => i.Header).SequenceEqual(new object[] { "Cut", "Copy", "Paste" }) && items[1].IsEnabled, "a native TextBox has Cut, Copy and Paste on right-click, enabled by its state");
            menu.Hide(); window.Close();
        }
    }
}
