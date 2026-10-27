# Adopting Shade

Shade supplies the look of desktop work tools. The app owns its data, storage, permissions, validation rules,
translations and navigation. Configure Shade before `Look.Apply` and before creating views; keep app settings out of the library.

Link your app's `AGENTS.md` to [APP_AGENTS.md](APP_AGENTS.md) for the shared UI rules and for filing Shade issues. Use the
documentation of the installed version. Changes between versions are in [CHANGELOG.md](../CHANGELOG.md).

## Startup

```csharp
Look.Configure(new ShadeOptions
{
    Culture = CultureInfo.GetCultureInfo("cs-CZ"),
    TextScale = 1, Density = 1, ReducedMotion = false
});
Look.Translate = key => resources.GetString(key) ?? key;
Look.Apply(app, "My application", mark: DrawApplicationMark);
```

- `ShadeOptions` also takes `UiFont` and `MonoFont` (available to Avalonia). `Density` and `TextScale` accept finite values from 1 to 2 and
  grow input/action heights, date geometry, summaries, progress, navigation, tick grids and table-designer rows.
  Set them once before `Look.Apply` (no runtime change); app-fixed widths need review at your scale. Mode and accent switch live. Configuration is app-wide, not per window.
- `ReducedMotion` is an app preference (Shade does not read the OS). It stops switch/action transitions, entry effects, sidebar rolling and indeterminate progress.
- A host that already has a compatible Avalonia base theme sets `InstallFluentTheme = false`; Shade then adds its controls without a second Fluent theme.

## Forms and validation

```csharp
var name = new Field { Error = "Enter a name" };
var labelled = Build.Labelled("Name", name);
```

`Error`, `Invalid`, `Pending` and `IsReadOnly` are Avalonia properties. A binding on `Field.Text` takes part in the platform
validation pipeline (`INotifyDataErrorInfo` included); `Error` is an explicit message and wins over binding errors, and
`Invalid` alone shows no message. `Build.Labelled` links the label to the editor and shows its message beneath. Keep required and
business rules in the view model. Date fields bind `Min` and `Max`. `Field` exposes native `PlaceholderText`; `Box` is the escape hatch for native editing options and IME.
`ListField` has a parameterless constructor, bindable `Hint` and two-way `Text`; user edits raise `Changed`, and setting the same normalized text keeps row editors and focus.
`HostPortField` binds `DefaultPort`, `SearchField` binds `Count`.

## Translation and culture

`Look.Translate` maps an English key to text and must return the key when missing. For plurals set `Look.Localize` instead; it
gets the English template and arguments, returns the final string, and wins over `Translate`. Set both hooks before creating controls.
To switch language in a live view, update the hooks and `Look.Culture` on the UI thread, then call `Look.RefreshLanguage()`. Built-in labels, tooltips,
calendar and automation names refresh; app titles, user input and custom placeholders stay yours. Table refresh keeps an invalid draft and caret.

`Look.Culture` names calendar parts, sorts table text and formats translation arguments. It does not change stored values,
date input (ISO text and the documented formats) or layout direction: right-to-left layout is not supported.

```csharp
var amount = new EditableTable.Column("amount", "Amount", EditableTable.Kind.Number)
{
    InputCulture = CultureInfo.GetCultureInfo("cs-CZ"),
    Display = value => value.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ"))
};
```

`InputCulture` parses, `Display` presents, stored values stay as typed. `Parse` replaces the culture with your parser (null = invalid).
Always set the culture: the default accepts comma or dot as decimal separator and is ambiguous for grouped numbers.

## Actions and async work

`ActionButton`, `IconButton` and `Capsule` derive from Avalonia `Button`: `Content`, `ContentTemplate`, `Command`, routed `Click`,
default/cancel buttons and button automation work as in Avalonia. `Kind`, `Icon`, `IconSize`, `IconColor`, `Live` and `Dimmed` are styled properties;
`Armed` and `IsBusy` notify bindings (`IconButton` needs an automation name). `HorizontalContentAlignment` aligns the label, and `Focusable = false` keeps keyboard focus in an editor (toolbars).
`HoldMs` and `ConfirmText` bind; hold actions need a pointer hold and are not bypassed by keyboard or automation; confirmation changes the caption only.
Explicit `Height` is honored; prefer `Look.ControlHeight` and review compact heights at your font and density.

