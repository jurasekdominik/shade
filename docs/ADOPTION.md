# Adopting Shade 0.6

Shade supplies the visual vocabulary for desktop work tools. The application owns its data, storage, permissions,
validation rules, translations and navigation. Configure Shade before `Look.Apply` and before creating views; do not store app settings in the library.

## Startup configuration

```csharp
Look.Configure(new ShadeOptions
{
    Culture = CultureInfo.GetCultureInfo("cs-CZ"),
    TextScale = 1,
    Density = 1,
    ReducedMotion = false
});
Look.Translate = key => resources.GetString(key) ?? key;
Look.Apply(app, "My application", mark: DrawApplicationMark);
```

`ShadeOptions` also accepts `UiFont` and `MonoFont`. Fonts must be available to Avalonia. Density and text scale accept
finite values from 1 to 2. Defaults preserve Shade's existing appearance. They expand input/action heights, date/calendar geometry, summaries, progress, navigation, tick grids and table-designer rows.
The gallery can render a 2x text pass with `--docs <scratch-root> --scale 2`. Set metrics/fonts once at startup, before
`Look.Apply` installs control themes; changing them at runtime is not supported. Restart with the new configuration.
Application-supplied fixed widths and compact badge/tag geometry still need review at the chosen
scale; arbitrary application layouts are not guaranteed. Mode and accent continue to switch live.
Reduced motion suppresses switch/action transitions, entry effects, sidebar rolling and indeterminate progress animation.
It is an application preference; the library does not discover or persist OS preferences.

When the host already supplies a compatible Avalonia base theme, configure `InstallFluentTheme = false` before
`Look.Apply`. Shade then installs its controls/styles without adding a second Fluent theme. This does not remove the
Fluent dependency or embedded fonts from the package. Keep the default when Shade supplies the base theme.

All configuration is application-wide. Independent per-window brands, cultures and density scopes are not supported.

## Forms and validation

```csharp
var name = new Field { Error = "Enter a name", IsReadOnly = false };
var labelled = Build.Labelled("Name", name);
```

`Error`, `Invalid`, `Pending` and `IsReadOnly` are Avalonia properties. A binding to `Field.Text` supports the platform's
validation pipeline, including `INotifyDataErrorInfo`. `Build.Labelled` links the visible label to the editor and displays
its validation message underneath. `Error` is an explicit application message and takes precedence over binding errors.
The red `Invalid` flag alone has no message. Keep required/business rules in the view model and return a useful error.
Date fields expose bindable `Min` and `Max`; `ActionButton` exposes native `Content` plus bindable `HoldMs` and `ConfirmText`.
`ListField` has a parameterless constructor, bindable `Hint` and two-way `Text`; user edits raise `Changed`. Setting the
normalized text to its existing value preserves its row editors and focus.

## Translation and culture

`Translate` remains compatible. For plural forms, set `Localize` instead; it receives the English template and arguments
and returns the final formatted string. It takes precedence over `Translate`. Choose plural rules in the application,
using its preferred localization system. Configure both hooks before constructing controls. To switch an existing view, update the hooks and `Look.Culture`
on the UI thread, then call `Look.RefreshLanguage()`. Built-in labels, tooltips, calendar names and automation names
refresh; application titles, user input and custom placeholders remain application-owned. Table language refresh preserves
an active invalid draft and caret without writing it into the model or raising edit events. The registry uses weak keys,
so discarded controls are collectible. Missing keys must return the original English text.

`Look.Culture` controls built-in calendar labels, textual table sorting and formatted translation arguments, not stored values or every input
format. Drawing shapes text with the culture's direction; this is not a complete RTL layout contract. Dates retain ISO text and the documented input formats. Right-to-left layouts and full culture-driven date input
remain separate work; do not advertise them as supported.

```csharp
var amount = new EditableTable.Column("amount", "Amount", EditableTable.Kind.Number)
{
    InputCulture = CultureInfo.GetCultureInfo("cs-CZ"),
    Display = value => value.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ"))
};
```

`InputCulture` determines parsing, `Display` determines presentation, and stored row values remain as typed.
`Parse` can override the culture with an application parser, returning null for invalid input. Set the culture explicitly:
the compatibility default accepts comma or dot as a decimal separator and is ambiguous for grouped numbers.

