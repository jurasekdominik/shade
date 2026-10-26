# Adopting Shade 0.6 (in progress)

Shade supplies the visual vocabulary for desktop work tools. The application owns its data, storage, permissions,
validation rules, translations and navigation. Configure Shade before creating views; do not store app settings in the library.

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
The gallery can render a 2x text pass with `--docs <scratch-root> --scale 2`. Set metrics/fonts once at startup and rebuild
views when changing them. Application-supplied fixed widths and compact badge/tag geometry still need review at the chosen
scale; arbitrary application layouts are not guaranteed. Mode and accent continue to switch live.
Reduced motion suppresses switch/action transitions, entry effects, sidebar rolling and indeterminate progress animation.
It is an application preference; the library does not discover or persist OS preferences.

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
invoke targets, summary actions use native buttons, and text tabs expose selection/container providers with roving focus. Interactive switches/ticks/state toggles/icon actions accept Space/Enter. Give icon-only controls meaningful automation names. Headless tests verify provider behavior;
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