```csharp
var save = new ActionButton("Save") { AsyncAction = cancellation => SaveAsync(cancellation) };
save.Failed += error => ShowError(error.Message);
```

Use one path: `Command`/`Click`, or `AsyncAction` (it wins; `Content` and `Command` bind as usual). While it runs `IsBusy` is true and the button cannot run again. `Cancel()` and visual detach
request cancellation, which the action must observe. Failure sets `LastError` and raises `Failed` (cancellation is not failure; do not throw from handlers).
`RunAsync` runs on the UI thread; `PerformClick` also applies confirmation and hold.

## Toggles and short lists

`Switch`, `StateToggle` and `FilterChip` derive from `ToggleButton`, `Tick` from `CheckBox`, keeping Shade's faces and animation.

```xml
<shade:Switch IsChecked="{Binding Enabled}" />
<shade:StateToggle IsChecked="{Binding DeviceState}" Pending="{Binding Pending}"
                   ShowUndo="True" UndoCommand="{Binding RestoreDeviceState}" />
<shade:FilterChip Content="Errors" Count="{Binding ErrorCount}" IsChecked="{Binding ShowErrors}" />
<shade:TickGrid Items="{Binding People}" Picked="{Binding SelectedPeopleInOrder}" />
```

- State is two-way `IsChecked`; `StateToggle` uses `null` for unknown (OFF/ON otherwise, `UnknownText` binds). `Click` is for user actions;
  `IsCheckedChanged` sees every change. Parent `Click` listeners get child events, so check `e.Source == sender`. Mixed values become checked on first activation unless `IsThreeState`.
- `StateToggle.UndoCommand` (+ `UndoCommandParameter`) does not change the state itself. `StateLook.On` is display only.
- `FilterChip` takes `Content`/`ContentTemplate` plus `Count`, `CountColor` and `Dot`.
- `TickGrid` snapshots `Items` and two-way ordered `Picked`: replace collections, in-place changes are not seen. User clicks raise `Changed`; programmatic picks only notify bindings.
- `Tag` binds `Text`, `Tone`, `Removable`, `RemoveCommand`, `RemoveCommandParameter`. `StatusBadge` binds `Text` and `Kind`. `NavigationButtons` binds `CanGoBack`, `CanGoForward`,
  `Collapsed`, `BackCommand` and `ForwardCommand`. `ModeSwitch` segments are native radios.

## Pickers and menus

```xml
<shade:ChoiceButton Items="{Binding Projects}" Current="{Binding Project}" Extra="Create project" />
<shade:MultiChoice Items="{Binding People}" Selected="{Binding SelectedPeople}" EmptyText="Everyone" />
<shade:SuggestionField Items="{Binding Names}" Text="{Binding Query}" PlaceholderText="Find a name" />
```

**ChoiceButton** (a `Button`): `Items` is a distinct ordinal snapshot; `Current` is two-way and may arrive before the items. A current value missing from a new list stays shown;
it never picks another. `Extra` is an action even with no options (`ExtraPicked`). `TextColor`, `Overridden`, `ItemColor` and `ItemIcon` bind; `Expanded` notifies.
Programmatic changes do not raise `Picked`. It is a compact string picker, not a templated or remote list.

**MultiChoice**: `Items` (`IReadOnlyList<string>`) and `Selected` (`IReadOnlySet<string>`, two-way) are copied snapshots: replace them, in-place changes are not seen.
Keys are ordinal and unique, in supplied order. With empty `Items` selected keys are kept (loading); a non-empty list drops unknown keys; clear with an empty set or `Set`.
`Label` is a fixed caption, `EmptyText = null` uses localized "All", more than two show `"{0} selected"`. Programmatic updates do not raise `Changed`; user toggles do.
No separate label/key model and no remote loading.