## Native toggles and short lists

`Switch`, `StateToggle` and `FilterChip` derive from Avalonia `ToggleButton`; `Tick` derives from `CheckBox`. They retain
Shade's drawn faces and switch animation while using native commands, pointer capture, keyboard input and automation.

```xml
<shade:Switch IsChecked="{Binding Enabled}" />
<shade:Tick IsChecked="{Binding Included}" />
<shade:StateToggle IsChecked="{Binding DeviceState}" Pending="{Binding Pending}"
                   ShowUndo="True" UndoCommand="{Binding RestoreDeviceState}" />
<shade:FilterChip Content="Errors" Count="{Binding ErrorCount}" IsChecked="{Binding ShowErrors}" />
<shade:TickGrid Items="{Binding People}" Picked="{Binding SelectedPeopleInOrder}" />
```

| Previous contract | 0.6 replacement |
| --- | --- |
| `Switch.On`, `Tick.State`, `StateToggle.On`, `FilterChip.Active` and their property fields | Native two-way `IsChecked` / `ToggleButton.IsCheckedProperty`. |
| `StateToggle.Unknown = true` | `IsChecked = null`; false/true show OFF/ON. |
| Custom `Toggled` / zero-argument chip `Click` | Native routed `Click` for user actions; `IsCheckedChanged` observes all state changes, including programmatic ones. |
| `Switch.Toggle()` / `Tick.Toggle()` | Update the bound view-model value; native input and automation perform user activation. |
| `StateToggle.Undo` callback | `UndoCommand` and optional `UndoCommandParameter`, on a separate native icon button. |

A programmatic state update does not raise `Click`. Space activates on release using native button behavior. Tick/state
mixed values become checked on the first activation unless native `IsThreeState` cycling is enabled. `StateLook.On`
remains a bindable display-only property. `ShowUndo`, `UnknownText` and `Pending` bind; parent `Click` listeners receive
routed child events, so check `e.Source == sender` when observing only the state toggle itself. Undo does not change its state.
`FilterChip` uses native `Content`/`ContentTemplate`, plus bindable `Count`, `CountColor` and `Dot`.

TickGrid snapshots `Items` and two-way ordered `Picked`. Replace collections to update them; in-place notifications are
not observed. User row clicks raise `Changed`; programmatic selection only notifies bindings. Its C# constructor retains
custom names/details, switch faces and initial selected-order behavior. Whole rows provide native checkbox targets.

## Single selection and menus

```xml
<shade:ChoiceButton Items="{Binding Projects}" Current="{Binding Project}" Extra="Create project" />
<shade:SuggestionField Items="{Binding Names}" Text="{Binding Query}" PlaceholderText="Find a name" />
```

ChoiceButton now extends native Button. `Items` is an ordinal, distinct snapshot; `Current` binds two ways and may
arrive before options. An explicit current value remains displayed even if it is not in a replacement list. It never
silently picks a different value. `Extra` is an action, including when options are empty; observe `ExtraPicked` separately.
`TextColor`, `Overridden`, `ItemColor` and `ItemIcon` are bindable properties; the delegate-based item appearance remains
available to C# view models. `SetItems` is an adapter. Programmatic updates do not emit `Picked`; `Expanded` notifies observers.

Menu rows use native buttons with invoke automation and real keyboard focus. Arrows/Home/End and typeahead move focus;
Enter/Space activate. Escape restores the trigger, Tab continues through the host, and outside dismissal preserves the
outside control's focus. Effective disable, ancestor hide and detach close owned popups. Option replacement closes stale actions.
This is a compact string picker, not a generic templated data/remote-loading framework.

SuggestionField retains the native text editor and NameSearch ranking. `Items` binds as a distinct sorted snapshot;
`ApplySelection` binds and can leave text intact for document/action workflows. Arrows update the existing popup rows
without rebuilding them or moving focus out of the editor. Read-only/disabled/hidden fields cannot commit stale suggestions.

## Editable pair rows

```csharp
var rows = new ObservableCollection<PairTable.Entry>
{
    new("Accept", "application/json"),
    new("accept", "text/plain")
};
```

