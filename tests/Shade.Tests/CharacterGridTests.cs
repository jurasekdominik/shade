using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade.Tests;

static partial class Program
{
    static void CharacterGrids()
    {
        var paperDefault = new CharacterGridField { Appearance = CharacterGridAppearance.Paper };
        var fieldDefault = new CharacterGridField { Appearance = CharacterGridAppearance.Field };
        var defaults = new Window { Width = 640, Height = 160, Content = new StackPanel { Children = { paperDefault, fieldDefault } } };
        defaults.Show(); Settle();
        Ok(paperDefault.CellSize.Width == 16 && paperDefault.CornerRadius == new CornerRadius(0) && fieldDefault.CellSize.Width == 20 && fieldDefault.CornerRadius == new CornerRadius(Radius), "approved A/C variants expose compact square paper and roomier rounded field defaults");
        defaults.Close();
        foreach (var appearance in Enum.GetValues<CharacterGridAppearance>())
        {
            var model = new Vm { Name = "0026018342" };
            var field = new CharacterGridField { CellCount = 16, CellSize = new Size(16, 20), Appearance = appearance };
            field.Bind(TextBox.TextProperty, new Binding(nameof(Vm.Name)) { Source = model, Mode = BindingMode.TwoWay });
            AutomationProperties.SetName(field, "Serial number");
            var next = new CharacterGridField { CellCount = 8, CellSize = field.CellSize, Appearance = appearance };
            var panel = new StackPanel { Children = { field, next } };
            var window = new Window { Width = 640, Height = 220, Content = panel };
            window.Show(); Settle();
            var presenter = field.GetVisualDescendants().OfType<TextPresenter>().Single();
            var geometry = field.Bounds.Size;
            Ok(geometry.Width == 258 && geometry.Height == 22 && next.Bounds.Width == 130, $"{appearance}: capacities retain identical pitch and compact geometry");

            foreach (var font in new[] { Mono, UiFont })
            {
                field.FontFamily = font;
                model.Name = "Wi0e\u0301🙂🇨🇿"; Settle();
                var boundaries = StringInfo.ParseCombiningCharacters(field.Text ?? "").Append(field.Text!.Length).ToArray();
                bool equal = true;
                for (int i = 1; i < boundaries.Length; i++)
                    equal &= Math.Abs(presenter.TextLayout.HitTestTextPosition(boundaries[i]).X - presenter.TextLayout.HitTestTextPosition(boundaries[i - 1]).X - 16) < .01;
                Ok(equal, $"{appearance}: fixed grapheme advance with {font}, composed accents and fallback glyphs");
                Ok(field.Bounds.Size == geometry, $"{appearance}: changing font and grapheme count preserves geometry");
            }

            model.Name = "ae\u0301🙂🇨🇿b"; field.Focus(); field.CaretIndex = 1; Settle();
            foreach (int boundary in new[] { 3, 5, 9, 10 })
            {
                window.KeyPress(Key.Right, Avalonia.Input.RawInputModifiers.None, PhysicalKey.None, null);
                window.KeyRelease(Key.Right, Avalonia.Input.RawInputModifiers.None, PhysicalKey.None, null); Settle();
                Ok(field.CaretIndex == boundary, $"{appearance}: native Right visits grapheme boundary {boundary} without splitting a cell");
            }

            model.Name = "0026018342"; Settle();
            var point = presenter.TranslatePoint(new Point(4 * 16 + 1, presenter.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Settle();
            Ok(field.CaretIndex == 4 && field.SelectionStart == field.SelectionEnd, $"{appearance}: click places a caret in its cell without selecting all");
            window.KeyTextInput("9"); Settle();
            Ok(model.Name == "00269018342", $"{appearance}: native typing preserves leading zeroes and the binding");
            field.Undo(); Settle(); Ok(field.Text == "0026018342", $"{appearance}: native undo restores the complete string");
            field.SelectionStart = 2; field.SelectionEnd = 5;
            window.KeyTextInput("ABC"); Settle(); Ok(model.Name == "00ABC18342", $"{appearance}: native replacement covers multiple cells");
            var selected = (field.CaretIndex, field.SelectionStart, field.SelectionEnd);
            field.Appearance = appearance == CharacterGridAppearance.Paper ? CharacterGridAppearance.Field : CharacterGridAppearance.Paper;
            Settle();
            Ok(field.IsFocused && field.Bounds.Size == geometry && selected == (field.CaretIndex, field.SelectionStart, field.SelectionEnd) && model.Name == "00ABC18342", $"{appearance}: changing appearance preserves focus, text, selection and geometry");
            field.Appearance = appearance;
            model.Name = "0123456789012345"; field.CaretIndex = 16; Settle();
            var origin = presenter.TranslatePoint(default, field)!.Value;
            double end = presenter.TextLayout.HitTestTextPosition(16).X + origin.X;
            Ok(Math.Abs(end - (field.Bounds.Width - field.BorderThickness.Right)) < .01, $"{appearance}: a full-capacity caret stays inside the outer frame");
            presenter.PreeditText = "ě"; Settle();
            Ok(presenter.TextLayout.WidthIncludingTrailingWhitespace == 17 * 16 && model.Name.Length == 16, $"{appearance}: native IME preedit uses the same pitch without committing text");
            presenter.PreeditText = null;

            var peer = ControlAutomationPeer.CreatePeerForElement(field)!;
            Ok(peer.GetName() == "Serial number" && peer.GetAutomationControlType() == AutomationControlType.Edit && peer is IValueProvider, $"{appearance}: one named native edit control exposes value automation");
            var value = (IValueProvider)peer;
            field.IsReadOnly = true; window.KeyTextInput("7"); Settle();
            bool rejected = false; try { value.SetValue("changed"); } catch (InvalidOperationException) { rejected = true; }
            Ok(rejected && value.IsReadOnly && model.Name == "0123456789012345", $"{appearance}: read-only blocks typing and automation edits");
            field.IsReadOnly = false;
            field.IsEnabled = false; window.KeyTextInput("7"); Settle();
            Ok(model.Name == "0123456789012345", $"{appearance}: disabled input cannot mutate");
            field.IsEnabled = true;
            model.Name = "01234567890123456789"; Settle();
            Ok(field.ValidationMessage != null && field.Text?.Length == 20 && field.Bounds.Size == geometry && peer.GetItemStatus() == field.ValidationMessage, $"{appearance}: overflow is preserved with an accessible error and fixed geometry");
            field.CellCount = 8; Settle(); Ok(field.Text?.Length == 20 && field.ValidationMessage != null, $"{appearance}: reducing capacity preserves the complete value");
            field.CellCount = 16; model.Name = new string('a', 15) + "e\u0301"; Settle();
            Ok(field.ValidationMessage == null, $"{appearance}: capacity counts a composed grapheme once");
            model.Name = "a\r\nb\tc"; Settle();
            Ok(field.ValidationMessage != null && presenter.TextLayout.TextLines.Count == 1 && presenter.TextLayout.WidthIncludingTrailingWhitespace == 5 * 16, $"{appearance}: external control characters are preserved, displayed in cells and marked invalid");

            model.Name = "abc"; field.CellGap = 4; Settle();
            Ok(presenter.TextLayout.HitTestTextPosition(2).X == 40 && field.Bounds.Width == 318, $"{appearance}: gaps share the same native layout and measured geometry");
            field.CellGap = 0;

            var decorations = new ObservableCollection<CharacterCellDecoration> { new() { Start = 1, Length = 2, Foreground = B(Success), Background = B(Fill) } };
            field.CellDecorations = decorations;
            field.DataContext = model;
            decorations[0].Bind(CharacterCellDecoration.LengthProperty, new Binding(nameof(Vm.EmployeeId)));
            model.EmployeeId = 2; Settle();
            Ok(ReferenceEquals(decorations[0].DataContext, model) && decorations[0].Length == 2, $"{appearance}: decoration bindings inherit the field's data context");
            field.CellCount = 16; model.Name = "abcd"; Settle();
            var coloured = presenter.TextLayout.TextLines.SelectMany(line => line.TextRuns).OfType<Avalonia.Media.TextFormatting.ShapedTextRun>().ToArray();
            Ok(coloured.Length == 4 && ReferenceEquals(coloured[1].Properties.ForegroundBrush, B(Success)), $"{appearance}: a public decoration styles its grapheme range");
            decorations[0].Foreground = B(Info); Settle();
            coloured = presenter.TextLayout.TextLines.SelectMany(line => line.TextRuns).OfType<Avalonia.Media.TextFormatting.ShapedTextRun>().ToArray();
            Ok(ReferenceEquals(coloured[1].Properties.ForegroundBrush, B(Info)), $"{appearance}: changing a decoration refreshes native glyph styling");
            decorations.Clear(); Settle();
            model.Name = new string('a', 17); field.CaretIndex = 3; field.SelectionStart = 1; field.SelectionEnd = 3; Settle();
            var oldTranslate = Look.Translate;
            try
            {
                Look.Translate = key => key == "Use at most {0} characters." ? "Použijte nejvýše {0} znaků." : oldTranslate?.Invoke(key) ?? key;
                Look.RefreshLanguage(); Settle();
                Ok(field.ValidationMessage == "Použijte nejvýše 16 znaků." && field.Text?.Length == 17 && field.CaretIndex == 3 && field.SelectionStart == 1 && field.SelectionEnd == 3, $"{appearance}: language refresh preserves an invalid draft and its selection");
            }
            finally { Look.Translate = oldTranslate; Look.RefreshLanguage(); }
            model.Name = "abcd"; Settle();
            panel.Children.Remove(field); decorations.Add(new() { Start = 0 }); panel.Children.Insert(0, field); Settle();
            Ok(field.Text == "abcd" && field.CellDecorations.Count == 1, $"{appearance}: detach and reattach preserve text and reconnect range changes");
            window.Close();
        }
    }
}