**SuggestionField** keeps the native editor and NameSearch ranking. `Items` is a distinct sorted snapshot; `ApplySelection` binds and can leave the text alone.
Arrows move through the popup rows without leaving the editor; read-only, disabled or hidden fields commit nothing.

**SearchChoice** is a native `ComboBox` with a search box atop its popup; words match in any order, ignoring case and accents (`Look.Culture`), in source order.

```xml
<shade:SearchChoice ItemsSource="{Binding Employees}" DisplayMemberBinding="{Binding DisplayName}"
                    SearchMemberBinding="{Binding SearchText}" SelectedValueBinding="{Binding Id}"
                    SelectedValue="{Binding EmployeeId}" PlaceholderText="Choose employee"
                    SearchPlaceholderText="Title, first or last name" />
```

Items are your objects; `SearchText` is whatever you want searchable. Bind `SelectedValue` (with `SelectedValueBinding`) or native `SelectedItem`. Default rows highlight matches;
a custom `ItemTemplate` calls `Build.Mark(textBlock, label, query)` and still needs a searchable binding; `SelectionBoxItemTemplate` sets the closed face. Filtering
never changes the selection; Enter or click commits (`SelectionCommitted` also fires for the same item), Escape cancels. `NoResultsText` defaults to localized "No matches".
Use an observable `ItemsSource`; after editing searchable properties in place call `RefreshSearch()`. It filters loaded data only: no remote paging or debounce, no multi-select.

**Menus** use native buttons: arrows, Home/End and typeahead move focus, Enter/Space activate, Escape restores the trigger, Tab leaves, outside clicks keep the
other control's focus. Disable, hide or detach closes owned popups; replacing options closes stale actions. Native expand/collapse and toggle providers cover automation.

## Pair rows

```xml
<shade:PairTable Entries="{Binding Rows}" CaseSensitive="False" KeyHint="Name" ValueHint="Value" />
```

`Entries` takes an `IList<PairTable.Entry>` (`ObservableCollection` for live changes, UI thread only; a plain list needs a new reference). Each `Entry` has notifying `Name`, `Value` and read-only `Error`.
Names compare trimmed and ordinal (`CaseSensitive = false` ignores case); blank and duplicate names stay in the collection, and
duplicates show errors on both rows (`HasErrors` notifies). `TryGetPairs(out pairs)` exports non-blank names only when valid; on failure it returns false and leaves every draft. The app decides when to save.
Model updates do not raise `Changed`; user add/remove/edit raises it once. `KeyHint`, `ValueHint` and `Common` bind. C# adapters: `Set`, `Ensure`, `Pairs(bool?)` (throws on duplicates), `Draft` (keeps the first duplicate; use `Entries` for full drafts).

## Tables

`Rows`, `Columns` and `IsReadOnly` are Avalonia properties. Use `ObservableCollection` for structural changes while attached (coalesced on the UI dispatcher, UI thread only);
fixed-size collections hide Add/Delete/reorder but keep cell editing. Column keys are unique and non-empty, rows distinct and non-null. Direct dictionary or column-property changes need `Refresh()`.

- **Edits**: `TrySetValue(row, column, text)` rejects read-only, computed and non-numeric input, then calls `Validate`; return an error string to reject. Success raises `CellEdited`
  (row, column, old, new) and `Changed`; rejections set `LastValidationError`. Writing dictionaries directly bypasses all checks. An invalid numeric/unit draft shows a danger border and message, keeps the stored value,
  survives theme/language changes and outside clicks, and is cancelled by Escape. The app owns save transactions.
- **Look**: columns override `HeadingSize` and `SubheadingSize` (null inherits), and set `Italic` with `Bold`. Alignment is `Row.Alignments[key]`, then `Column.Alignment`, then automatic (numbers right):