```xml
<shade:PairTable Entries="{Binding Rows}" CaseSensitive="False" KeyHint="Name" ValueHint="Value" />
```

`Entries` accepts an `IList<Entry>`; use `ObservableCollection<Entry>` for live structure changes. Each distinct non-null
row has notifying `Name`, `Value` and read-only `Error`. Edits mutate those row objects; add/remove mutates the collection.
Mutate on the UI thread. Subscriptions follow attachment, and reattachment reconciles detached changes. Plain-list structure
changes require replacing `Entries` with a new list reference; fixed-size structures hide add/remove actions but their row objects remain editable.
Unchanged row objects keep their editors when another row is added. `KeyHint`, `ValueHint` and `Common` bind.

Blank and duplicate names stay in the collection. Names compare trimmed, ordinally; `CaseSensitive = false` checks header
names without case. Both duplicate rows show errors; `HasErrors` notifies, and live language refresh updates messages.
`TryGetPairs(out pairs)` exports non-blank names only when valid, using the configured comparer. Failure returns false
and an empty result while preserving every draft. The host decides when to save. Programmatic model updates do not raise
`Changed`; user add/remove/edit raises it once.

`Set`, `Ensure`, `Pairs(bool?)` and `Draft` remain C# adapters. `Set` replaces rows, preserving editors only when every row
already matches; use `Entries` for live drafts. `Pairs(bool?)` throws on duplicates using the configured comparer unless explicitly overridden. `Draft`
keeps the first duplicate in a lossy dictionary: use `Entries` to retain complete drafts.

## Other integration changes

- Tag has bindable `Text`, `Tone`, `Removable`, `RemoveCommand` and `RemoveCommandParameter`; removal uses a native button.
- Field exposes native-owned `PlaceholderText`; `Box` remains the native editing-options/IME escape hatch. Date/host/search
  fields remain composites rather than forcing segmented editors through a single TextBox base. Clear and undo targets
  use native buttons and respect read-only state. HostPortField has a parameterless constructor and bindable `DefaultPort`.
- SearchField `Count` binds. TableDesigner `Columns`, `Rows`, `Locked` and `EmptyText` bind; replace data to rebuild previews.
- NavigationButtons binds `CanGoBack`, `CanGoForward`, `Collapsed`, `BackCommand` and `ForwardCommand`; commands still apply
  native CanExecute. StatusBadge has a real parameterless constructor and bindable `Text`/`Kind`.
- ModeSwitch segments expose native radio selection. Masonry rejects non-finite/invalid layout settings before layout.

## Multiple selection (0.5.1)

```xml
<shade:MultiChoice Items="{Binding People}"
                   Selected="{Binding SelectedPeople}"
                   EmptyText="Everyone" />
```

`Items` accepts an `IReadOnlyList<string>`; `Selected` accepts an `IReadOnlySet<string>` and binds two ways.
Options use ordinal string identity and keep their supplied order; duplicate options are removed. Both collections are
copied into snapshots so later mutations of a source collection cannot silently change the control. Replace the property
(or use `Set` for selection); in-place collection notifications are not observed. Bindings may arrive in either order.
While `Items` is empty, selected keys are retained for normal view-model initialization/loading. A non-empty option
snapshot removes unavailable keys. To clear selection explicitly, set `Selected` to an empty set or call `Set` with an
empty sequence. An empty option list stays closed. This preserves pending selection without a separate loading API.
Use stable names; a separate display-label/key model and remote option loading are not provided.

`Label` optionally keeps a fixed caption; `EmptyText = null` uses the localized “All”. More than two selected names use
`Look.Localize` with `"{0} selected"`. Programmatic updates notify bindings without raising `Changed`; user checkbox toggles
notify both. `Set(control.Selected)` is safe. The old constructor and `Set` method remain supported.

Enter/Space or Down opens the picker; Up opens at the last option. Within it, arrows/Home/End and typed prefixes move
focus, Space toggles native checkboxes without closing, Esc restores the trigger, and Tab/Shift+Tab continue through
the application. Clicking elsewhere dismisses without stealing focus. Disabling, hiding, detaching or replacing options
closes and releases the popup. Native expand/collapse and checkbox toggle providers cover automation; platform screen-reader
verification is still required. Give a picker a visible label or an application automation name.

