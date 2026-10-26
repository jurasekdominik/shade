# Shade component and Avalonia integration audit

What this repo does: Shade supplies a consistent desktop visual system and reusable components on Avalonia 12.1.4 / .NET 10.
This audit assumes ordinary MVVM applications, small option/configuration lists, and bounded desktop tables with up to
10,000 loaded rows. A separate 100,000-row scroll case is a stress observation, not a new support promise.

Reviewed on 26 October 2026 against `5457caf`. This is an audit and a proposed refactoring sequence; production code,
public APIs and package structure were not changed by this review. It supersedes the shorter backlog where priorities differ.

**Recommendation:** build 0.6 around Avalonia's existing behavior and Shade-owned control themes. Preserve Shade's visual
identity and useful specialized controls. Accept straightforward API breaks instead of adding layers of compatibility glue.
Native here means Avalonia controls, property metadata, templates, selection and automation.

## Scope and evidence

- Inventoried all **41 exported, non-nested Control types**, their base classes, constructors and declared properties;
  reviewed all 43 files in `src/Shade/Controls`, shared helpers, the package, gallery integration, binding fixture and CI.
- Found **55 declared public Avalonia-property fields**. **22 control types** have a real runtime parameterless constructor.
  These counts describe the API, not its quality: factory-only dialogs and static reference builders do not all need a XAML constructor.
- The Release build passes with warnings treated as errors; all **399 existing C# assertions** pass. Extra headless audit
  probes reproduced the integration failures below. Passing the current suite does not cover those failures.
- Checked actual package/font sizes and measured scrolling. No new GUI performance, trim-safety, AOT or screen-reader claim
  follows from these headless checks. No visual changes were made, so gallery images were not regenerated for this audit.

Likelihood below describes a concrete usage pattern, not an invented statistical percentage. Small means a local correction;
medium means a component migration with its bindings/tests; large means a new data/template contract across consumers.

## Must fix: routine integration failures

### 1. An open picker can change a disabled form

[ChoiceButton](../src/Shade/Controls/ChoiceButton.cs), [Menu](../src/Shade/Controls/Menu.cs).

- **What this is:** ChoiceButton delegates an open popup to Menu and does not retain its lifecycle. Its open guard checks
  local IsEnabled; selection callbacks do not check the owner's effective enabled state.
- **Problem:** In the audit probe, opening One/Two, disabling the containing StackPanel, then pressing Down/Enter selected
  Two. The popup remained visible. Disabling a form during background work is an ordinary application operation.
- **Fix:** Own and close the popup on disable/hide/detach; guard user commits with effective-enabled state. Reuse the
  lifecycle already exercised by MultiChoice, or use a themed native ComboBox/Flyout where that removes the custom work.
- **If skipped:** a disabled screen can still accept edits through a previously opened popup.
- **Likelihood / cost:** routine in busy forms; small immediate guard/cleanup, medium native picker migration.

### 2. Selection depends on the order in which bindings arrive

[MultiChoice](../src/Shade/Controls/MultiChoice.cs).

- **What this is:** Selected immediately intersects requested keys with the currently loaded Items. The adoption guide
  currently documents setting options first.
- **Problem:** `new MultiChoice { Selected = new HashSet<string> { "Ben" }, Items = new[] { "Anna", "Ben" } }` ends empty.
  The native ComboBox retained Ben when SelectedItem was supplied before ItemsSource in the equivalent probe.
- **Fix:** support pending bound selection using native selection machinery where practical. Define what happens during
  initial loading separately from explicit item removal; do not build a remote-loading framework to solve this.
- **If skipped:** normal view-model initialization or asynchronous option loading can discard a previously chosen value.
- **Likelihood / cost:** routine MVVM timing; medium. This is a reproduced limitation of the documented contract, not an
  allegation that the existing options-first contract behaves differently from its documentation.

### 3. The common form helper omits the validation/label contract

[FormLayout.Field](../src/Shade/Controls/FormLayout.cs), [Build.Labelled](../src/Shade/Controls/Build.cs).

- **What this is:** two helpers construct a label plus input. Build.Labelled associates the accessible label and displays
  validation; FormLayout.Field constructs separate text and ignores both features.
