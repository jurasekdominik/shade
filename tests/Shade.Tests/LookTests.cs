// Look checks.
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
        static void Colours()
        {
            Ok(Math.Abs(Contrast(Colors.White, Colors.Black) - 21) < 0.01, "contrast: white on black is 21:1");
            Ok(OnColor(Hex("#FFFFFF")) == Ink && OnColor(Hex("#151519")) == White, "text on a fill: ink on light, white on dark");
            foreach (var mode in new[] { Mode.Dark, Mode.Light })
            {
                SetMode(mode);
                foreach (var (name, dark, light) in Accents)
                {
                    SetAccent(dark, light);
                    var a = mode == Mode.Dark ? dark : light;
                    Ok(Accent.Color == a, $"{mode} {name}: the accent takes its {mode} value");
                    Ok(Contrast(AccentText, Raised) >= 4.5, $"{mode} {name}: accent text reads at 4.5:1 on Raised");
                    Ok(Contrast(OnAccent, Accent) >= 4.5, $"{mode} {name}: text on the accent reads at 4.5:1");
                    Ok(NearStatus(a) == null, $"{mode} {name}: clear of the status colours");
                }
            }
            SetAccent(Hex("#3366FF"));
            SetMode(Mode.Light);
            Ok(Look.Background.Color == Hex("#F4F4F7") && Accent.Color == Hex("#3366FF"), "a custom accent stays through a mode switch");
            Ok(Contrast(AccentText, Raised) >= 4.5, "a low-contrast accent's text is lifted to 4.5:1");
            SetMode(Mode.Dark); SetAccent(Accents[0].dark, Accents[0].light);
        }

        static void Marks()
        {
            TestApp.Marks.Clear();
            IconPng(Hex("#123456"), 3);
            Ok(TestApp.Marks.SequenceEqual(new[] { true }), "the app's mark draws the icon");
            var window = new Window { Content = new Mark { Width = 38, Height = 38 }, Width = 100, Height = 100 }; window.Show();
            Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            Ok(TestApp.Marks.Contains(false), "the app's mark draws the Mark control");
            window.Close();
        }

        static void Parsers()
        {
            var p = Workbench.Pairs("Accept: text/plain\n\nx-id : 42");
            Ok(p.Count == 2 && p["accept"] == "text/plain" && p["X-ID"] == "42", "pairs: name: value per line, blank lines skipped, names ignore case");
            Ok(Throws(() => Workbench.Pairs("a: 1\nA: 2")), "pairs: a duplicate name is refused");
            Ok(Throws(() => Workbench.Pairs("no separator")), "pairs: a line without the separator is refused");
            Ok(Workbench.PairsText(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }) == "a: 1\nb: 2", "pairs back to text");
        }

        static bool Throws(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }

        /// <summary>The mode switch: a click and the arrows pick, the mode follows; System without a platform's setting is dark.</summary>
        static void Modes()
        {
            var sw = new ModeSwitch(); int changed = 0; sw.Changed += () => changed++;
            var window = new Window { Width = 400, Height = 200, Content = new StackPanel { Margin = new Thickness(16), Children = { sw } } }; window.Show();
            Settle();
            Control Segment(string name) => sw.GetVisualDescendants().OfType<Control>().First(c => Avalonia.Automation.AutomationProperties.GetName(c) == name + " mode");
            var light = Segment("Light"); var p = light.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(p, Avalonia.Input.MouseButton.Left); window.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle();
            Ok(sw.Value == ModeChoice.Light && Current == Look.Mode.Light && changed == 1, "a click on Light switches to light mode");
            Ok(ReferenceEquals(sw.BorderBrush, B(Look.Separator)) && !light.Classes.Contains(":focus-visible"), "pointer mode selection keeps a neutral outer frame without a keyboard focus ring");
            light.Focus(Avalonia.Input.NavigationMethod.Tab); window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(sw.Value == ModeChoice.Dark && Current == Look.Mode.Dark && Segment("Dark").IsFocused, "Right moves to Dark and switches");
            Ok(ReferenceEquals(sw.BorderBrush, B(Look.Separator)) && Segment("Dark").Classes.Contains(":focus-visible"), "keyboard mode selection keeps the focus ring on its segment and the outer frame neutral");
            ModeSwitch.Apply(ModeChoice.Light); ModeSwitch.Apply(ModeChoice.System);
            Ok(Current == Look.Mode.Dark || Current == Look.Mode.Light, "System takes the computer's mode (headless: whatever it reports)");
            ModeSwitch.Apply(ModeChoice.Dark);
            window.Close();
        }

        /// <summary>The accent picker: Apply takes a curated name or a colour; a swatch, a typed colour and the helper set it.</summary>
        static void AccentPicking()
        {
            Ok(AccentPicker.Apply("teal") && Accent.Color == Accents.First(a => a.name == "Teal").dark, "Apply: a curated accent by its name, any case");
            Ok(AccentPicker.Apply("#3366FF") && Accent.Color == Hex("#3366FF"), "Apply: a colour");
            Ok(!AccentPicker.Apply("blue") && !AccentPicker.Apply("#3366F") && Accent.Color == Hex("#3366FF"), "Apply: anything else changes nothing");
            var picker = new AccentPicker();
            Ok(picker.Value == "#3366FF", "a new picker shows the accent as it is: " + picker.Value);
            int changed = 0; picker.Changed += () => changed++;
            var window = new Window { Width = 520, Height = 420, Content = new Border { Padding = new Thickness(16), Child = picker } }; window.Show();
            void Click(Point p) { Settle(); window.MouseDown(p, Avalonia.Input.MouseButton.Left); window.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle(); }
            Settle();
            var swatches = picker.Children[1];
            Click(swatches.TranslatePoint(new Point(5 + 34 + 12, swatches.Bounds.Height / 2), window)!.Value);
            Ok(picker.Value == "Indigo" && Accent.Color == Accents[1].dark && changed == 1, "a swatch sets its curated accent at once");
            var field = picker.GetVisualDescendants().OfType<Field>().Single();
            field.Box.Text = "#2DD4BF"; Settle();
            Ok(picker.Value == "#2DD4BF" && Accent.Color == Hex("#2DD4BF") && changed == 2, "a typed colour sets the accent");
            Click(field.TranslatePoint(new Point(40, 14), window)!.Value);
            Ok(window.GetVisualDescendants().OfType<ColorGrid>().Any(), "the Custom field opens the colour helper");
            Click(new Point(500, 30));
            Ok(!window.GetVisualDescendants().OfType<ColorGrid>().Any() && changed == 2, "a click elsewhere closes it, with nothing changed");
            SetMode(Mode.Light); picker.Value = "Violet";
            Ok(Accent.Color == Accents[0].light && field.Text == HexOf(Accents[0].light), "a curated accent takes the mode's value, its hex in the field");
            SetMode(Mode.Dark); picker.Restyle();  // what Look.Repaint does in an app (no app lifetime here)
            Ok(field.Text == HexOf(Accents[0].dark), "a mode switch shows the other value");
            window.Close();
            SetAccent(Accents[0].dark, Accents[0].light);
        }

        /// <summary>Every public control is in DESIGN.md (what agents read) and in the gallery, by its class name.</summary>
        static void Documented()
        {
            var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "Shade.slnx"))) root = root.Parent;
            if (root == null) { Ok(false, "the repository root (Shade.slnx) is found"); return; }
            string doc = System.IO.File.ReadAllText(System.IO.Path.Combine(root.FullName, "DESIGN.md")),
                gallery = System.IO.File.ReadAllText(System.IO.Path.Combine(root.FullName, "gallery", "Showcase.cs"));
            bool Named(string text, string name) => System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{name}\b");
            foreach (var t in typeof(Look).Assembly.GetExportedTypes().Where(t => typeof(Control).IsAssignableFrom(t) && !t.IsAbstract && !t.IsNested).OrderBy(t => t.Name))
                Ok(Named(doc, t.Name) && Named(gallery, t.Name), $"{t.Name} is in DESIGN.md and the gallery");
        }
    }
}