## Native actions and the 0.6 migration

`ActionButton`, `IconButton` and `Capsule` now derive from Avalonia `Button`. Shade supplies their `ControlTheme` through
`Look.Apply`; normal Avalonia styles, `ContentTemplate`, commands, routed clicks, default/cancel buttons and native
button automation apply. The string constructors remain convenient for C# callers. All three can be created in XAML.

```xml
<shade:ActionButton Content="{Binding SaveLabel}" Kind="Primary" Command="{Binding Save}" />
<shade:IconButton Icon="{Binding ExportIcon}" Command="{Binding Export}"
                  AutomationProperties.Name="Export" />
```

| Previous contract | 0.6 replacement |
| --- | --- |
| `Text` / `ActionButton.TextProperty` | Native `Content` / `ContentControl.ContentProperty`. |
| `button.Click += () => Save()` | Routed `button.Click += (_, e) => Save()`, or native `Command`. |
| `LabelAlignment` | Native `HorizontalContentAlignment`. |
| `KeepPointerFocus = true` for editor toolbars | `Focusable = false` where an action should leave keyboard focus in the editor. |
| Layout through the old Border child | Native template/content APIs; do not rely on the old visual tree. |

`Kind`, `Icon`, `IconSize`, `IconColor`, `Live` and `Dimmed` are Avalonia styled properties, so they can bind or be styled.
Changing an icon after construction now works. `Armed` and `IsBusy` notify bindings. Confirmation changes the displayed
caption without replacing bound `Content`. Hold actions still require a pointer hold; keyboard and automation do not
bypass that policy. Explicit `Height` is now honored instead of being forced back to the default. Prefer the default
`Look.ControlHeight` for normal actions; review intentionally compact heights with the chosen font and density.
Recompile consumers: these are intentional API/base-class breaks, including inherited native property/event types.

## Async actions

```csharp
var save = new ActionButton("Save") { AsyncAction = cancellation => SaveAsync(cancellation) };
save.Failed += error => ShowError(error.Message);
```

Use one execution path: `Command`/`Click` or `AsyncAction`. The async path takes precedence. While running, `IsBusy` is
true and the button cannot execute again. `Cancel()` requests cooperative cancellation, also requested on visual detach.
The action must observe its token. Failure sets `LastError` and raises `Failed`; cancellation does not count as failure.
The host supplies error/status presentation and must not throw from failure handlers. `RunAsync` uses the UI thread,
as other control operations do. It is the execution method; `PerformClick` additionally enforces confirmation/hold policy.

## Table integration

`Rows`, `Columns` and `IsReadOnly` are Avalonia properties. Use `ObservableCollection<Row>` / `ObservableCollection<Column>` for structural changes while a table is attached.
Notifications coalesce on the UI dispatcher. Mutate UI collections on the UI thread. The table still needs mutable lists
for its Add/Delete/reorder features. Fixed-size/read-only collection structures hide unsupported add/delete/reorder
actions while retaining cell editing when the rows themselves are editable. Explicit structural methods reject
unsupported mutations. Column keys must be unique and nonempty; each row must be a distinct non-null instance. Direct dictionary or column-property changes require `Refresh()`.

`TrySetValue(row, column, text)` rejects read-only/computed cells and invalid numeric input, then invokes `Validate`.
Return an error string to reject the edit without replacing the stored value. Successful edits raise `CellEdited` with
row, column, old text and new text, and the existing `Changed` event. Observe `LastValidationError` for rejected edits.
`TrySetValue` is the programmatic edit path; arbitrary writes to dictionaries bypass these checks.

Invalid numeric/unit drafts show a danger border and a visible error message. The stored value and calculations stay
at the last accepted input. Enter/Tab wait for a correction; Escape cancels the invalid draft. Clicking outside keeps
the draft visible. Theme/language changes preserve it; an explicit model refresh/replacement may discard drafts.
Successful edits still apply immediately: the application owns save transactions and domain validation.

Columns can override `HeadingSize` and `SubheadingSize` (null inherits the table), and set `Italic` alongside `Bold`.
These affect native headings, cell text, editors and width measurement. Call `Refresh()` after changing column properties.