- **Problem:** `FormLayout.Field("Name", new Field { Error = "A required value" })` produced neither an error TextBlock nor
  a LabeledBy association in the audit probe.
- **Fix:** compose the existing labelled-field implementation inside the layout helper. In the native migration, use a
  single reusable labelled-field presentation for native editors, composites and date fields.
- **If skipped:** the same input has different validation and accessible naming depending on the layout helper used.
- **Likelihood / cost:** routine form construction; small. No new validation framework is needed.

### 4. Highlighting table text clears selection

[Workbench.Table](../src/Shade/Controls/Workbench.cs).

- **What this is:** changing Mark resets ListBox.ItemsSource to null and then reinstalls the original rows to rebuild text.
- **Problem:** select Alpha, call `Workbench.SetMark(table, "a")`: the native ListBox's SelectedIndex becomes -1. Search
  highlighting should not change which row is selected.
- **Fix:** update the text presentation through a binding/template property rather than replacing ItemsSource. Preserve
  the existing native ListBox selection and virtualization.
- **If skipped:** typing a search can clear selection and emit application selection callbacks for a presentation change.
- **Likelihood / cost:** routine search interaction; small to medium, depending on the row-template cleanup.

### 5. Clearing search raises the edit event twice

[SearchField.Clear](../src/Shade/Controls/SearchField.cs), [Field](../src/Shade/Controls/Field.cs).

- **What this is:** Clear writes Box.Text and explicitly raises Edited; Field later raises Edited again for TextChanged.
- **Problem:** typing abc, subscribing to Edited, then pressing Escape produced two notifications for one clear operation.
- **Fix:** use one notification path. Keep normal native text notifications for all value changes and an explicit submit
  command/event where an action is needed; avoid a second manual edit signal.
- **If skipped:** a search/filter request can run twice. An app's debounce can hide the defect but does not fix the contract.
- **Likelihood / cost:** routine interaction; small.

### 6. Structural changes can leave a bound selection stale

[TableDesigner.Columns](../src/Shade/Controls/TableDesigner.cs).

- **What this is:** replacing columns clamps the private selected field to -1 without updating SelectedProperty.
- **Problem:** starting with two columns and Selected = 1, replacing them with one column yielded Selected = -1 and zero
  SelectedProperty notifications in the audit probe.
- **Fix:** route the reset through the public property/SetAndRaise and add bindable Rows, Columns and Locked as this
  component is migrated. Preserve the custom drag preview rather than replacing the whole designer.
- **If skipped:** a view model and the designer disagree about the selected column after ordinary structural edits.
- **Likelihood / cost:** routine designer usage; small notification fix, medium fuller binding work.

## Should fix: refactors with clear consumer benefit

### 7. Standard actions and toggles rebuild native behavior

[ActionButton](../src/Shade/Controls/ActionButton.cs), [IconButton](../src/Shade/Controls/IconButton.cs),
[Capsule](../src/Shade/Controls/Capsule.cs), [Tick](../src/Shade/Controls/Tick.cs),
[Switch](../src/Shade/Controls/Switch.cs), [StateToggle](../src/Shade/Controls/StateToggle.cs).

- **What this is:** these controls implement combinations of pointer capture, key handling, commands and automation on
  Border or Control, while Avalonia Button/ToggleButton/CheckBox/ToggleSwitch already provide that behavior.
- **Problem:** native capabilities such as default/cancel buttons and common command/content bindings require more Shade
  APIs. ActionButton.Kind/Icon are ordinary properties; setting Icon on a parameterless ActionButton silently does nothing
  because its icon child is created only by particular constructors. Switch/StateToggle setters raise Toggled while Tick's
  setter does not, and held-key behavior differs across the family. Capsule is pointer-only.
- **Fix:** move the action family onto Button with Shade themes, and toggles onto appropriate native toggle bases. Keep
  confirmation, hold progress and cancellation as small Shade extensions; retain drawn faces where they express Shade's
  design. Use native Content, Command, CommandParameter and IsChecked rather than creating new aliases for each component.
- **If skipped:** every new interaction/automation feature needs several custom implementations and consumer event adapters.
- **Likelihood / cost:** routine app integration; medium. A native base is a maintenance/compatibility improvement, not a
  performance guarantee. Prototype the hold and switch animation before deleting their existing implementations.