```csharp
code.Alignment = Avalonia.Media.TextAlignment.Left; // the column
row.Alignments[code.Key] = Avalonia.Media.TextAlignment.Center; // one cell
table.Refresh();
```

- **Size**: only the viewport, a buffer and the focused row are realized; place the table in a bounded layout (a Grid star row), not an unbounded `StackPanel`. All rows stay in memory (no paging), and Fit
  and footers read every loaded row. There is no column virtualization; prefer fixed widths if profiling shows Fit dominating. `dotnet run --project tests/Shade.Tests -c Release -- --scale 10000 10` checks 10,000 rows
  (`--unique --footers`, `--fixed`).
- **Design pointers**: Ctrl-drag a heading to reorder (Ctrl+Left/Right by keyboard; Escape cancels); resize from the 12 px target inside its right edge, double-click fits. `TableDesigner` binds `Columns`, `Rows`, `Locked` and `EmptyText`; replace data to rebuild previews.

### Column ribbon and paper

`ShowColumnEditor = true` adds the toolbar and inline ribbon (default false); Edit columns/Done toggles `Design`. It covers alignment, units, formulas, typography, bounded widths and paper formats.

```xml
<shade:EditableTable ShowColumnEditor="True" Design="{Binding EditingColumns}"
                     PaperFormat="A4" PaperLandscape="{Binding Landscape}"
                     CustomPaperSize="{Binding CustomPaperSize}" ConstrainToPaper="{Binding PreviewPaper}" />
```

- Presets: Screen, A5, A4, A3, Letter, Legal, Custom. `PaperFormat`, `PaperLandscape` and `CustomPaperSize` are validated, two-way properties (the ribbon uses `SetCurrentValue`, so bindings survive).
  `PaperSize` is the effective size in mm (null for Screen) for your print/export; printing is yours. Preview width is 96 DIPs per inch, capped by the host.
- `ConstrainToPaper` (default true) caps the preview to the paper; false edits full width while keeping the paper settings. The ribbon's Viewport choice flips it. Viewport changes and resizing never raise `Changed` or rewrite widths.
- `PaperDimensionsPlacement` is `Bottom` (default) or `FormatLabel` (dimensions beside the caption). The web grid takes `options.paperDimensionsPlacement`.
- Size properties keep their binding defaults; use `Mode=TwoWay` to receive ribbon changes. Ribbon numbers use `Look.Culture` (cs-CZ accepts `60,5`; the web grid uses `data-culture`) and show at most two decimals without altering exact geometry.
- Done validates every ribbon field, commits valid drafts and stays in Design on an error. Escape in a field restores its last value. Unit drafts commit to their original column; paper drafts belong to the table.
- The ribbon does not set `Decimals`, date/currency rules, allowed presets or host validation callbacks ([BACKLOG.md](BACKLOG.md)).

## Sidebar and accent

`Sidebar.ExpandedWidth` is bindable (DIPs); the brand wraps and a visible mark gets reserved room, e.g. `new Sidebar(name, tagline, entries) { ExpandedWidth = 260 }`.
`AccentPicker.Value` follows global accent changes as well as its own. A `ColorGrid` is independent unless you wire it:

```csharp
var helper = new ColorGrid(Look.Accent);
helper.Bind(ColorGrid.ValueProperty, new Avalonia.Data.Binding("Color")
{
    Source = Look.B(Look.Accent), Mode = Avalonia.Data.BindingMode.OneWay
});
helper.Picked += (colour, _) => Look.SetAccent(colour);
helper.PickedCurated += i => Look.SetAccent(Look.Accents[i].dark, Look.Accents[i].light);
```

## Accessibility
Buttons expose invoke, switches/ticks/state toggles toggle, date fields and table cells value; tabs have selection with roving focus. Give icon-only controls and pickers an automation name or visible label.
Focus borders keep 3:1 contrast. Screen-reader checks per platform, full grid semantics and calendar-day navigation still need manual review.
