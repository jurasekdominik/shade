// Shared test apps, the view model and helpers.
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
    sealed class TestApp : Application
    {
        /// <summary>Each time the app's mark was drawn: true when as an icon.</summary>
        public static readonly List<bool> Marks = new List<bool>();
        public override void Initialize() => Look.Apply(this, "Tests", mark: (g, box, c, phase, icon) => { Marks.Add(icon); DrawMark(g, box, c, Look.Text, phase); });
    }

    sealed class HostThemeApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
            Look.Configure(new ShadeOptions { InstallFluentTheme = false });
            Look.Apply(this, "Host theme");
        }
    }

    public sealed record Employee(int Id, string Name, string Title) { public string SearchText => Title + " " + Name; }

    public sealed class Vm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public IReadOnlyList<Employee> Employees { get; } = new[] { new Employee(1, "Jan Novák", "Mgr."), new Employee(2, "Petra Šílá", "Bc.") };
        int? employeeId = 2;
        public int? EmployeeId { get => employeeId; set => Set(ref employeeId, value); }
        bool flag; bool? ticked = false; string name = "", query = "", pick = "Two", accent = "Violet"; double? share = 0.25;
        string? cardKey;
        public string? CardKey { get => cardKey; set => Set(ref cardKey, value); }
        IReadOnlyList<SummaryCards.Item> cards = new[] { new SummaryCards.Item { Title = "Checks", Value = "11/12" } };
        public bool Flag { get => flag; set => Set(ref flag, value); }
        EditableTable.PaperKind paperFormat;
        EditableTable.PaperDimensionsPosition paperPlacement;
        public EditableTable.PaperDimensionsPosition PaperPlacement { get => paperPlacement; set => Set(ref paperPlacement, value); }
        bool paperLandscape;
        bool constrainToPaper = true;
        public bool ConstrainToPaper { get => constrainToPaper; set => Set(ref constrainToPaper, value); }
        Size paperDimensions = new Size(210, 297);
        public EditableTable.PaperKind PaperFormat { get => paperFormat; set => Set(ref paperFormat, value); }
        public bool PaperLandscape { get => paperLandscape; set => Set(ref paperLandscape, value); }
        public Size PaperDimensions { get => paperDimensions; set => Set(ref paperDimensions, value); }

        public bool? Ticked { get => ticked; set => Set(ref ticked, value); }
        public string Name { get => name; set => Set(ref name, value); }
        string actionText = "Save";
        public string ActionText { get => actionText; set => Set(ref actionText, value); }
        ButtonKind actionKind = ButtonKind.Primary;
        public ButtonKind ActionKind { get => actionKind; set => Set(ref actionKind, value); }
        Geometry actionIcon = Icons.Plus;
        public Geometry ActionIcon { get => actionIcon; set => Set(ref actionIcon, value); }
        string recipients = "";
        public string Recipients { get => recipients; set => Set(ref recipients, value); }
        public string Query { get => query; set => Set(ref query, value); }
        public string Pick { get => pick; set => Set(ref pick, value); }
        IReadOnlyList<string> people = new[] { "Anna", "Ben", "Chloe" };
        public IReadOnlyList<string> People { get => people; set => Set(ref people, value); }
        IReadOnlySet<string> selectedPeople = new HashSet<string> { "Ben" };
        public IReadOnlySet<string> SelectedPeople { get => selectedPeople; set => Set(ref selectedPeople, value); }
        IReadOnlyList<string> shortPicks = new[] { "Ben" };
        public IReadOnlyList<string> ShortPicks { get => shortPicks; set => Set(ref shortPicks, value); }
        public System.Collections.ObjectModel.ObservableCollection<PairTable.Entry> PairRows { get; } = new() { new("Accept", "text/plain") };
        Color swatchColour = Color.Parse("#3366FF");
        public Color SwatchColour { get => swatchColour; set => Set(ref swatchColour, value); }
        public string Accent { get => accent; set => Set(ref accent, value); }
        ModeChoice mode = ModeChoice.Dark;
        public ModeChoice Mode { get => mode; set => Set(ref mode, value); }
        DateTime? day = new DateTime(2026, 10, 24);
        public DateTime? Day { get => day; set => Set(ref day, value); }
        DateTime? until = new DateTime(2026, 10, 30);
        public DateTime? Until { get => until; set => Set(ref until, value); }
        bool designing;
        public bool Designing { get => designing; set => Set(ref designing, value); }
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

    sealed class ValidationVm : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        string name = "";
        public string Name { get => name; set { name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Name))); } }
        public bool HasErrors => name.Length == 0;
        public System.Collections.IEnumerable GetErrors(string? propertyName) => HasErrors ? new[] { "Name is required" } : Array.Empty<string>();
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    }
}