### 8. Constructor-local styling prevents ordinary theme overrides

[Field](../src/Shade/Controls/Field.cs), [Build](../src/Shade/Controls/Build.cs), [Look.Apply](../src/Shade/Controls/Look.cs).

- **What this is:** appearance and visual trees are assigned as local values in constructors and rebuilt by Restyle.
  Look.Apply also installs Fluent, app-wide TextBox styles and a global ScrollBar theme.
- **Problem:** a style setter requesting Field.Padding = 40 still yielded 8 in the probe. Many visual options lack Avalonia
  properties. Embedding Shade into an application with its own theme means accepting global changes and dependence on
  Fluent template part names, rather than just including Shade's components.
- **Fix:** put visual defaults in Shade ControlThemes with TemplateBindings and normal pseudo-classes. Keep state/command
  logic in C#. Separate installing Shade resources from installing an application's base theme. Keep the present global
  look for single-brand apps; do not invent a per-window brand service without an application that needs it.
- **If skipped:** projects fight local values or reach into child controls to customize otherwise ordinary controls.
- **Likelihood / cost:** routine reusable-library theming; medium to large, performed one control family at a time.

**Version-specific caution:** do not mechanically replace every DirectProperty. In Avalonia 12.1.4 a constant style
setter on Progress.Value worked in the audit probe. The demonstrated problem is constructor-local defaults and missing
properties. Keep working direct/read-only properties; choose StyledProperty for new theme-facing options where appropriate.

### 9. Data and selection contracts are incomplete across composites

[PairTable](../src/Shade/Controls/PairTable.cs), [ListField](../src/Shade/Controls/ListField.cs),
[EditableTable](../src/Shade/Controls/EditableTable.cs), [TextTabs](../src/Shade/Controls/TextTabs.cs),
[Sidebar](../src/Shade/Controls/Sidebar.cs).

- **What this is:** several controls accept all data through constructors or methods. Others expose root collections but
  not row-property notifications, writable selected items, templates or commands.
- **Problem:** changing sidebar routes, pair values, tab labels or a table's current/selected rows requires glue code.
  EditableTable advertises editable Add controls even with an array-backed Rows collection: AddRows then throws
  NotSupportedException in the probe. Its dictionaries and column settings need Refresh rather than normal model updates.
- **Fix:** use ItemsSource, native selection contracts and ItemTemplates for data-driven lists. PairTable needs editable
  row objects, not a dictionary-only binding that drops blank/duplicate drafts. ListField should gain a value collection
  with its comma-separated string kept only as a convenience adapter. Respect source mutation capability when exposing Add,
  Remove or Reorder; do not require a new data-service framework.
- **If skipped:** integration complexity grows in every consumer, and common collection choices cause runtime failures.
- **Likelihood / cost:** routine MVVM/data binding; medium for small composites, large for a generic spreadsheet row model.
  Do not convert static reference-card builders into elaborate data models just for API symmetry.

### 10. Pointer-only sub-actions and missing native semantics remain

[Tag](../src/Shade/Controls/Tag.cs), [SearchField](../src/Shade/Controls/SearchField.cs),
[Field.UndoIcon](../src/Shade/Controls/Field.cs), [ModeSwitch](../src/Shade/Controls/ModeSwitch.cs),
[StatusBar](../src/Shade/Controls/Sidebar.cs), [Progress](../src/Shade/Controls/Progress.cs), [ColorGrid](../src/Shade/Controls/ColorGrid.cs).

- **What this is:** several small actions are Borders/StackPanels with pointer handlers. Mode segments have manual keys
  without native selection peers, and the drawn progress bar has no range-value provider.
- **Problem:** a tag remove cross accepts any pointer release; search clear and undo are not independent keyboard/automation
  actions. Keyboard-only users cannot operate the color palette. These are ordinary input modes, not obscure edge cases.
- **Fix:** use themed Button targets for actions, native selection/radio behavior for segments, and ProgressBar for progress
  semantics where its template can reproduce the design. Keep the HSV drawing custom; add native swatch/tab targets and
  keyboard manipulation instead of replacing it with a visually unrelated color picker.
