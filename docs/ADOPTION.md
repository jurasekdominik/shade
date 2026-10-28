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
- `Look.Apply(this, name, markIcon: true)` also gives every window the mark as its icon, in the accent; `mark: (g, box, colour, phase, icon) => ...` draws the app's own mark instead of Shade's two discs, wherever Shade draws one (icons, `Look.Mark`, the sidebar's default, dialogs). Set the app's default font to `avares://Shade/Assets/Inter#Inter` (`.With(new FontManagerOptions { DefaultFamilyName = ... })`).
- Paint with roles, so a new mode or accent repaints everything: `B(Raised)`, `Build.Text("Name", Text2)`. Work a colour out per instance (a blend of your own colour) in the control's `Restyle()` (implement `IRestyle`), which `Look` calls on every switch; paint state (hover, focus, selected, open) with pseudo-class styles instead; Shade's own derived colours (hover, tints) are set once in `Look`.

## Colour resources

`Look.Apply` publishes every colour role as an application resource, `Shade` plus the role's name in `Look`: `ShadeBackground`, `ShadeRaised`, `ShadeFill`, `ShadeSeparator`, `ShadeOutline`, `ShadeText`, `ShadeText2`, `ShadeText3`, `ShadeDisabled`, `ShadeAccent`, `ShadeAccentHi` (accent hover), `ShadeAccentText`, `ShadeOnAccent`, `ShadeAccentTint`, `ShadeFocusBorder`, `ShadeSuccess`, `ShadeWarn`, `ShadeDanger`, `ShadeDangerText`, `ShadeDangerMuted` (danger tint), `ShadeInfo` and the rest of the `Tone` fields. Each is the role's one shared brush, which a mode or accent switch recolours in place, so XAML follows live:

```xml
<Border Background="{DynamicResource ShadeRaised}" BorderBrush="{DynamicResource ShadeSeparator}" BorderThickness="1" />
```

Use them for the colour of a native control or a view of your own; never a hex. Colours worked out from a role (a blend, a tint) stay with the control that needs them.

## Native controls

Shade themes these Avalonia controls in `Look.Apply`, so an app uses them as they are: `CheckBox`, `ToggleSwitch` and `ProgressBar` (see *Toggles and short lists*), `TextBox` (a Shade text field: hover, focus and a binding error show on its frame; Cut, Copy and Paste on right-click, labels through `Look.Translate`; Fluent's `clearButton` and `revealPasswordButton` classes are not drawn), `ComboBox` and `ComboBoxItem` (a `ChoiceButton`'s look: hover, keyboard focus and `:dropdownopen` on the frame, the list attached below it), `ContextMenu`, `MenuFlyout` and `MenuItem` (Shade's menu card: the lit row is `:selected`, `InputGesture` is the shortcut hint, a header of `-` a line, `Classes="danger"` danger text; the top-level `Menu` bar is not themed), `ListBox` and `ListBoxItem` (28 px rows, the pointer and the selected row on Fill; no frame of its own) and `TabControl` and `TabItem` (the look of `TextTabs`, with a page for the selected tab). Their colours are Shade's roles and follow a mode or accent switch. Shade has its own control, not a theme, where it adds behavior or a concept Avalonia lacks (`ActionButton`, `Field` and the date, search and choice fields, tables, `Sidebar`, `Dialog`, ...). A native `Button` or `DatePicker` is not themed: use the Shade control from *Choosing a component* in DESIGN.md, or file an issue. Each release adds to the themed list; the CHANGELOG names what moved.

## Forms and validation

```csharp
var name = new Field { Error = "Enter a name" };
var labelled = Build.Labelled("Name", name);
```

`Error`, `Invalid`, `Pending` and `IsReadOnly` are Avalonia properties. A binding on `Field.Text` takes part in the platform
validation pipeline (`INotifyDataErrorInfo` included); `Error` is an explicit message and wins over binding errors, and
`Invalid` alone shows no message. `Build.Labelled` links the label to the editor and shows its message beneath. Keep required and
business rules in the view model. Date fields bind `Min` and `Max`. `Field` exposes native `PlaceholderText`; `Box` is the escape hatch for native editing options and IME.
`Field` and the date, host:port, search and suggestion fields paint their border through Shade's field theme on pseudo-classes: `:pointerover`, `:focus-within`, `:disabled` and Shade's `:invalid` (`Invalid`, `Error` or a binding error), `:pending`, `:overridden` (an `Inherit` override or `Pinned`) and `:open`; an app style can select them. Focus anywhere inside the field, its undo or clear button too, lights the border. `Field.Watch(TextBox)` is removed: a subclass's own text boxes need no call (the border follows `:focus-within`).
`ListField` has a parameterless constructor, bindable `Hint` and two-way `Text`; user edits raise `Changed`, and setting the same normalized text keeps row editors and focus.
`HostPortField` binds `DefaultPort`, `SearchField` binds `Count`.

`DateField` types in three blocks (`YYYY-MM-DD`): digits fill a block and move on ("4" in the month can only be April), Up/Down change it, Left/Right move between blocks, Backspace empties it, "-", "." or "/" move on, and Ctrl+V takes `2026-10-24` or `24.10.2026`. A day outside `Min`/`Max` marks the field invalid and keeps the value. A click anywhere opens the month (the icon closes it); Alt+Down or F4 opens it from the blocks, arrows move a day or a week, Page Up/Down a month, Enter picks, Esc closes. `DateRangeField` adds one field for a period: a pick in the start sets it, clears the end and asks for it (hovering previews the range); a pick in the end closes the month; a picked end before the start swaps them, a typed one marks the field. The typed blocks take the keyboard focus and speak for the field: a screen reader hears the field's name (`AutomationProperties.Name` or its `Build.Labelled` caption), help text and value when Tab reaches it.

## Translation and culture

`Look.Translate` maps an English key to text and must return the key when missing. For plurals set `Look.Localize` instead; it
gets the English template and arguments, returns the final string, and wins over `Translate`. Set both hooks before creating controls.
To switch language in a live view, update the hooks and `Look.Culture` on the UI thread, then call `Look.RefreshLanguage()`. Built-in labels, tooltips,
calendar and automation names refresh; app titles, user input and custom placeholders stay yours. Table refresh keeps an invalid draft and caret.

`Look.Culture` names calendar parts, sorts table text and formats translation arguments. It does not change stored values,
date input or layout direction: right-to-left layout is not supported. Dates are ISO only: `DateField` shows and types `YYYY-MM-DD` (and takes `24.10.2026` pasted) in every culture; a culture's date format waits for an app that asks.

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
`Armed` and `IsBusy` notify bindings (`IconButton` needs an automation name). `HorizontalContentAlignment` aligns the label, and `Focusable = false` keeps keyboard focus in an editor (toolbars). `Classes="quiet"` makes an `IconButton` a small secondary action inside another control (Text3, Text on hover), as a tag's remove and a search's clear.
`HoldMs` and `ConfirmText` bind; hold actions need a pointer hold and are not bypassed by keyboard or automation; confirmation changes the caption only, leaving bound content intact. A hold consumes the full pointer press, so release cannot activate a new action when the label changes; early release, leaving the button, lost capture, disabling, detachment or window deactivation cancels it. It uses a monotonic clock and does not shrink the hit target (progress is the pressed feedback).
Explicit `Height` is honored; prefer `Look.ControlHeight` and review compact heights at your font and density.

```csharp
var save = new ActionButton("Save") { AsyncAction = cancellation => SaveAsync(cancellation) };
save.Failed += error => ShowError(error.Message);
```

Use one path: `Command`/`Click`, or `AsyncAction` (it wins; `Content` and `Command` bind as usual). While it runs `IsBusy` is true and the button cannot run again. `Cancel()` and visual detach
request cancellation, which the action must observe. Failure sets `LastError` and raises `Failed` (cancellation is not failure; do not throw from handlers).
`RunAsync` runs on the UI thread; `PerformClick` also applies confirmation and hold.

### Dialogs with your own buttons and body

`Ask.Confirm`, `ConfirmDelete` and `Info` answer yes or no. For any other question name the buttons:

```csharp
var pick = await Ask.Choose(window, "Save changes?\n\nYour changes are lost if you don't save.",
    new ChoiceOption("cancel", "Cancel"), new ChoiceOption("discard", "Don't save", Danger: true), new ChoiceOption("save", "Save", Primary: true));
if (pick == "save") Save();
```

- `Ask.Choose` returns the key of the button picked, or null for Esc or the window's close. Buttons sit left to right with the last at the right edge; `Primary` is the main action (Enter picks it), `Danger` is red and only a click picks it. The text may start with a short title line and a blank line, as in `Confirm`.
- A body of your own (a field, a list, an icon beside the text): `Ask.Choose(window, body, options)` with any `Control`, or `new Dialog(title, body, options, width)` and `ShowDialog` (read `Chosen`). The dialog keeps its look, Esc and the owner's centring; validate in your own handler before closing if a field must be filled.
- Web: a `<dialog class="s-dialog">` takes any content and buttons in its `.buttons` row; `.s-field` fills the width.

## Toggles and short lists

`StateToggle` and `FilterChip` derive from `ToggleButton`, keeping Shade's faces and animation. A switch and a check box are Avalonia's own `ToggleSwitch` and `CheckBox`: `Look.Apply` themes them (the pill and its sliding, stretching thumb; the box, check and dash; the keyboard focus ring; all painted by pseudo-classes), `Content` is the label beside them, and they need no Shade type. The thumb is not draggable, a click or Space toggles. `ReducedMotion` is read when a switch is built.

```xml
<ToggleSwitch IsChecked="{Binding Enabled}" />
<shade:StateToggle IsChecked="{Binding DeviceState}" Pending="{Binding Pending}"
                   ShowUndo="True" UndoCommand="{Binding RestoreDeviceState}" />
<shade:FilterChip Content="Errors" Count="{Binding ErrorCount}" IsChecked="{Binding ShowErrors}" />
<shade:TickGrid Items="{Binding People}" Picked="{Binding SelectedPeopleInOrder}" />
```

- State is two-way `IsChecked`; `StateToggle` uses `null` for unknown (OFF/ON otherwise, `UnknownText` binds). `Pending` is also its UI Automation status ("Pending", through `Look.Translate`; an app's own `ItemStatus` wins). `Click` is for user actions;
  `IsCheckedChanged` sees every change. Parent `Click` listeners get child events, so check `e.Source == sender`. A mixed `CheckBox` becomes unchecked on its first activation (Avalonia's own cycle; with `IsThreeState` it goes on, mixed, off).
- `StateToggle.UndoCommand` (+ `UndoCommandParameter`) does not change the state itself. `StateLook.On` is display only.
- `FilterChip` takes `Content`/`ContentTemplate` plus `Count`, `CountColor` and `Dot`.
- `TickGrid` snapshots `Items` and two-way ordered `Picked`: replace collections, in-place changes are not seen. User clicks raise `Changed`; programmatic picks only notify bindings.
- A progress bar is Avalonia's own `ProgressBar`: `Look.Apply` themes it (a 6 px pill, the part never shorter than its height). Bind `Value` (to `Maximum`) as usual; `IsIndeterminate` slides a segment across, and `Classes="countdown"` empties the part in Text 3 toward the next event. A label above and an amount at the right are your own text; `StatusBar.Busy` keeps its small bar. `ReducedMotion` is read when `Look.Apply` builds the theme: the segment then holds still. An indeterminate bar tells UI Automation its status is "Busy" (`AutomationProperties.ItemStatus`, translated through `Look.Translate`; an app's own status wins). Avalonia still reports a range value of 0 with it.
- `Tag` binds `Text`, `Tone`, `Removable`, `RemoveCommand`, `RemoveCommandParameter`. `StatusBadge` binds `Text` and `Kind`. `NavigationButtons` binds `CanGoBack`, `CanGoForward`,
  `Collapsed`, `BackCommand` and `ForwardCommand`. `ModeSwitch` segments are native radios.

## Pickers and menus

```xml
<shade:ChoiceButton Items="{Binding Projects}" Current="{Binding Project}" Extra="Create project" />
<shade:MultiChoice Items="{Binding People}" Selected="{Binding SelectedPeople}" EmptyText="Everyone" />
<shade:SuggestionField Items="{Binding Names}" Text="{Binding Query}" PlaceholderText="Find a name" />
```

**ChoiceButton** (a `ComboBox`): it keeps the native selection (`SelectedItem`, `SelectionChanged`), `IsDropDownOpen`, keyboard and automation, and adds string `Items` and `Current`. `Items` is a distinct ordinal snapshot (it hides `ItemsControl.Items`); `Current` is two-way and may arrive before the items. A current value missing from a new list stays shown;
it never picks another. `Extra` is an action even with no options (`ExtraPicked`; the selection does not move). `Overridden`, `ItemColor` and `ItemIcon` bind (an icon shows in the list too); use `Foreground` for a colour of the whole field. `Opening` runs as the list opens.
Programmatic changes do not raise `Picked`, and picking the item already current does not either. Arrows, Enter and Space open the list. It is a compact string picker, not a templated or remote list: for objects use `SearchChoice` or a plain `ComboBox`.

*Migrating from 0.10:* `ChoiceButton` is a `ComboBox`, not a `Button`: `Click` and `Command` are gone (use `IsDropDownOpen` or `Opening`), `Expanded` is `IsDropDownOpen`, `TextColor` is `Foreground`, and `Restyle()` is gone. The list no longer reads a "Delete…" item as danger or `Menu.Divider` as a line: those are `Menu` features. `MultiChoice` is a templated control (it derives from `TemplatedControl`, not `Border`): `Child` is gone. `Restyle()` is gone from `Field`, `NavItem`, `TextTabs`, `FilterChip`, `SearchChoice` and `MultiChoice`; `Field.Watch` and `Field.HasFocus` are removed.

**MultiChoice**: `Items` (`IReadOnlyList<string>`) and `Selected` (`IReadOnlySet<string>`, two-way) are copied snapshots: replace them, in-place changes are not seen.
Keys are ordinal and unique, in supplied order. With empty `Items` selected keys are kept (loading); a non-empty list drops unknown keys; clear with an empty set or `Set`.
`Label` is a fixed caption, `EmptyText = null` uses localized "All", more than two show `"{0} selected"`. Programmatic updates do not raise `Changed`; user toggles do.
No separate label/key model and no remote loading.

**SuggestionField** keeps the native editor and NameSearch ranking. `Items` is a distinct sorted snapshot; `ApplySelection` binds and can leave the text alone.
Arrows move through the popup rows without leaving the editor; read-only, disabled or hidden fields commit nothing. Up to eight ranked matches show, Escape dismisses, and custom text stays supported. A pick that opens a new draft keeps the existing draft until you apply the template.

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

**Menu items**: `Menu.Show(anchor, entries, picked)` and `Menu.Panel(entries, picked, close, width)` take `MenuEntry` records and call back with the entry, so you match on `Key`, not an index.

- `Text` is required; `Key`, `Icon` (an `Icons.*` geometry), `Shortcut` (a hint only, you bind the keys), `IsEnabled`, `IsChecked` (a check in the icon column), `IsDanger` and `IsDivider` (`MenuEntry.Line`) are optional.
- Text that begins "Remove", "Delete" or "Disconnect" is red and goes below a line; `Menu.Danger(text)` marks any other. It is checked before translation, so a translated "Delete" stays red.
- A disabled entry is dimmed and skipped by arrows, typeahead and Enter. The string overloads stay and build the same entries.
- Not there yet: sub-menus.

```csharp
Menu.Show(button, new[] { new MenuEntry("Duplicate") { Key = "dup", Icon = Icons.Copy, Shortcut = "Ctrl+D" },
                          new MenuEntry("Delete") { Key = "del", IsDanger = true } }, e => Run(e.Key), atPointer: true);
```

On the page, `listMenu` items may be `{ text, icon, shortcut, disabled, checked, danger }` as well as strings.

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
- **Rows and sorting**: a heading click sorts up, then down, then back to the original order (sorting is on by default). `Workbench.Table` takes `selection` (Ctrl and Shift pick several rows), `checkable` (rows start with a tick box that follows the selection), `toggle` (a click picks without Ctrl) and `fill` (a row's tint); a click opens a row that drills into a view, a double-click one that opens an editor.
- **Editing**: one editor moves over the clicked cell with the caret where the click landed (from the keyboard the value is selected). Enter and Shift+Enter go down and up the column, Tab and Shift+Tab to the next editable cell (computed cells are skipped). A value with a unit (`Kind.Unit`) cycles the column's units on a click of the unit (Alt+Down while editing); the number stays as typed, and a row's unit not in the list shows as stored until clicked. `Display` turns a number into what shows (culture, currency) for typed, computed and footer values; the stored text and the editor keep what was typed.
- **Columns**: a general column (`Kind.General`) keeps numbers and codes as typed ("00123", "AB-7"); numbers sit right and count, text sits left and sorts after them. A computed column joins columns with + − × ÷ (× and ÷ first), in decimals so money adds up to the cent (`Exact`, `FooterValue`); supply your own money rules through `Calculate`, keeping the formula for other columns with `FromTerms`. Its cells are read only but focusable. The footer offers None, Sum, Average, Min, Max or Count from a menu. `Sizing` is **Fit** (the default: the whole column, so nothing is cut), **Fill** (`Grow`: shares what is left, wrapping down to the longest word) or **Fixed** (`Width`), each with `MinWidth` and `MaxWidth`; while a cell is edited Fit columns only grow, so nothing jumps. Dragging a heading's edge sets a Fill column's minimum or another column's fixed width; a double-click fits it again.
- **App rules**: `CanResize` and `CanEditFooter` switch the heading edges and the footer menu off whatever `Design` and `IsReadOnly` say; `CurrentColumn` can be set; `CanRemoveColumn` is asked before a removal (false keeps the column and asks nothing). A selection, a sort or a new row moves and repaints rows in place, so the edited cell keeps its caret. Row numbers select (a click, Ctrl adds, Shift takes a range, Esc clears); Add takes 1–100 rows at once.
- **Look**: columns override `HeadingSize` and `SubheadingSize` (null inherits), and set `Italic` with `Bold`. Alignment is `Row.Alignments[key]`, then `Column.Alignment`, then automatic (numbers right):

```csharp
code.Alignment = Avalonia.Media.TextAlignment.Left; // the column
row.Alignments[code.Key] = Avalonia.Media.TextAlignment.Center; // one cell
table.Refresh();
```

- **Size**: only the viewport, a buffer and the focused row are realized; place the table in a bounded layout (a Grid star row), not an unbounded `StackPanel`. All rows stay in memory (no paging), and Fit
  and footers read every loaded row. There is no column virtualization; prefer fixed widths if profiling shows Fit dominating. `dotnet run --project tests/Shade.Tests -c Release -- --scale 10000 10` checks 10,000 rows
  (`--unique --footers`, `--fixed`).
- **Design pointers**: in `Design` the headings become the table designer: Ctrl-drag a heading to reorder (Ctrl+Left/Right by keyboard; Escape or releasing Ctrl cancels, a plain drag only selects; sorting pauses), and a right-click offers Insert column before and after and Remove column (asked first). Resize from the 12 px target inside its right edge, double-click fits. `TableDesigner` binds `Columns`, `Rows`, `Locked` and `EmptyText`; replace data to rebuild previews.

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
- Trim and extend the ribbon with properties: `RibbonTabs` (flags `Column | Text | Width | Table`; one left drops the tab strip), `HiddenRibbonFields` (English labels such as `"Footer"`; a label two fields share hides both), `PaperFormats` (the presets offered, in order; the current one stays listed) and `ColumnFields`, an `ObservableCollection<ColumnField>` of your own text settings (`new ColumnField("Export name", c => map[c], (c, v) => map[c] = v)`) shown at the end of the Column tab. Fields can be added, not removed, once the ribbon exists. Web: `options.ribbonTabs`, `hiddenFields`, `paperFormats`, `columnFields: [{ label, key }]` (the value lives on the column as `key`). Field order stays fixed.
- The Column tab sets `Decimals` (Auto, 0-4: computed values and footers) and `Suffix` ("€", "%": the unit shown after a number); stored text stays as typed. Date or currency rules of your own go through `ColumnFields` and `Display`. The ribbon does not call host validation callbacks.

### Toolbar and Add rows layout

The defaults are the layout above; set a property to change it.

```xml
<shade:EditableTable ShowColumnEditor="True" ToolbarActions="EditColumns, MoveColumn, AddColumn"
                     ToolbarPlacement="Bottom" AddRowsPlacement="Top" />
```

- `ToolbarActions` (flags `EditColumns`, `MoveColumn`, `RemoveColumn`, `AddColumn`, `CalculatedColumn`; default `All`) hides built-in buttons; an emptied group closes up. It hides only the button: set `Design` yourself when you hide Edit columns.
- `ToolbarPlacement` and `AddRowsPlacement` are `Top`, `Bottom` or `Hidden` (defaults `Top`, `Bottom`). The open column editor stays on the table's side of the toolbar; `Hidden` on the toolbar removes its buttons only.
- `ToolbarItems` and `AddRowsItems` take your own controls (between the built-in toolbar groups; at the end of the Add rows bar). To reorder the built-in groups, hide them and add your own.
- The web grid: `options.toolbar` (`edit`, `move`, `remove`, `add`, `computed`), `toolbarPlacement`, `addRowsPlacement`, and children with `data-slot="toolbar"` or `"add"`.

### Row menu

The row menu (right-click, the row's more button, Menu key) shows Duplicate, Insert above and below, your items, then Delete.

```csharp
table.RowActions = EditableTable.RowAction.Duplicate | EditableTable.RowAction.Delete;
table.RowMenuItems.Add(new EditableTable.RowMenuItem(new MenuEntry("Open record") { Icon = Icons.Folder }, rows => Open(rows[0])));
```

- `RowActions` (flags `Duplicate`, `InsertAbove`, `InsertBelow`, `Delete`; default `All`) hides built-in items. With every built-in hidden and no `RowMenuItems` the menu does not open.
- `RowMenuItems` is a list of `RowMenuItem(MenuEntry, Action<IReadOnlyList<Row>>)`; the action gets the selected rows in table order. Set it up before the user opens the menu; changes apply to the next open.
- The footer Calculate menu and the column heading menu are still fixed.
- The web grid: `options.rowActions` (`duplicate`, `insertAbove`, `insertBelow`, `delete`) and `options.rowMenu` (`{ key, text, icon, shortcut, disabled, checked }`); a pick fires a `rowmenu` event on the grid with `{ key, rows }`.

### Cell, heading and footer content

A column takes your own controls in its cells, heading and footer; null (or a template that returns null) draws today's text.

```csharp
column.CellTemplate = new FuncDataTemplate<EditableTable.Row>((row, _) => new StatusBadge(row.Values["status"], "ok"));
column.HeaderTemplate = new FuncDataTemplate<EditableTable.Column>((c, _) => new Tag(c.Title));
column.FooterTemplate = new FuncDataTemplate<EditableTable.Column>((c, _) => new StatusBadge("Count " + table.Footer(c), "ok"));
```

- `CellTemplate` is built from the `Row`, padded like the text, and rebuilt when the row is edited or `Refresh` runs. The cell's text still sets the width (Fit) and the row's height, so give `Display` the text the content needs; editing, typing and the keyboard model are unchanged, and the editor covers the content. A templated cell has no unit button or `Suffix`. Only the realized rows build content.
- `HeaderTemplate` replaces the title and subtitle (sorting, resizing and the heading menu stay); Fit still measures `Title`. `FooterTemplate` replaces the total and its Calculate menu, and is rebuilt when a value changes.
- Web: a column's `cellTemplate`, `headerTemplate` and `footerTemplate` are HTML strings (your own markup); `{name}` is replaced by that value, escaped: any key of the row in a cell, plus `{value}` (the text); `{title}` and `{subtitle}` in a heading; `{footer}`, `{function}` and `{title}` in a footer.

## Summary cards

Cards only show until you make them clickable: handle `Picked`, set `Selectable`, or give a card a `Command`. `SelectedKey` (two-way; a card's `Key`, its title when it has none) frames the selected card, and a click on a card sets it:

```xml
<shade:SummaryCards Items="{Binding Cards}" Selectable="True" SelectedKey="{Binding Filter}" />
```

- An `Item` may carry `ToolTip` (also on a disabled card: say why), `IsEnabled` (false: half strength, no click, no focus), `Command` and `CommandParameter` (run before `Picked`). `Item.Active` still frames a card; use one of the two ways to select.
- A click selects; it does not clear. Set `SelectedKey` to null yourself (a filter that toggles, a Clear button).
- Web: `aria-pressed="true"` frames a card, `data-tip` is its tooltip, `aria-disabled="true"` disables it (not `disabled`: the tooltip stays).
- A card's content is the drawn face (title, value, line, ring or trend); an app that needs another line or a small action files an issue.

## Sidebar and accent

A sidebar entry (`NavItem`) paints its state through its theme: `:pointerover`, `:selected` (the page shown), `:child-selected` (the parent of the page shown), `:disabled`, and the focus ring on `:focus-visible` only, so a click shows none. `TextTabs` marks the active tab `:selected`.

`Sidebar.ExpandedWidth` is bindable (DIPs); the brand wraps and a visible mark gets reserved room, e.g. `new Sidebar(name, tagline, entries) { ExpandedWidth = 260 }`.

The sidebar opens through properties and has a parameterless constructor for XAML:

```xml
<shade:Sidebar Brand="myapp" Tagline="Orders" ShowHistory="False">
  <shade:Sidebar.Footer><TextBlock Text="Signed in as Jan" /></shade:Sidebar.Footer>
  <shade:Sidebar.Entries>
    <shade:NavEntry Label="Tasks" Route="tasks" />
    <shade:NavEntry Label="Admin" IsHeading="True" />
    <shade:NavEntry Label="Users" Route="users" IsEnabled="False" />
  </shade:Sidebar.Entries>
</shade:Sidebar>
```

- `Entries` is an `ObservableCollection<NavEntry>`: add, remove or change entries (also their children) any time and the rows follow. `NavEntry` takes `IsVisible` (a permission hides it and its children), `IsEnabled` (greyed, no click), `Badge` (`new NavBadge("3", Look.Danger)`, hidden while narrow), `ToolTip` (default: the label) and `IsHeading` (a section title; `Label` only). `Icon` takes a glyph from `Icons`; icon and `Children` are plain properties now, the constructor still works.
- `Footer` puts your content above Back and Forward (a user row, settings, a connection state); `ShowHistory="False"` drops Back and Forward. Read `Collapsed` (and `CollapsedChanged`) to show icons only while the bar is narrow; the footer is clipped, not rearranged. `Header` replaces the name and tagline; the mark and the collapse click stay.
- Web: the markup is yours: `disabled` on a `.s-nav`, `<span class="s-tag danger">3</span>` inside it, `<div class="s-heading">Workspace</div>` between entries, `<div class="s-side-foot">` above `.s-history` (`hidden` on `.s-history` drops it; mark the narrow text of your footer `.label`).
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
Buttons expose invoke, switches/check boxes/state toggles toggle, date fields and table cells value; tabs have selection with roving focus. Give icon-only controls and pickers an automation name or visible label.
Focus borders keep 3:1 contrast. Screen-reader checks per platform, full grid semantics and calendar-day navigation still need manual review.

## Limits

- A fixed colour (`new Tone(color)`, or a blend set outside `IRestyle.Restyle`) does not follow a mode or accent switch; roles and `Restyle` do. Metrics (`TextScale`, `Density`) belong before `Look.Apply`; a `Look.Configure` after it rebuilds the themes for controls built afterwards.
- An open popup (a menu, a tooltip) follows a switch in its role colours; its computed ones (a row's highlight) on its next open.
- The headless render checks run on Linux; run the gallery on Windows to check it for real.
