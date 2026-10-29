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

## Character grids

`CharacterGridField : TextBox` is one string editor with one focus stop. `Appearance = CharacterGridAppearance.Paper`
is the approved compact, square printed grid (A); `Field` is the roomier Shade field with rounded outer corners (C).
Both keep the complete cell grid. Both are public,
bindable appearances of the same control, with the same editing, binding, keyboard and automation behavior.

```xml
<shade:CharacterGridField Text="{Binding SerialNumber}"
                          Appearance="Paper" CellCount="16" CellSize="16,20"
                          Padding="0" CornerRadius="0" BorderThickness="0.5"
                          Background="{DynamicResource PaperFieldFill}"
                          Foreground="{DynamicResource PaperInk}"
                          CellBorderBrush="{DynamicResource PaperRule}"
                          CellBorderThickness="0.5"
                          FocusBorderBrush="{DynamicResource PaperFocus}"
                          CaretBrush="{DynamicResource PaperInk}"
                          SelectionBrush="{DynamicResource PaperSelection}"
                          SelectionForegroundBrush="{DynamicResource PaperInk}"
                          IsReadOnly="{Binding IsPrintPreview}" />
```

The host supplies the example paper resources. Omitting them uses Shade roles. The native nullable `Text` property binds
two-way by default and preserves the exact value: zeroes, explicit spaces, punctuation and composed characters. User
changes keep the binding; native `TextChanged` also reports external changes. No numeric parsing, normalization, separate
commit value or custom edit event is added. Caret and selection offsets remain native UTF-16 positions; cells and
decoration ranges count graphemes. Native insert, selection, clipboard and undo/redo semantics apply. Tab moves
between complete fields; the host handles routed keys for row navigation, Enter or Escape policy.

`CellCount` is positive grapheme capacity. `CellSize` is the finite, positive width/height of one cell in local DIPs;
`CellGap` is nonnegative. The desired width is `CellCount * CellSize.Width + (CellCount - 1) * CellGap`, plus the inherited
outer `Padding` and per-side `BorderThickness`; height is `CellSize.Height` plus vertical padding/borders. Defaults follow
Shade metrics: Field (the default) uses a 20-DIP pitch and 28-DIP whole height; Paper uses 16×20-DIP cells and square
corners (22-DIP whole height including its borders). Explicit cell dimensions/corners override either preset and stay
fixed during density changes. Switching appearance preserves text, focus and selection; supplying the same `CellSize`
keeps geometry identical across switches. Document zoom transforms drawing and pointer placement together. Native
horizontal scrolling reveals an overflowing value; cell painting follows the same offset. Fractional lines are antialiased
at the document's actual scale instead of rounding each cell to a different pitch.

`CellPadding` insets glyphs inside that pitch; `CharacterAlignment` accepts Left, Center (default) or Right. Native font
family, size, weight/style, caret, selection, opacity, foreground, background and corner properties remain configurable.
A native presenter shapes each grapheme with font fallback, then supplies its fixed advance to the same native text layout
used for hit testing, selection, caret and IME preedit. The native caret follows the aligned glyph's insertion origin within
its cell, rather than sitting on its separator. Font changes do not change cell pitch. Choose cells large enough for the
requested font and padding; glyphs are not shrunk to fit. Password/multiline modes and RTL are not supported.
Rounded outer frames use uniform border thickness; per-side paper borders use square joins.

`CellBorderBrush` and scalar `CellBorderThickness` control the internal rules independently of the outer frame. With zero
gap each divider is drawn once. `FocusBorderBrush` controls the state outline without changing the fill. Public state
selectors are `:pointerover`, `:focus`, `:read-only`, `:disabled`, `:invalid` and `:pending`; invalid wins over pending,
focus and hover. Explicit paper brushes survive those states and live mode/accent changes. The host must supply
contrasting ink, caret and selection when it overrides the surface.

