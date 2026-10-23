// Shade's checks without a screen: the colour rules, the parsers, and the controls in XAML with bindings.
// Exits 1 when one fails.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using static Shade.Look;

namespace Shade.Tests
{
    sealed class TestApp : Application
    {
        /// <summary>Each time the app's mark was drawn: true when as an icon.</summary>
        public static readonly List<bool> Marks = new List<bool>();
        public override void Initialize() => Look.Apply(this, "Tests", mark: (g, box, c, phase, icon) => { Marks.Add(icon); DrawMark(g, box, c, Look.Text, phase); });
    }

    public sealed class Vm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        bool flag; bool? ticked = false; string name = "", query = "", pick = "Two"; double? share = 0.25;
        IReadOnlyList<SummaryCards.Item> cards = new[] { new SummaryCards.Item { Title = "Checks", Value = "11/12" } };
        public bool Flag { get => flag; set => Set(ref flag, value); }
        public bool? Ticked { get => ticked; set => Set(ref ticked, value); }
        public string Name { get => name; set => Set(ref name, value); }
        public string Query { get => query; set => Set(ref query, value); }
        public string Pick { get => pick; set => Set(ref pick, value); }
        public double? Share { get => share; set => Set(ref share, value); }
        public IReadOnlyList<SummaryCards.Item> Cards { get => cards; set => Set(ref cards, value); }
        public Command Save { get; } = new Command();
    }

    public sealed class Command : ICommand
    {
        public int Runs; bool can = true;
        public bool Can { get => can; set { can = value; CanExecuteChanged?.Invoke(this, EventArgs.Empty); } }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => can;
        public void Execute(object? parameter) => Runs++;
    }

    static class Program
    {
        static int failed;
        static void Ok(bool cond, string what) { if (!cond) failed++; Console.WriteLine((cond ? "ok   " : "FAIL ") + what); }

        static int Main()
        {
            AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            Colours();
            Parsers();
            Xaml();
            Marks();
            Console.WriteLine(failed == 0 ? "all passed" : failed + " failed");
            return failed == 0 ? 0 : 1;
        }

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

        static void Xaml()
        {
            var vm = new Vm();
            var view = new Bound { DataContext = vm };
            var window = new Window { Content = view }; window.Show();
            Dispatcher.UIThread.RunJobs();
            T Get<T>(string name) where T : Control => view.FindControl<T>(name) ?? throw new Exception(name + " missing");

            var sw = Get<Switch>("Toggle");
            vm.Flag = true; Ok(sw.On, "Switch: the view model turns it on");
            sw.Toggle(); Ok(!vm.Flag, "Switch: a toggle reaches the view model");
            Ok(!Get<StateToggle>("State").On, "StateToggle follows the same value");

            var tick = Get<Tick>("Box");
            tick.Toggle(); Ok(vm.Ticked == true, "Tick: a click reaches the view model");
            vm.Ticked = null; Ok(tick.State == null, "Tick: the view model sets it to some");

            var field = Get<Field>("NameField");
            field.Box.Text = "Anna"; Dispatcher.UIThread.RunJobs(); Ok(vm.Name == "Anna", "Field: typing reaches the view model");
            vm.Name = "Ben"; Ok(field.Text == "Ben", "Field: the view model sets the text");
            var find = Get<SearchField>("Find");
            find.Box.Text = "rep"; Dispatcher.UIThread.RunJobs(); Ok(vm.Query == "rep", "SearchField binds like a field");

            var bar = Get<Progress>("Bar");
            Ok(bar.Value == 0.25 && bar.Label == "Uploading", "Progress: value bound, label set in XAML");
            vm.Share = null; Ok(bar.Value == null, "Progress: null = unknown length");
            vm.Share = 2; Ok(bar.Value == 1, "Progress: the value is clamped to 1");

            var cards = Get<SummaryCards>("Cards");
            Ok(cards.Items.Count == 1 && cards.Detailed, "SummaryCards: items bound, detailed set in XAML");
            vm.Cards = vm.Cards.Append(new SummaryCards.Item { Title = "Backups", Value = "6/6" }).ToList();
            Ok(cards.Items.Count == 2, "SummaryCards: new items follow");

            var save = Get<ActionButton>("Save");
            Ok(save.Text == "Save" && save.Kind == ButtonKind.Primary, "ActionButton: text and kind from XAML");
            save.PerformClick(); Ok(vm.Save.Runs == 1, "ActionButton: a click runs the command");
            vm.Save.Can = false; Ok(!save.IsEffectivelyEnabled, "ActionButton: the command's CanExecute disables it");
            save.PerformClick(); Ok(vm.Save.Runs == 1, "ActionButton: a disabled button doesn't run it");

            var choice = Get<ChoiceButton>("Choice");
            choice.SetItems(new[] { "One", "Two", "Three" }, vm.Pick);
            Ok(choice.Current == "Two", "ChoiceButton: the current item from the view model");
            vm.Pick = "Three"; Ok(choice.Current == "Three", "ChoiceButton: the view model picks");

            var blocks = Get<Masonry>("Blocks");
            Ok(blocks.MinColumn == 200 && blocks.MaxColumns == 2 && blocks.Gap == 32, "Masonry: settings from XAML, the rest default");
            window.Close();
        }
    }
}
