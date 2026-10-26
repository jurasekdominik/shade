using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using static Shade.Look;

namespace Shade;

/// <summary>Editable name/value rows. Blank and duplicate drafts remain in Entries until the application exports them.</summary>
public sealed class PairTable : StackPanel
{
    public sealed class Entry : INotifyPropertyChanged
    {
        string name = "", value = "";
        string? error;
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Name { get => name; set { value ??= ""; if (name == value) return; name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public string Value { get => value; set { value ??= ""; if (this.value == value) return; this.value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public string? Error { get => error; internal set { if (error == value) return; error = value; PropertyChanged?.Invoke(this, new(nameof(Error))); } }
        public Entry() { }
        public Entry(string name, string value) { Name = name; Value = value; }
    }
    public static readonly DirectProperty<PairTable, IList<Entry>> EntriesProperty = AvaloniaProperty.RegisterDirect<PairTable, IList<Entry>>(nameof(Entries), x => x.Entries, (x, value) => x.Entries = value);
    public static readonly DirectProperty<PairTable, bool> HasErrorsProperty = AvaloniaProperty.RegisterDirect<PairTable, bool>(nameof(HasErrors), x => x.HasErrors);
    public static readonly StyledProperty<bool> CaseSensitiveProperty = AvaloniaProperty.Register<PairTable, bool>(nameof(CaseSensitive), true);
    public static readonly StyledProperty<string> KeyHintProperty = AvaloniaProperty.Register<PairTable, string>(nameof(KeyHint), "");
    public static readonly StyledProperty<string> ValueHintProperty = AvaloniaProperty.Register<PairTable, string>(nameof(ValueHint), "");
    public static readonly DirectProperty<PairTable, IReadOnlyList<string>> CommonProperty = AvaloniaProperty.RegisterDirect<PairTable, IReadOnlyList<string>>(nameof(Common), x => x.Common, (x, value) => x.Common = value);
    IList<Entry> entries = new ObservableCollection<Entry>();
    IReadOnlyList<string> common = Array.Empty<string>();
    bool hasErrors, attached;
    INotifyCollectionChanged? observable;
    readonly HashSet<Entry> watched = new();
    readonly Dictionary<Entry, (Grid line, Field key, Field value, TextBlock error, ActionButton remove)> views = new();
    readonly StackPanel rows = new() { Spacing = M(4) };
    readonly Grid head;
    readonly ActionButton add, pick;
    public event Action? Changed;
    public IList<Entry> Entries
    {
        get => entries;
        set
        {
            ArgumentNullException.ThrowIfNull(value); Check(value);
            if (ReferenceEquals(entries, value)) return;
            Unobserve(); SetAndRaise(EntriesProperty, ref entries, value); Observe(); Sync();
        }
    }
    public bool HasErrors => hasErrors;
    public bool CaseSensitive { get => GetValue(CaseSensitiveProperty); set => SetValue(CaseSensitiveProperty, value); }
    public string KeyHint { get => GetValue(KeyHintProperty); set => SetValue(KeyHintProperty, value); }
    public string ValueHint { get => GetValue(ValueHintProperty); set => SetValue(ValueHintProperty, value); }
    public IReadOnlyList<string> Common { get => common; set { ArgumentNullException.ThrowIfNull(value); SetAndRaise(CommonProperty, ref common, Array.AsReadOnly(value.Distinct(StringComparer.Ordinal).ToArray())); pick.IsVisible = common.Count > 0 && !entries.IsReadOnly; } }
    public PairTable() : this("", "") { }
    public PairTable(string keyHint, string valueHint, IReadOnlyList<string>? common = null)
    {
        Spacing = M(6); KeyHint = keyHint; ValueHint = valueHint;
        head = Grid(Words(Build.Text("", Text2, TextSize.Label, 500), text => text.Text = T("Name")), Words(Build.Text("", Text2, TextSize.Label, 500), text => text.Text = T("Value")), new Border { Width = M(28) });
        add = Words(new ActionButton(T("Add"), ButtonKind.Ghost, Icons.Plus), button => button.Content = T("Add"));
        add.Click += (_, _) => { var entry = Append("", ""); views[entry].key.Box.Focus(); Changed?.Invoke(); };
        pick = Words(new ActionButton(T("Common ▾"), ButtonKind.Ghost), button => { button.Content = T("Common ▾"); ToolTip.SetTip(button, T("Add a well-known name")); });
        pick.Click += (_, _) => Menu.Show(pick, this.common.ToList(), i => { if (entries.IsReadOnly) return; var entry = entries.FirstOrDefault(x => x.Name.Trim() == "" && x.Value.Trim() == "") ?? Append("", ""); entry.Name = this.common[i]; views[entry].value.Box.Focus(); Changed?.Invoke(); }, width: M(260));
        Children.Add(head); Children.Add(rows); Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(4), Children = { add, pick } });
        Common = common ?? Array.Empty<string>(); Sync();
        AttachedToVisualTree += (_, _) => { attached = true; Observe(); Sync(); };
        DetachedFromVisualTree += (_, _) => { attached = false; Unobserve(); };
        Words(this, _ => Validate(), "validation");
    }
    static void Check(IList<Entry> value)
    {
        if (value.Any(x => x == null) || value.Distinct().Count() != value.Count) throw new ArgumentException("Pair entries must be non-null, distinct row instances.", nameof(value));
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CaseSensitiveProperty && views != null) Validate();
        if ((change.Property == KeyHintProperty || change.Property == ValueHintProperty) && views != null)
            foreach (var view in views.Values) { view.key.PlaceholderText = KeyHint; view.value.PlaceholderText = ValueHint; }
    }
    void Observe()
    {
        if (!attached) return;
        observable = entries as INotifyCollectionChanged; if (observable != null) observable.CollectionChanged += CollectionChanged;
        foreach (var entry in entries) if (watched.Add(entry)) entry.PropertyChanged += EntryChanged;
    }
    void Unobserve()
    {
        if (observable != null) observable.CollectionChanged -= CollectionChanged; observable = null;
        foreach (var entry in watched) entry.PropertyChanged -= EntryChanged; watched.Clear();
    }
    void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { Check(entries); Unobserve(); Observe(); Sync(); }
    void EntryChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName is nameof(Entry.Name) or nameof(Entry.Value) or null or "") Validate(); }
    static Grid Grid(Control key, Control value, Control end)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,8,3*,4,Auto") };
        Avalonia.Controls.Grid.SetColumn(value, 2); Avalonia.Controls.Grid.SetColumn(end, 4);
        grid.Children.Add(key); grid.Children.Add(value); grid.Children.Add(end); return grid;
    }
    void Sync()
    {
        Check(entries);
        foreach (var entry in views.Keys.Except(entries).ToArray()) { rows.Children.Remove(views[entry].line); views.Remove(entry); }
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!views.TryGetValue(entry, out var view))
            {
                var key = new Field(KeyHint); var value = new Field(ValueHint);
                key.Bind(Field.TextProperty, new Binding(nameof(Entry.Name)) { Source = entry, Mode = BindingMode.TwoWay });
                value.Bind(Field.TextProperty, new Binding(nameof(Entry.Value)) { Source = entry, Mode = BindingMode.TwoWay });
                Avalonia.Automation.AutomationProperties.SetName(key.Box, T("Name")); Avalonia.Automation.AutomationProperties.SetName(value.Box, T("Value"));
                var error = Build.Text("", DangerText, TextSize.Label); error.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
                var remove = Words(new ActionButton("", ButtonKind.Ghost, Icons.X) { Width = M(28), MinWidth = M(28), VerticalAlignment = VerticalAlignment.Top }, button => { ToolTip.SetTip(button, T("Remove")); Avalonia.Automation.AutomationProperties.SetName(button, T("Remove")); });
                var line = Grid(new StackPanel { Spacing = M(4), Children = { key, error } }, value, remove);
                value.VerticalAlignment = VerticalAlignment.Top;
                remove.Click += (_, _) => { if (entries.IsReadOnly) return; entries.Remove(entry); Sync(); Changed?.Invoke(); };
                key.Edited += () => { Validate(); Changed?.Invoke(); }; value.Edited += () => Changed?.Invoke();
                view = (line, key, value, error, remove); views.Add(entry, view);
            }
            int at = rows.Children.IndexOf(view.line);
            if (at < 0) rows.Children.Insert(i, view.line); else if (at != i) rows.Children.Move(at, i);
            view.remove.IsVisible = !entries.IsReadOnly;
        }
        head.IsVisible = entries.Count > 0; add.IsVisible = !entries.IsReadOnly; pick.IsVisible = common.Count > 0 && !entries.IsReadOnly; Validate();
    }
    void Validate()
    {
        var comparer = CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var duplicates = entries.Where(x => x.Name.Trim() != "").GroupBy(x => x.Name.Trim(), comparer).Where(x => x.Count() > 1).SelectMany(x => x).ToHashSet();
        foreach (var entry in entries)
        {
            entry.Error = duplicates.Contains(entry) ? T("Duplicate name: {0}", entry.Name.Trim()) : null;
            if (views.TryGetValue(entry, out var view)) { view.key.Error = entry.Error; Avalonia.Automation.AutomationProperties.SetHelpText(view.key.Box, entry.Error ?? ""); view.error.Text = entry.Error; view.error.IsVisible = entry.Error != null; }
        }
        SetAndRaise(HasErrorsProperty, ref hasErrors, duplicates.Count > 0);
    }
    Entry Append(string name, string value)
    {
        if (entries.IsReadOnly) throw new InvalidOperationException("Adding pair rows requires a mutable Entries collection.");
        var entry = new Entry(name, value); entries.Add(entry); Sync(); return entry;
    }
    /// <summary>Exports valid non-blank names using CaseSensitive; duplicate drafts remain editable on failure.</summary>
    public bool TryGetPairs(out Dictionary<string, string> pairs)
    {
        Validate(); pairs = new(CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        if (HasErrors) return false;
        foreach (var entry in entries) if (entry.Name.Trim() != "") pairs.Add(entry.Name.Trim(), entry.Value);
        return true;
    }
    /// <summary>Dictionary adapter; duplicate names throw without discarding drafts.</summary>
    public Dictionary<string, string> Pairs(bool? caseSensitive = null)
    {
        var result = new Dictionary<string, string>((caseSensitive ?? CaseSensitive) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries) if (entry.Name.Trim() != "" && !result.TryAdd(entry.Name.Trim(), entry.Value)) throw new ArgumentException("Duplicate name: " + entry.Name.Trim());
        return result;
    }
    /// <summary>Legacy lossy dictionary adapter. Use Entries to retain every blank/duplicate draft.</summary>
    public Dictionary<string, string> Draft()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries) if (entry.Name.Trim() != "") result.TryAdd(entry.Name.Trim(), entry.Value);
        return result;
    }
    public void Set(IDictionary<string, string> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        if (entries.Select(x => (x.Name, x.Value)).SequenceEqual(pairs.Select(x => (x.Key, x.Value)))) return;
        Entries = new ObservableCollection<Entry>(pairs.Select(x => new Entry(x.Key, x.Value)));
    }
    public void Ensure(IEnumerable<string> names)
    {
        var have = new HashSet<string>(entries.Select(x => x.Name.Trim()), CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        foreach (var name in names) if (have.Add(name)) Append(name, "");
    }
}