- **If skipped:** automation coverage depends on the particular control chosen, despite a consistent visual language.
- **Likelihood / cost:** routine for keyboard/assistive input; small action replacements, medium segmented/progress/color work.

### 11. Fit width chooses character count instead of rendered width

[Workbench.FitWidth](../src/Shade/Controls/Workbench.cs).

- **What this is:** the helper measures only the string with the most characters.
- **Problem:** for `iiiiiiiiiiii` and `WWWW`, it returned 62 while WWWW needed about 76.2 including the same padding in the
  headless font probe. Ordinary proportional-font names therefore get cut despite the helper's contract.
- **Fix:** measure candidate widths and take the maximum. Add a cache only if a real large input needs one; the existing
  EditableTable already has its own measurement cache and does not use this approximation.
- **If skipped:** short, wide names can be clipped while longer, narrow names determine the column.
- **Likelihood / cost:** routine proportional text; small. Do not build a general text-width service for this fix.

## Nice to have: measured, conditional resource work

### 12. Package assets offer more selective savings than component registration

[Shade.csproj](../src/Shade/Shade.csproj), [Look.Configure](../src/Shade/Controls/Look.cs), [Icons](../src/Shade/Controls/Icons.cs).

- **What this is:** one assembly holds all controls and six embedded fonts; it depends on Avalonia.Themes.Fluent.
- **Problem:** supplying custom fonts through ShadeOptions changes which fonts are used, but still ships the bundled files.
  The fonts total about 2.45 MiB, against a 2.76 MiB Shade DLL. Most component code is comparatively small.
- **Fix:** if a real consuming app needs smaller distribution, extract one optional font-assets pack and preserve the normal
  Shade package as the complete, styled default. Make Fluent optional only after Shade owns the required native templates.
  Do not split every component into a package or add a runtime component loader.
- **If skipped:** apps using their own fonts carry an extra few MiB of assets; this is a deployment cost, not proof of an
  equivalent live-memory or startup penalty.
- **Likelihood / cost:** universal download payload, situational practical impact; medium packaging/migration work.

### 13. Optimize known hot work without a speculative engine

[EditableTable.RealizeViewport](../src/Shade/Controls/EditableTable.cs), [ColorGrid.PaintMixer](../src/Shade/Controls/ColorGrid.cs),
[Look.Icon](../src/Shade/Controls/Look.cs).

- **What this is:** table scrolling scans loaded-row metadata even with few realized rows; the mixer recomputes its contrast
  boundary during drawing; all 33 icon geometries initialize together; WindowIcon caches have no eviction.
- **Problem:** cost grows with loaded data or repeated draw/unique-icon requests. The scroll probe still performed well
  at the supported 10,000-row case, so this evidence does not justify replacing the whole spreadsheet.
- **Fix:** when profiling warrants it, track realized rows directly and dirty offsets only when row heights/order change;
  cache the mixer's boundary by hue/theme/size; use lazy icon properties; bound the WindowIcon cache. Leave the current
  small catalog alone if a first-use profile shows negligible cost.
- **If skipped:** some avoidable allocation/scanning remains; long-lived icon use can retain unique entries. No measured
  shipping failure at 10,000 rows was demonstrated here.
- **Likelihood / cost:** scrolling is routine; a consequential stall/cache growth is workload-dependent. Small to medium
  focused improvements; a replacement virtualization/data-loading system would be a large and unjustified first response.

## Complete component decisions

“Theme/native” means reusing Avalonia behavior while preserving Shade's artwork. “Keep composite/custom” means the current
shape has a purpose; improve properties or native child targets without forcing an unrelated stock control onto it.