Cell alignment inherits in this order: `Row.Alignments[columnKey]`, `Column.Alignment`, automatic placement.
Use Avalonia's `TextAlignment.Left`, `Right` or `Center`; an override applies to both the displayed value and editor.
It does not change parsing, sorting, calculations, headings or footers. Unit/suffix labels keep their reserved trailing space.

```csharp
code.Alignment = Avalonia.Media.TextAlignment.Left; // keep numeric and alphanumeric codes together
row.Alignments[code.Key] = Avalonia.Media.TextAlignment.Center; // just this cell
// row.Alignments.Remove(code.Key); // inherit the column again
// code.Alignment = null; // restore automatic alignment (numbers right, text left)
table.Refresh();
```

Large tables realize only the viewport, a small buffer and the focused row. Variable-height rows wrap; unseen row
heights are estimated until measured, so the scroll extent can adjust. Scrolling retains the active editor/caret and
releases other offscreen controls. Place the table in a bounded Avalonia layout (for example a Grid star row), not an
unbounded vertical StackPanel, to get a bounded viewport. Small tables keep their controls to avoid unnecessary churn.

All row data remains in memory. This is UI row virtualization, not remote paging. Fit widths inspect the full loaded
dataset; repeated measurements share a bounded cache. Footer calculations also use all loaded rows; displayed footer values are cached across layout and invalidated by edits,
refreshes and footer changes. Public `Footer`/`FooterValue` calls calculate against current data. Column virtualization
is not implemented. Prefer fixed widths if profiling shows exact Fit scans dominate a unique-text workload. Test your actual wide/text-heavy data before setting performance guarantees.
Run `dotnet run --project tests/Shade.Tests -c Release -- --scale 10000 10` for a 10,000-row/10-column check. Add `--unique --footers` for distinct values with totals and `--fixed` to compare fixed widths. Its exit
status checks that the visual count stays bounded; elapsed time and allocations are observations, not fixed CI thresholds.

## Accessibility and compatibility

Buttons/icon actions expose invoke automation; switches, ticks and state toggles expose toggle automation. Date fields and table cells expose value automation through their validation paths. Calendar days expose named native
invoke targets, summary actions use native buttons, and text tabs expose selection/container providers with roving focus. Native toggle and action controls accept Space/Enter. Give icon-only controls meaningful automation names. Headless tests verify provider behavior;
Windows/macOS/Linux screen-reader checks are still required. Tabs support arrows/Home/End and summaries support directional navigation. Shared focus borders retain at least 3:1
contrast on the page and raised surfaces even for black/white custom accents. Full grid semantics and calendar-day
navigation still need platform accessibility review.

0.5 is a breaking release: `TextSize` values, `Look.ControlHeight`, `FormLayout.RowHeight` and font fields become
runtime properties. Recompile consumers. Replace uses in constant expressions/default arguments with runtime values.
`Build.Text` / `Build.Lines` use zero as the default-size sentinel. `SummaryCards` now derives from `Panel` and places
its drawing surfaces inside native action targets; recompile callers and avoid relying on its old visual-tree shape.
Default geometry and artwork are preserved.

Before release, require the warning-free build, control tests, gallery rendering in both modes, generated docs check,
browser interaction checks, package contents and public API baseline. Keep the theme contrast checks and add regression
tests whenever a parser, edit rule or interaction changes. Never put app-specific services or routes into Shade.

## Documentation checks

The page uses the same bundled Inter fonts as the native library; generation writes fonts and their license under
`docs/fonts`. There is no Google Fonts request. The browser check accepts a file path or an HTTP URL, so it can run against
a local static server when the browser disallows file navigation. It never needs TLS verification disabled.
See [the release plan](RELEASE_PLAN.md) for the remaining platform gates.

## Searchable dropdown (0.6.1)

`SearchChoice` is a native single-selection `ComboBox` with a search field at the top of its popup. Open it to browse
all loaded items; type words in any order to narrow results. Every word must match somewhere in the searchable text.
Matching uses `Look.Culture`, ignores case/accents, and preserves source order: `novak mgr` matches `Mgr. Jan Novák`.
The search field stays visible while the native result list scrolls and virtualizes rows.
Default result labels highlight matching letters with readable accent text and an accent tint: `lá` highlights the final
two letters in `Malá`, and `la` does too. Overlapping words merge. A match in a hidden search field does not highlight
unrelated label characters. Custom `ItemTemplate` layouts own their highlights; use `Build.Mark(textBlock, label, query)`.