`CellDecorations` is a bindable `IList<CharacterCellDecoration>` with a separate observable default collection per field.
Use an observable collection for live additions/removals. Each decoration has bindable `Start`, `Length`, `Background`,
`Foreground` and `BorderBrush`. Ranges use fixed zero-based grapheme positions, not text identities; later non-null
properties win overlaps. Decorations inherit their owning field's data context/resources; use separate items per field.
Unspecified properties inherit the field's appearance. Selection ink wins over range ink;
cell rules remain above selection, and the field's state outline owns the perimeter. Decorations never change text,
capacity or editability. Collection/item subscriptions detach when the source changes or the field leaves the tree.

```csharp
reading.CellDecorations!.Add(new CharacterCellDecoration
{
    Start = 7, Length = 2, Background = decimalTailBrush
}); // the final two cells of "024581,36"; the comma is ordinary editable text
```

Typing and whole-value paste are rejected when they would increase the value beyond grapheme capacity; a selected range
can be replaced when the result fits. An oversized paste leaves the complete old value and selection intact. Composed
characters count once. Clipboard reads feed the normal native text-input/undo path and are discarded if their target or
selection changed while awaiting the clipboard. Programmatic values and capacity reductions still preserve overflow in
full as an invalid draft; corrective edits may retain or reduce this overflow but cannot grow it. `Text` bindings can
receive an external invalid value: keep business validation and the persisted value in the host. Do not set native `MaxLength` to
`CellCount`: it counts UTF-16 units and can truncate paste. `ValidationMessage` exposes overflow, explicit `Error` or a
native binding error (`Error` wins); `Invalid` alone only marks the border, and `Pending` marks work awaiting application.
Messages stay outside the fixed row: bind `ValidationMessage` where needed or use `Build.Labelled`. Built-in messages
follow `Look.Localize`/`Look.Translate`; language refresh does not change the value, caret or selection. Pasted line breaks
and tabs are rejected atomically; programmatically supplied ones are preserved, shown as symbols and marked invalid.
Overwrite mode is not implemented.