| Component | Binding/API gap | Recommended next direction |
| --- | --- | --- |
| AccentPicker | Value binds; Label and swatch state do not; popup lifecycle differs from other pickers. | Keep composite/global accent behavior; bind Label, use native swatch actions and shared owned-popup rules. |
| ActionButton | Text/Command/confirmation bind; Kind/Icon/Live/Dimmed do not. Icon setters depend on constructor path. | Button + Shade theme; normal Content/Command, bindable extensions for hold/confirm/busy and visuals. |
| Ask | Factory-created dialog; strings and callback helpers are adequate for its role. | Keep native Window/dialog behavior; native default/cancel commands and focus rules, not a new dialog service. |
| Capsule | Constructor-only content, Click only, pointer-only behavior. | A Button variant/theme; avoid maintaining a second button engine. |
| ChoiceButton | Current binds; options, item appearance and popup ownership are method-based. | Prototype themed ComboBox with ItemsSource/SelectedItem/ItemTemplate; preserve extra-action and compact-field behavior only where needed. |
| ColorGrid | Constructor-only starting value; events only; no keyboard selection model. | Keep HSV renderer; add bindable selected color and native tab/swatch targets. Do not replace merely for a native label. |
| CompactTable | Constructor-only rows/columns/selection; intentionally creates every row. | Keep simple for small static summaries. Use ListBox/native row selection for dynamic/selectable data; do not add virtualization to a six-row settings table. |
| DateField | Value/limits/Text bind; FirstDay/Weekends are plain; unused Field.Box is still allocated. | Keep ISO segmented editing until a native prototype matches it. Separate date chrome from the text-editing base; evaluate themed Calendar for the popup. |
| DateRangeField | Start/End/limits bind; same chrome/option gaps as DateField. | Keep range workflow and validation. Reuse native popup/calendar parts where they preserve range behavior. No forced replacement by two unrelated date pickers. |
| Dialog | Native Window base already correct; protected construction is intentional. | Keep; reduce helper dependence on ActionButton-specific behavior as native buttons arrive. |
| EditableTable | Root rows/columns/options bind; CurrentColumn/Selected and row/column property changes are incomplete. | Keep virtualized editor/calculation features. Improve source/selection contracts and mutable-source checks; defer any DataGrid replacement to a successful feature/performance prototype. |
| Field | Text/validation/read-only bind; Placeholder, editing options, inheritance/undo/pinned state require child access or plain properties. | A themed TextBox for ordinary text, plus shared field presentation for composites. Reuse native input/validation/IME, not forwarding every TextBox property through a Border. |
| FilterChip | Active binds; label/count/dot/command do not. | ToggleButton for an actual filter. Reference examples that are actions can use a Button with the same visual theme. |
| HostPortField | Inherits Text binding; no parameterless constructor; DefaultPort/Host/Port are not a complete bindable surface. | Keep two native editors as a composite; bind Host/Port/DefaultPort and retain formatted text as an adapter. Keep protocol parsing separate from visual chrome. |
| IconButton | Constructor-only path, no Command, plain Dimmed. | Icon-only Button theme with bindable icon and accessible name; share action behavior. |
| InlineCode | Native SelectableTextBlock is already appropriate; text is constructor-only/read-only outside. | Keep composition. Add Text/CopyCommand if runtime-bound code is required; no custom text engine. |
| ListField | Two-way string and Hint bind; comma-delimited text is the only aggregate value. | Native ItemsControl + native editors; value collection for structured data, existing string parser as convenience. Preserve drafts and unchanged-row focus. |
| LogText | Already extends SelectableTextBlock; Add/Clear and 3,000-line cap suit streaming use. | Keep as a positive native-extension example. Add batching only if real append rates need it; a ListBox would change cross-line text selection. |
| Masonry | Its four layout options already use Avalonia properties. | Keep custom Panel. Add simple finite/range validation for invalid settings; no new layout package needed. |
| ModeSwitch | Value binds; hand-built segments have no native selection pattern. | Native single-selection/radio semantics with Shade segment styling; retain System-mode following. |
| MultiChoice | Items/Selected/captions bind; snapshots and native checkbox targets are good; selection-before-items is lossy. | Keep the useful hybrid, fix deferred selection, and use native list/selection behavior where it removes custom keyboard bookkeeping. There is no requirement to find a stock multi-select ComboBox. |
| NavigationButtons | Native-shaped actions compose correctly; availability and actions are method/event-only. | Bind CanGoBack/CanGoForward and commands; keep this small composite. |
| NavItem | Already inherits Button; Active/ParentActive/ShowText are plain and label/icon are constructor-only. | Keep native Button base; add bindable state/content and a reusable navigation ControlTheme. |
| PairTable | No row collection binding/default constructor; dictionaries cannot represent duplicate/blank drafts. | Native ItemsControl/ListBox with editable pair-row view models; bind validation and source mutation, keep export helpers. No generic form schema engine. |
| Progress | All four public values bind; drawn control lacks native range semantics. | Prototype a themed ProgressBar plus caption/detail; retain normalized 0..1/countdown extensions only where they improve the API. Keep custom drawing if the simpler template cannot match it cheaply. |
| ReferenceCard | Static fluent builder and native child controls; dynamic data binding not its current purpose. | Keep the builder. Use native action themes for help/examples; add a bindable Title only for an actual live-content need. |
| SearchField | Inherits Text; Count is plain; clear is a pointer Border and emits duplicate Edited. | Native TextBox-based search theme/composite with a real clear Button and bindable result count. |
| Sidebar | Constructor-only entries/name/route; Collapsed is plain; changes rebuild children. | Keep layout/brand animation. Native ItemsControl/ListBox or hierarchical item presentation for entries; SelectedRoute/Collapsed and commands bind. No replacement of app navigation services. |
| StateLook | Display-only On is a plain property. | Keep this drawn face; make On bindable and reuse it inside native toggle presentation if useful. |
| StateToggle | On/Unknown/Pending bind; UnknownText/ShowUndo/Undo do not; manual activation differs from other toggles. | Native toggle semantics with the existing state face and a separate native undo Button. Normalize state/action notifications. |
| StatusBadge | Optional C# constructor arguments are not a real parameterless constructor; status/text are Set-only. | Small ContentControl or existing composition with bindable text/status; retain dot-and-word design. |
| StatusBar | Show/Busy/Count methods and pointer events are the only data/action API. | Keep composite; bind message/status/work/progress/count and use native action targets/commands. |
| SuggestionField | Inherits Text; options are fixed in constructor; ApplySelection plain; popup is recreated for arrow navigation. | Evaluate themed AutoCompleteBox with NameSearch matching/ranking; retain custom suggestion workflow if native completion changes document-pick behavior. Either way bind options and update highlight without recreating the popup. |
| SummaryCards | Items/Detailed bind; item replacement rebuilds all cards; actions are event-only. | Keep drawn charts and native Button targets. Add command binding; use item templates/notification-driven values if frequent live updates require it. No diff engine for four static cards. |
| Switch | On binds but programmatic changes fire Toggled; custom pointer/key/automation behavior. | Themed ToggleSwitch or ToggleButton with the current animated face, whichever preserves the design with less code. |
| TableDesigner | Only Selected binds; Rows/Columns/Locked/EmptyText are plain; shrinking columns skips selection notification. | Keep specialized column-drag preview; bind its data/options and native header action/focus semantics. |
| Tag | Plain Text, constructor-only tone/removable state, pointer-only remove. | A small themed content control with native remove Button/Command; scale compact geometry. |
| TextTabs | Active binds; labels are a constructor array and SetName; selection/keyboard peers are hand-built. | Theme a native selection control/tab strip. Bind ItemsSource and selection; keep content lifetime in the host so changing tabs does not discard drafts. |
| Tick | State binds and custom face is lightweight; native checkbox behavior/peer is replicated. | CheckBox + Shade template or existing drawn face; standard IsChecked and native automation. |
| TickGrid | Constructor-only options/selection; label activation differs from checkbox behavior. | Native ItemsControl + CheckBox/ToggleSwitch rows, bound selected values, whole-row label association. Preserve selected-order behavior where applications use it. |
| TrayMenu | Factory-only native Window is deliberate for a branded tray surface; uses generic Menu rows. | Keep if native OS menus cannot preserve the required appearance. Improve shared menu semantics; add a native system-menu alternative only for a real platform requirement. |