```xml
<shade:SearchChoice ItemsSource="{Binding Employees}"
                    DisplayMemberBinding="{Binding DisplayName}"
                    SearchMemberBinding="{Binding SearchText}"
                    SelectedValueBinding="{Binding Id}"
                    SelectedValue="{Binding EmployeeId}"
                    PlaceholderText="Choose employee"
                    SearchPlaceholderText="Title, first or last name" />
```

Each employee is an application-owned object. Its `SearchText` can combine title, first name, surname, department or
other searchable fields; the library contains no employee/database model. `DisplayMemberBinding` supplies the visible
label, while native `SelectedValueBinding`/two-way `SelectedValue` map an ID. Bind native `SelectedItem` instead when
the view model wants the object itself. Duplicate labels remain distinct objects/IDs. Strings work without either
member binding. Compiled bindings inherit their item type from `ItemsSource`. `ItemTemplate` customizes rows; native
`SelectionBoxItemTemplate` customizes the closed selection. With a rich template, still provide a searchable binding. Native `TextSearch.TextBinding` can supply a readable text label
for automation when using templates instead of `DisplayMemberBinding`; use a compact `SelectionBoxItemTemplate` when
rows have multiple lines.

Filtering and arrow navigation do not change the committed selection—even when the selected object is filtered out.
Enter or a primary click commits; Escape cancels and restores the trigger. Tab/Shift+Tab close and continue through
the host. Opening resets the query and shows all options. Empty results show localized `NoResultsText` (default
“No matches”); Enter on an empty list does nothing. `SearchPlaceholderText` defaults to localized “Search”.

`SearchText` binds two ways. Native `SelectionChanged` observes committed/programmatic selection changes;
`SelectionCommitted` is the routed event for explicit activation, including choosing the existing selection again.
Result rows provide native invoke targets. Disabling, hiding or detaching the picker closes its popup.

Use an observable `ItemsSource` for live collection updates. Search text is cached per item while the popup is open;
collection changes and each opening refresh it. After changing search-relevant properties on existing objects in place,
call `RefreshSearch()` to rebuild that cache. Display bindings still observe normal item notifications. This avoids
subscribing to every property of thousands of records on every keystroke. Keep non-null data records and native
single-selection semantics; this variant uses its popup search rather than an editable closed caption.

The control filters loaded data; the app owns database/API calls, loading/error state and source replacement. An app can
observe `SearchText`, but this version does not provide remote paging, request cancellation/debouncing or a multi-select
mode. Existing selection/source replacement follows Avalonia's standard ComboBox behavior; filtering does not replace
that full source. Large lists still cost memory to load/search, while only the viewport's row visuals are realized.


## Sidebar branding and shared accent state (0.6.1)

`Sidebar.ExpandedWidth` is a bindable width in device-independent pixels (default follows startup metrics). The brand
uses Title typography and wraps, the header grows, and a visible mark gets reserved space. Use, for example,
`new Sidebar(name, tagline, entries) { ExpandedWidth = 260 }` for a wider brand. Collapse, expand and rapid reversal
keep that configured width; hidden/custom marks use the same layout. Shared menus keep native focus while preserving
pointer versus keyboard modality, including restoring it after a choice or Escape.

`AccentPicker.Value` follows global accent changes as well as its own picks. Bind `ColorGrid.Value` to a shared colour
source to synchronize several helpers. For a global accent preview:

```csharp
var helper = new ColorGrid(Look.Accent);
helper.Bind(ColorGrid.ValueProperty, new Avalonia.Data.Binding("Color")
{
    Source = Look.B(Look.Accent), Mode = Avalonia.Data.BindingMode.OneWay
});
helper.Picked += (colour, _) => Look.SetAccent(colour);
helper.PickedCurated += i => Look.SetAccent(Look.Accents[i].dark, Look.Accents[i].light);
```

The helper remains usable for independent colours; it does not change the global accent automatically.