Give each field an `AutomationProperties.Name` or `Build.Labelled` caption. Both appearances expose one Edit/value peer,
read-only state and validation in help/status. Automation writes preserve bindings and reject read-only/disabled fields.
Native Avalonia 12's TextBox peer does **not** expose a text-selection automation pattern; this implementation does not
claim screen-reader caret/selection support. Real Windows IME/DPI and screen-reader checks, that selection pattern and
the release web parity pass remain open under [issue #105](https://github.com/jurasekdominik/shade/issues/105).

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

**ChoiceButton** (a `ComboBox`; `WheelSelects` lets a wheel notch over the focused, closed field pick the next or previous item, off by default): it keeps the native selection (`SelectedItem`, `SelectionChanged`), `IsDropDownOpen`, keyboard and automation, and adds string `Items` and `Current`. `Items` is a distinct ordinal snapshot (it hides `ItemsControl.Items`); `Current` is two-way and may arrive before the items. A current value missing from a new list stays shown;
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
Result content is display-only: the native row button owns clicks over text, highlights and empty space, and commits once.
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
- `ToolbarPlacement` and `AddRowsPlacement` are `Top`, `Bottom` or `Hidden` (defaults `Top`, `Bottom`). With both on `Top`, the toolbar and the open column editor come first, the Add rows bar under them, then the table (the desktop control and the web grid agree). The open column editor stays on the table's side of the toolbar; `Hidden` on the toolbar removes its buttons only.
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

`ColorGrid.IsDocumentPalette = true` offers Palette/Mixer without theme-curated accents or app-background contrast filtering.
The default remains the theme-accent helper. PaperFormBuilder supplies its own used/recent document swatches above this palette.

## Paper forms

Source preview for [#108](https://github.com/jurasekdominik/shade/issues/108), built on the independently usable
[character grid](#character-grids) and the compact native checkbox contract
([#106](https://github.com/jurasekdominik/shade/issues/106)). No new NuGet release is implied.

```csharp
var form = new PaperFormBuilder { Definition = savedLayout, Values = fieldValues };
form.Changed += () => SaveLayout(form.Definition.ToJson()); // layout edits only
// Use PaperFormView with the same Definition/Values for a fill-only surface.
form.Mode = PaperFormMode.Fill;
string editableHtml = form.ExportHtml();
using var output = File.Create("form.pdf");
form.ExportPdf(output); // UI thread; flattened, read-only page images
```

`PaperFormDefinition` is a versioned JSON layout snapshot, with `PaperPage` and `PaperStructure` records. `ToJson` and
`FromJson` validate schema version 1, finite positive geometry, colours, hierarchy, unique IDs and unique nonempty field
keys. Unknown JSON members/versions are rejected. Treat collections as immutable snapshots: replace the definition to
apply host changes. In-place list mutation is not observed. Units are millimetres, converted with 96/25.4 DIPs per mm;
FontSize is in document DIPs. A5/A4/A3, Letter, Legal and Custom reuse `EditableTable.PaperKind`; Screen is not a paper
format. Landscape swaps the dimensions. Margins define the printable area. PageIndex selects one page; export includes
all pages. Geometry outside margins, overlaps, unknown types, field errors and text overflow are reported in Problems;
export refuses these problems. Changes of paper size retain structures and report placement problems rather than crop.

Every built-in input requires a nonempty FieldKey so editing and export address the same stored value; static text and
groups may omit it.

Values live separately in `IList<PaperFormValue>`, keyed by FieldKey. The default is an ObservableCollection. Missing keys
are added as blank values when a page is visited; prepopulate every key if the list is read-only. Observable collection
changes and value-property changes are observed while attached; replacing Values detaches old subscriptions. Plain lists
are snapshots: replace Values after structural changes. Text is a string, never parsed, trimmed or truncated. Checked
holds boolean/indeterminate values and Error holds host validation. Save these three properties and Key through an
app-owned value DTO; do not serialize AvaloniaObject internals. Deleting/undoing layout does not remove values. A duplicate
gets new IDs/keys and copies its input values; originals remain intact. The app decides when to persist or remove orphan
values. Native TextChanged includes model changes; it is not a user-only autosave event.

Definition, Values, Mode, Zoom, PageIndex and StructureTypes are Avalonia properties. Definition, Mode, Zoom, PageIndex,
SelectedId, SelectedIds, ToolKind, InspectorPlacement and SideBySidePages default to two-way bindings. User edits use SetCurrentValue so bindings survive. `Changed` fires
once per valid layout update/undo/redo, not for external Definition updates or input changes. Each completed pointer gesture
is one update. Valid sidebar text changes apply while typing and share one undo step for that focused editing session.
CanUndo/CanRedo and Problems
notify bindings. A new externally supplied Definition clears history. Layout history retains snapshots for the current
editing session, separate from each native editor's text undo stack. Hosts should replace Definition at document boundaries.

Design mode supplies a palette and click-drag creation, snap placement, selection, move/resize, duplicate/delete and an
inspector. The Insert menu chooses an element; the adjacent hint and crosshair tell the user to drag on the page to draw it.
Escape or Insert clears the drawing tool. Drag the bottom-right handle to resize. For character fields, the initial bounds
use the drawn width rounded to a whole number of cells. Drag resizing changes CellCount at fixed cell width and height,
never stretching glyphs or leaving partial cells. Properties expose Cell count, Cell width and Cell height. With whole-grid
alignment Left, overall width is computed; Center/Right keep an allocated Field width so the complete grid can align inside
it independently of glyph alignment (Alignment). Reducing CellCount preserves the allocated width for Center/Right.
`GridAlignment` belongs to PaperStructure; it does not change character alignment inside each cell. Changing count or cell width updates that width immediately. Paper-form borders are drawn inside the cell bounds: width
is CellCount * CellWidth (inside any wider allocated field) and height is the full cell height. The independently used CharacterGridField keeps its documented
outer-border measurement. Choose a
group and draw around existing elements to adopt them, or draw fields inside a group. Child coordinates are relative to
the group; groups can nest. Moving a group carries its children. The first click selects a group; while it is selected,
a normal click selects a child at the pointer. Siblings can then be selected directly. Double-click reaches a nested child;
click a group's empty area or select its Layers row to move the group itself. Moving a child outside a group reparents it; moving it into
another group adopts it. Resizing groups preserves child geometry and cannot shrink below their contents. Layers lists
every page, group and element with nested indentation; selecting a row activates its page. Higher rows paint in front.
Drag layer rows above/below a sibling to reorder, into the middle of a group to nest, or onto a page header to reparent there.
`MoveLayers(ids, targetId, pageIndex, inside, before)` supplies the same action to hosts; Alt+Up/Down reorders the primary
selection for keyboard users. Group/descendant cycles are rejected. `ShowLayers` hides this browser.

Drag empty canvas for marquee selection; Ctrl/Shift-click adds/removes an item. The native layers list supports Ctrl and
Shift ranges. `SelectedIds` is the complete selection and `SelectedId` its primary item; set SelectedIds to a singleton to
replace a multiple selection explicitly. Drag/arrow keys move all selected roots together, including across group bounds;
parents and selected descendants move only once. Undo restores geometry, hierarchy and selection. Copy/paste, duplicate and
delete apply to the complete selection; copying fields from several pages pastes their page-local geometry onto the active
page. `MoveSelection(x, y)` accepts a common millimetre delta. Multiple selection properties show a summary; select one item
to edit its detailed properties.
In the focused design grid, Page Up/Down selects structures,
arrows move by GridPitch, Shift+arrows by ten steps, Ctrl+C/Ctrl+V copy/paste blocks (including groups and values), Ctrl+D
duplicates, Delete removes, Ctrl+Z/Ctrl+Y undo/redo and Escape
cancels an active gesture/tool. The grid is physical guidance; it never prints. Right-click empty paper for insertion/paste/guide creation; right-click an existing element for Copy, Duplicate, Snap to grid and Delete. Page settings appear when nothing is selected;
valid page and structure settings apply instantly. Incomplete/invalid numeric or colour drafts stay in the current input
without replacing the last valid layout. Native text undo and caret/focus remain intact during live updates. Field keys
are generated automatically for inserted/copied inputs, unique across the document and retained values; the sidebar shows
them read-only. Element menus act on the selection; they do not offer insertion. New pages become active immediately;
layout undo/redo also restores the active page.

All pages remain in one scrollable canvas, arranged vertically by default or in two columns with `SideBySidePages`, separated by a small gap and bounded outer padding. PageIndex marks
the active page rather than hiding the others. Fit page, Fit width and Fit two pages (also public methods) reveal the active
page. Zoom is capped at 400%; its lower bound fits one page, or two for a longer document, up to 100%. Remaining pages
are reached by scrolling. Ctrl+wheel zooms around the pointer where scroll bounds permit; Shift+wheel pans horizontally.
Normal wheel scrolling remains vertical. In the two-column layout, Fit two pages fits a horizontal pair; subsequent pairs
scroll vertically. `ShowPageNavigation` controls Previous/Next and the active-page counter in the editor footer. Independently,
`Definition.ShowPageNumbers` prints Page N of M inside the bottom margin (at least 4 mm); it is also a page-properties toggle. The standalone PaperFormView shows its selected page and accepts 1-400% zoom.

Builder sizes always use GridPitch; positions do too unless a structure has SnapToGrid=false: supplied layouts, inspector sizes/positions and drawing gestures are normalized.
Character cell width uses grid steps and overall width remains an exact number of cells; glyphs never stretch. Snap to
fields defaults on: moving/resizing ordinary blocks aligns compatible edges/centres, and moves can repeat or divide gaps.
Candidates must also land on the drawing grid. Accent guides show the winning alignment/gaps. Toggle Snap to fields in
the persistent top grid ribbon or bind `SnapToElements`; GridPitch and SnapToElements are two-way properties. Changing GridPitch normalizes
the document geometry without scaling typography. Native input values and keys are preserved.
Grid visibility, pitch and snapping stay in this ribbon while any element is selected. Width/height/position and other
numeric properties offer larger side-by-side up/down arrows, keyboard Up/Down and wheel input. Geometry increments use the current GridPitch;
cell count and font size use one, padding uses 0.5 mm and border/line width uses 0.1 mm. Free-position X/Y controls use 0.1 mm. Valid off-grid drafts show their snapped
value when focus leaves the input. Ctrl+wheel remains reserved for document zoom.

`InspectorPlacement = Sidebar` is the default. `Ribbon` puts the same property controls above the canvas in wrapping rows;
SidebarLeft and RibbonBottom supply the other edges. Apps may bind the property or use the toolbar choice. `ShowInspector`
controls every placement. InspectorWidth, RibbonFieldWidth and PropertyRibbonHeight size the panels. ToolbarPlacement,
GridSettingsPlacement, LayersPlacement and FooterPlacement use Dock; LayersSize controls the layer panel's width/height.
NumberButtonsPlacement supports Right (default), Left, Top, Bottom and Hidden; NumberButtonSize controls each adjacent
button's size (28 by default). Changing these options preserves the live numeric input, focus, draft and bindings. Undo/Redo use labelled icons.
Checkbox structures keep square bounds: width/height inspector edits and one-axis drag resizing update both sides. This
controls the compact indicator; use separate static text for a printed caption. A line structure (Kind="line") is a native
separator with LineOrientation (Horizontal/Vertical), BorderColor and BorderWidth. It has no input key/value; its allocated
bounds follow the grid, while the stroke is centred inside them. Lines export to HTML and PDF.

Rulers show millimetres at a stable shell size around each page. ShowRulers disables them independently of ShowGrid.
Drag from the top ruler for a horizontal guide or the left ruler for a vertical one. Empty-paper context actions also add
both orientations. Drag an existing guide to move it; right-click it on empty paper to remove it. AddGuide, MoveGuide and
RemoveGuide expose these actions to hosts. PaperPage.Guides stores immutable PaperGuide records (ID, orientation, position)
as editor metadata. Undo/redo includes guide edits; guides/rulers never appear in fill/preview, HTML or PDF. Guides outside
a resized page remain stored but hidden/inactive. ShowGuides controls display, hit testing and guide snapping. Visible guides
align compatible edges/centres independently of SnapToElements. SnapToGrid=false permits precise/free positions, including
off-grid guides; normal sizes and character pitches still stay on the drawing grid. Multiple selection uses a common grid
step if any selected root requires it, preserving relative offsets. Layer dragging shows an accent insertion line at the
actual destination indentation; an inside-group drop also highlights the group.

A date element uses Kind="date", a FieldKey and DateFormat (default yyyy-MM-dd). The format accepts one yyyy, one M/MM and
one d/dd in any order, separated by spaces, dots, slashes or hyphens. It is a native text input, preserves invalid drafts and
validates actual calendar dates before export. HTML keeps the format and the same validation. The gallery includes dd.MM.yyyy.

Font family is a searchable picker of fonts installed on the machine, with Segoe UI (or an installed system fallback) as
the document default. Bundled UI fonts are not added to this list. The HTML export names the chosen system font without
embedding Windows font files; a receiving machine needs that font for matching typography. PDF flattens the native result.
Fill/Text/Border colour buttons show a swatch, open the shared palette and accept keyboard colour entry; Default clears the
explicit override. The helper opens with the resolved hex value and shows colours from the current form (including nested fields and cell
ranges), plus up to twelve recent picks shared by text, fill and border. A completed helper interaction remembers its last
explicit colour; replacing Definition externally clears this editor-session history. Used colours are collected when the
helper opens. Theme-curated accents are absent. Palette/Mixer remain available without filtering against the app background.
Choosing a document swatch or palette colour fills the hex field and
keeps the helper visible. Hex entry and Default share one footer row. Clicking the selector again dismisses it. Valid colours
apply immediately, while invalid drafts keep the last valid colour. Toolbar dropdowns
reserve their longest translated option's width. The context menu has no internal height cap or scrollbar.

Fill uses native text editing and one focus stop per input. Moving/resizing retains existing editors and bindings;
definition order defines tab order. Preview makes text read-only/selectable and disables boolean mutation without fading
its paper ink. Caption/subtitle space is inside the structure's bounds. Plain/captioned/multiline values use TextPadding
(one millimetre by default), configurable in the sidebar. Text colour replaces the ambiguous Ink label; it controls the
field's foreground (including selected text) and checkbox mark, independently of Fill colour. Selection stays on complete
character boxes while the native caret follows the aligned glyph inset. Multiline text wraps. AllowMultilineScrolling defaults false: native typing, Enter and paste that would grow beyond the visible paper
viewport is rejected atomically, with normal selection/undo retained. The HTML textarea enforces the same policy. Enabling
scrolling permits longer editing drafts and shows an automatic vertical scrollbar; export still reports/rejects values that
do not fit. Preview stays fixed. Programmatic overflow and shrinking bounds never truncate existing values; they remain
invalid drafts that can be corrected. Typing/paste cannot grow character overflow; programmatic values and capacity reductions
remain editable invalid drafts, including when CellCount is reduced;
see the standalone grid's insertion, IME and automation limits. Explicit compact sizing/font metrics are document-owned;
test that the chosen font fits the cells. Keep validation text externally in Problems/Error rather than growing rows.

`StructureTypes` accepts `PaperStructureTemplate` entries with a stable Kind, palette Label, a native control factory and
an equivalent HTML factory. Factories receive the structure, keyed value and effective read-only policy; they own native
bindings, custom validation and accessibility. Custom Properties are versioned by the host inside its layout contract.
ExportHtml returns trusted host HTML for these registered factories; field/layout text from built-in structures is encoded.
The same native factory is rendered for PDF. Register matching native/HTML implementations for a custom kind; an unknown
kind blocks export. ToolbarContent adds host actions; ShowToolbar/ShowInspector/ShowGrid allow host composition.

HTML is a standalone editable fill document using the selected machine fonts (without redistributing Windows font files), native text/checkbox
inputs and fixed-grapheme character painting. Its Save HTML action downloads a new file with the current values; it
does not overwrite the original file or contact a service. Selection, clipboard and undo use browser input behavior.
The HTML layout is fixed; it does not embed the native visual designer. The Print action checks overflow first. The host
still owns business validation, file storage and approval workflows. Custom/system fonts must be available on the reader's
machine unless the host supplies an embedded equivalent in its custom template.

PDF export renders the same native page in Preview, without guides or designer chrome. It includes one compressed RGB
image per physical page, no editable fields or scripts, at 144 DPI by default (72–600 supported; 40 million pixels maximum
per page). **Text is rasterized and not searchable/selectable.** The caller owns the destination stream. Rendering runs
on the UI thread in an invisible temporary window; it needs the application's configured rendering backend. Export is
staged before writing so a later invalid page cannot leave a partial document. `RenderPage(pageIndex, dpi)` exposes the
same geometry as a caller-owned disposable bitmap for an app's print pipeline. Hosts should prepare exports before
truncating/replacing an existing file; the gallery demonstrates this.

Run the mixed-field designer with `dotnet run --project gallery -c Release -- --paper-form`. Generate test HTML/PDF and
both-mode previews with `--paper-form-previews out/paper-form`, then run `python gallery/paper-form-check.py` (Python
Playwright 1.56.0/Chromium and PyMuPDF). This covers PDF page geometry/widgets and standalone save/reopen, text/checkbox values, multiline, graphemes, undo and
75/100/150% pointer geometry. The native focused suite is `dotnet run --project tests/Shade.Tests -c Release -- --paper-form`.

Remaining scope: automatic distribution and explicit alignment commands, page reordering/removal, a richer repeated-row palette,
vector/searchable PDF, browser designer chrome and its release web-sync step. Physical Windows DPI/IME and actual
screen-reader testing remain pending; headless checks do not certify those paths. See the
[implementation note](proposals/paper-form-builder.md) for evidence and current boundaries.

## Accessibility
Buttons expose invoke, switches/check boxes/state toggles toggle, date fields and table cells value; tabs have selection with roving focus. Give icon-only controls and pickers an automation name or visible label.
Focus borders keep 3:1 contrast. Screen-reader checks per platform, full grid semantics and calendar-day navigation still need manual review.

## Limits

- A fixed colour (`new Tone(color)`, or a blend set outside `IRestyle.Restyle`) does not follow a mode or accent switch; roles and `Restyle` do. Metrics (`TextScale`, `Density`) belong before `Look.Apply`; a `Look.Configure` after it rebuilds the themes for controls built afterwards.
- An open popup (a menu, a tooltip) follows a switch in its role colours; its computed ones (a row's highlight) on its next open.
- The headless render checks run on Linux; run the gallery on Windows to check it for real.