### Shared helpers and internal infrastructure

| Area | Decision |
| --- | --- |
| Look, Tone, TextSize, ShadeOptions | Keep tokens, shared brushes, startup metrics and reduced-motion policy. Move control defaults into themes gradually; avoid an all-at-once global/scoped theme rewrite. |
| Build, FormLayout, Shell | Keep concise composition helpers. Unify label/error presentation and avoid silently overriding a supplied control's bindings/layout. Disclosure already builds its content lazily; preserve that useful behavior. |
| Workbench | Keep native ListBox/TextBox/file-picker use. Improve the data-table template/selection contract; fix highlighting and FitWidth before a larger table API migration. |
| Menu | Prefer native menu/flyout actions and item-container themes. Keep context-action semantics distinct from data selection; do not stretch one string/action menu helper into a second selection framework. |
| NameSearch | Keep deterministic matching/ranking as a pure utility, reusable with a native AutoCompleteBox. No new search service or dependency. |
| Icons | Keep the current geometry catalog and licensing. Lazy properties are a possible small first-use optimization, not a reason for 33 NuGet packages. |
| ControlPeers | Remove peers as their controls move to native bases; keep peers for specialized date/grid/drawn controls. Test behavior; do not replace working peers solely to change a class name. |
| DateBlocks, Calendar, MonthGrid | Keep segmented/range behavior while evaluating native month presentation. Standardize ownership/cleanup and input validation; avoid duplicating a second date implementation during migration. |
| EditableTable Row/Column/Term | The dictionaries/callbacks are useful adapters, not a complete arbitrary project data-binding model. Introduce a clearer source/column binding contract only with representative consumer examples. Keep decimal calculation tests. |
| NavEntry, SummaryCards.Item, TableDesigner.Column | Static immutable descriptors are reasonable; live data requires replace-notify or standard property notifications. Use stable identity where rename/translation is a normal operation. |
| Mark, FieldError and small drawing helpers | Keep purpose-specific helpers. FieldError may need to target the new native field presentation after the Field migration; no replacement exception hierarchy is needed. |