### Table pointer interactions (0.6.1)

In `EditableTable.Design` and `TableDesigner`, **hold Ctrl while dragging a heading** to reorder it. A plain header
click/drag selects; no grip overlays its label. Ctrl+Left/Right still reorders by keyboard. Releasing Ctrl or pressing
Escape cancels a pointer reorder; fields dropped from another control into TableDesigner retain their normal drag path.
Header tooltips describe the gesture, and holding Ctrl shows the move cursor.

Resize from the 12-pixel target just inside the heading's right edge; Ctrl is unnecessary. The target remains inside
its own column so the next column cannot cover half of it. Double-click still fits. Pointer focus does not scroll the
heading away from the pressed point; keyboard focus continues to reveal offscreen headings.

A short table keeps its Add rows controls directly below its rendered footer/scrollbar, even inside a tall Grid star
row. Long tables reserve room for those controls and scroll the body. Insertion lines and drop targeting use the actual
grid bounds; unused space below it is excluded. Invalid cell borders replace the accent focus frame across the entire
cell, including its unit or suffix.

### Integrated column editor (0.6.3)

Set `table.ShowColumnEditor = true` to opt into the toolbar and inline ribbon. The default remains false for existing layouts. Edit columns/Done toggles `Design`; Add rows stays below the grid. Column, Text, Width and Table use shared TextTabs. Set `Column.Alignment` to Left, Center or Right; null (Auto) retains the existing numeric/text alignment. `Row.Alignments` continues to take precedence. Native and web controls include units, formulas, typography, bounded widths and paper formats. Paper presets are Screen, A5, A4, A3, Letter, Legal and Custom. `PaperFormat`, `PaperLandscape` and `CustomPaperSize` are validated Avalonia properties with two-way binding defaults; the ribbon uses `SetCurrentValue` so edits do not replace bindings. `PaperSize` is the observable effective size in millimetres (null for Screen), ready for an app’s print/export page setup. Native preview width uses 96 DIPs per inch and is capped by the host; Screen restores normal sizing. Printing itself remains platform/application-owned, as it is outside a table control’s responsibility.

```xml
<shade:EditableTable ShowColumnEditor="True"
                     Design="{Binding EditingColumns}"
                     PaperFormat="{Binding PaperFormat}"
                     PaperLandscape="{Binding Landscape}"
                     CustomPaperSize="{Binding CustomPaperSize}" />
```

Existing heading/subheading/value-size properties retain their binding defaults; use `Mode=TwoWay` when the view model should receive size changes made in the ribbon. Unit definitions commit on Enter or focus loss. Existing row units remain stored, and a draft is committed to its original column when selection changes. Fixed-width drafts outside min/max bounds are rejected; leaving the field snaps to the valid bound. Public column model changes still require `Refresh()`.

In the 0.6.4 patch, ribbon widths, paper dimensions and typography labels use `ShadeOptions.Culture` (`Look.Culture`), with grouping disabled for numeric entry. For example, `cs-CZ` accepts `60,5` as 60.5 DIPs; `en-US` rejects that text. The web ribbon uses `data-culture` on the grid, falling back to the document language. Ordinary cell input retains its separate `Column.InputCulture`/`Parse` contract.

Done validates current ribbon text synchronously, commits valid pending input and keeps Design enabled when an applicable field has an error, selecting its pane and focusing it. Escape inside a ribbon field restores its last applied value and keeps the ribbon open; Escape elsewhere attempts Done. Changing columns discards invalid column drafts and commits valid unit drafts to the original column. Paper drafts belong to the table and persist across column changes; choosing a preset discards custom-dimension errors. Programmatic `Design`/permission changes remain host-controlled. Supplied font sizes outside the usual presets appear as actual choices, without normalizing the model. Formula sources distinguish duplicate titles by subtitle, then by one-based table position if their rich headings still match; storage keys remain internal.

The ribbon does not yet configure `Decimals`, date/currency display rules, allowed paper presets or host validation callbacks. Existing model values and app-owned boundary validation remain supported; see [the capability backlog](BACKLOG.md).

See [the 0.6.3 implementation audit](RELEASE_0_6_3_AUDIT.md) for release validation, its post-release correction and platform limits.