## Resource loading: what would actually help

Measured local build/package sizes, rounded:

| Item | Size | Interpretation |
| --- | ---: | --- |
| Shade.0.5.1.nupkg | 1.29 MiB | Compressed distribution, not live memory. |
| Shade.dll | 2.76 MiB | All current control code and embedded resources. |
| Six original TTF assets | 2.45 MiB | Four Inter weights plus two Cascadia Code weights. Compare with total DLL size; this is not a runtime font-memory measurement. |
| Avalonia.Themes.Fluent.dll | 0.70 MiB | A transitive managed theme dependency; Avalonia/renderer/native dependencies are additional. |

Unused controls are not all constructed when a project references Shade. Runtime method compilation and control creation
already avoid much unused work; asset packaging, type initialization and installing themes are separate costs. Embedded
fonts are resolved when used, and an assembly's file size does not mean all of its bytes are resident/decoded immediately.
The gallery/docs/fonts are not shipped wholesale in the runtime DLL; package inspection confirmed the actual entries.

**Practical sequence:**

1. Keep one normal package and lazy construction of expensive views/popups; do not add `LoadComponents(Button, Table)`.
2. Let hosts compose Shade resources without automatically installing a second Fluent theme or globally restyling unrelated
   text boxes/scrollbars. This improves integration even if it does not produce a measurable memory saving.
3. If actual deployment requirements justify it, separate fonts once. Preserve the standard Shade bundle and its typography;
   allow a smaller entry point for apps supplying their own fonts. NuGet feature flags on an already-built DLL cannot
   selectively remove its embedded fonts or methods.
4. Consider a tables-specific assembly only after core/tables release cadence or measured use justifies that packaging cost.
   The current entire managed control library is too small to justify a package per component.
5. Trimming/AOT are application publish workflows. Add a representative compiled-XAML consumer publish test before declaring
   Shade compatible; do not flip an IsTrimmable/IsAotCompatible switch and infer safety. See [.NET trimming guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained).

## Performance observations and proportional fixes

A headless probe used two fixed-width text columns, a 700 × 500 window, ten warm-up scroll steps, then 100 one-row steps.
Measurements include dispatcher/layout work without real GPU rendering. They are same-machine observations, not frame-rate
or platform guarantees. Visual count stayed **473** in every case.

| Loaded rows | Median step | 95th percentile step | Allocations across 100 steps |
| ---: | ---: | ---: | ---: |
| 1,000 | ~1.4 ms | ~1.8 ms | ~20.7 MiB |
| 10,000 | ~1.6 ms | ~2.7 ms | ~20.7 MiB |
| 100,000 (stress) | ~4.3 ms | ~6.9 ms | ~20.8 MiB |

Metadata scans explain the growth, but the supported case does not demonstrate a crisis. Preserve current row realization
and the single native editor. Exact Fit with mostly unique values still has the much larger startup shaping cost recorded
in AUDIT.md; fixed widths remain a useful explicit option. Benchmark new native templates against the same dataset, fonts,
widths, realized controls and edit behavior before calling a migration faster.

| Situation | Likelihood in the stated scope | Proportional response |
| --- | --- | --- |
| Binding values arrive before options; forms disable while a popup is open | Routine MVVM/background-work timing | Fix as a core contract; use native selection/lifetime behavior where it fits. |
| Searching/highlighting/clearing or rebuilding a designer changes selection/events | Routine interaction | Local fixes and targeted regression assertions. |
| Native templates add unwanted minimum heights or spacing | Routine when adopting native controls; already encountered in MultiChoice | Set Shade template defaults explicitly and verify the visual/interaction contract. Do not abandon native behavior over a default template. |
| Hundreds of rows in PairTable/CompactTable | Outside their normal small-list role | Document small-list scope; use the appropriate native virtualized list/table when a real app reaches it. |
| 100,000 loaded rows, hundreds of columns or remote paging | Beyond current measured support goals | Profile an actual consuming workflow; no paging/column virtualization framework as a speculative precaution. |
| DateTime.MinValue/MaxValue navigation | Rare in actual calendars | Keep the existing cheap boundary clamps; no extra date-state engine. |
| Arbitrarily malformed protocol text or unsupported locale glyph input | Depends on the consuming app; many are outside the documented input formats | Validate at the small parsing boundary; do not turn HostPortField into a networking stack or build a new text-input engine. |
| Continuous unique window-icon creation | Situational; picker changes and public Icon calls can create entries | Small bounded cache when touched; no cache service or eviction framework. |
| Every possible font, density, platform and language combination | Unbounded as a test matrix | Cover selected supported fonts, default/2x metrics and target platforms; handle demonstrated failures, not every hypothetical combination. |

## Suggested 0.6 sequence

1. **Close ordinary contract defects first:** disabled popup commits, selection initialization, search events, labelled-field
   validation, table highlight selection, TableDesigner notifications and rendered-width fitting. Respect read-only/fixed
   collection capabilities. Each leaves a small behavior regression check.
2. **Migrate one vertical slice:** ActionButton/IconButton/Capsule onto native Button behavior with Shade themes, including
   a real form using commands, default/cancel, icons, confirmation and hold. Verify the visual style before expanding.
3. **Standardize text/toggle primitives:** native text properties and validation, CheckBox/ToggleSwitch semantics, compact
   action targets, predictable state notifications and bindable visual options. Remove redundant peers/handlers as replaced.
4. **Migrate collection controls:** ChoiceButton/Menu, tabs, TickGrid and pair/list editors. Use native ItemsSource, selection,
   commands and templates, preserving drafts and application content lifetime. Leave reference builders simple.
5. **Improve specialized controls selectively:** table binding/selection notifications and semantics, date/calendar ownership,
   color keyboard access, live summary commands. Keep the custom drawing/calculation/layout where it earns its maintenance.
6. **Measure distribution/startup again:** optional host-theme/font packaging and small hot-path changes follow the evidence.
   No dependency loader, schema engine, event bus, generic form framework or blanket native rewrite.

Use a small compiled-XAML consumer to test the contracts while migrating. Native control behavior, minimum template sizes,
focus, IME, commands, accessibility and allocation count must be checked together. Retaining a hundred obsolete aliases
would defeat the simplification; prefer one documented migration for this still-unreleased breaking development line.

**Verdict:** move Shade toward themed native primitives plus focused custom composites. Fix the routine binding/state
failures first; retain the spreadsheet, range/date design, color mixer and Masonry until a simpler implementation proves equivalent.

**Lean:** no invented line-count saving. Native migration can retire custom action/toggle peers and handlers; removing the
mandatory Fluent dependency is conditional on supplying the needed themes, not an immediate dependency deletion.

**Not checked:** real Narrator/VoiceOver/Orca, Windows/macOS IME and OS tray behavior, GPU-rendered frame timing, process
working set, actual trimmed/AOT publishes, or every formula/drawing permutation. The remaining release gates still apply.
