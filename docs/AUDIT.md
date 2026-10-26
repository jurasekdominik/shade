# Shade release-candidate audit

What this repo does: Shade is an Avalonia desktop component library with a matching documentation site.
Applications own their data, persistence, permissions and business rules. This audit assumes a bounded desktop
viewport and loaded datasets up to the measured 10,000-row cases, not an unlimited remote spreadsheet service.

The review traced field/binding validation, action execution/lifetime, decimal parsing/calculation, row edits,
collection replacement, viewport realization, date input, automation providers, live language ownership, keyboard activation, metric geometry and the build/package workflow.
It is not a line-by-line review of every drawing operation or a screen-reader certification.

## Fixes verified

- Rows and their data/measurement caches are separate from realized controls. Large tables retain controls near the
  viewport and the active editor; other row controls are released. Variable-height rows use estimated extents until
  measured. Small tables preserve their existing control trees.
- Text measurement results are shared across repeated values, sizes, weights and fonts. The per-table cache is bounded
  to 16,384 entries. Scroll offsets reuse their prefix array instead of allocating it on each scroll event.
- Regression tests cover initial/end viewports, wrapped rows, editing/caret preservation while scrolling, Enter to an
  offscreen row, adding/focusing a row, offscreen aggregate updates and column replacement/reordering during editing.
- `Rows`, `Columns` and `IsReadOnly` are Avalonia properties. Invalid replacement lists fail before replacing the current
  lists. Column keys must be unique/nonempty; duplicate row instances are rejected.
- Column replacement previously measured old row arrays using the new column count and could crash. Rebuild now closes
  the editor without measuring obsolete structures, then restores it by column key.
- Field validation supports the standard view-model error pipeline. Numeric columns support explicit culture/parsers;
  validated edits reject invalid input without replacing stored data. Async actions prevent duplicate execution and
  support cancellation/failure reporting. Existing API compatibility changes remain declared for 0.5.
- Icon actions and state toggles now support keyboard/automation activation. Dates and realized table cells expose
  value providers through the same validation paths. Date labels target the accessible field, not its unused TextBox.
- Built-in language refresh uses weak control registrations. Default prompts and action labels refresh while app-owned
  labels, raw input, invalid drafts and caret positions are preserved. A collection check covers discarded controls.
- Tabs expose selection semantics and roving focus. Calendar days and summary actions expose native invoke targets.
  Tick/icon/chip activation cancels on pointer release outside and suppresses held-key repeats; confirmation needs a
  distinct second key press. Focus borders retain 3:1 contrast with extreme custom accents in both themes.
- Summary cards now compose native buttons with drawn surfaces. Visual review found zero-sized template content;
  explicit measurement/stretch fixes it, and gallery pixel checks prevent blank cards from passing behavior-only tests.
- Input/action heights, date blocks/calendars, summaries, progress, navigation, color helper, tick grids and table-designer
  rows follow startup metrics. Default/2x gallery generation passes; explicit app widths still constrain content.
- List values/hints and action labels support compiled XAML bindings; programmatic updates avoid user-action events.
- Displayed footer aggregates are cached across layout and invalidated by edits/refreshes/footer choices. Public footer
  calculations still read current model values. Choice width measurement avoids temporary TextBlock construction.
- Calendar rendering/navigation handles both .NET date boundaries, including months whose 42-day grid extends beyond
  DateTime.MinValue/MaxValue. Pasted date ranges enforce limits. Open calendars close when made read-only.
- The web page uses bundled Inter fonts and their license. Google Fonts is no longer a runtime dependency. The smoke
  test accepts HTTP URLs as well as file paths. Its column removal check waits for the asynchronous dialog close action.

## Performance evidence

Same-machine headless startup observations; times vary. Allocated bytes are cumulative managed allocations during
construction/showing, not retained working set or GPU/native memory. The wide fixture repeats numeric values across columns.

| Case | Visuals | Managed allocation | Startup |
| --- | ---: | ---: | ---: |
| Previous table: 1,000 rows × 1 column | 13,093 | 218 MiB | ~1.55 s |
| Current table: 1,000 rows × 1 column | 401 | 11 MiB | ~0.36 s |
| Row virtualization before shared width cache: 10,000 × 10 | 883 | 494 MiB | ~1.56 s |
| Current: repeated values, Fit, 10,000 × 10 | 905 | 70 MiB | ~0.62 s |
| Current: unique values, Fit + totals, 10,000 × 10 | 905 | 505 MiB | ~1.78 s |
| Current: unique values, fixed widths + totals, 10,000 × 10 | 905 | 55 MiB | ~0.54 s |

The three current fixtures increased the post-GC managed heap by approximately 25, 25 and 24 MiB respectively.
These are managed heap differences, not a process working-set measurement.

Run `dotnet run --project tests/Shade.Tests -c Release -- --scale 10000 10` to reproduce the wide case.
Append `--unique --footers` and optionally `--fixed` for the unique-value comparisons.
The scale command fails if controls grow with all loaded rows; timing is not a hard CI threshold.
All data and per-row width caches remain loaded. Fit widths and footer calculations still scan that data. Prefer fixed
column widths if profiling shows exact Fit measurements dominate a unique-text workload. Horizontal virtualization and
remote paging are not implemented. A bounded viewport is required, as with standard Avalonia virtualized lists.

## NocoDB inspiration

[NocoDB InfiniteTable.vue](https://github.com/nocodb/nocodb/blob/4f27d56930818ccf887565e4e7156faf5e1447b4/packages/nc-gui/components/smartsheet/grid/InfiniteTable.vue)
separates a visible row slice from its cached data and prunes work outside that slice. That principle informed this
change. Shade implements it independently in its existing Avalonia grids, preserving the single native editor,
variable-height rows and exact decimal calculations. NocoDB/Vue code and dependencies were not imported.

## Remaining release gates

1. **Manual platform accessibility checks.** Invoke/value/toggle/selection providers and named day targets pass headless
   checks. Narrator/VoiceOver/Orca, focus rendering, IME and platform popup behavior need real desktop verification.
   Full grid semantics remain incomplete; do not claim whole-library accessibility certification.
2. **Application metric coverage.** Default and 2x gallery renders pass, including summaries and table-designer rows.
   Compact badges/tags, long app labels, explicit fixed widths and custom fonts still require application review.
3. **Internationalization scope.** Live built-in language refresh is implemented. Full RTL layout and culture-driven date
   block order remain unsupported; directional text shaping alone does not provide either.
4. **Real wide/unique-text workloads.** Controls remain bounded, but exact Fit measurements still inspect loaded data.
   Fixed widths avoid most shaping costs; profile real app data before choosing further virtualization or remote paging.

## Validation

The warning-free Release build, 372 headless assertions, three large-table profiles, default/2x gallery contrast/component/live-theme
checks, generated documentation, 112 browser checks over local HTTP, design-token lint and package-content verification passed.
The browser passed both themes and responsive checks without disabling TLS or administrator policy. The file URL remains
restricted; serving the page via the supported HTTP test input avoids needing file navigation.

## 0.5.1 follow-up audit

The next review followed multi-selection through compiled XAML, pointer/keyboard/automation input and popup cleanup.
`MultiChoice` now has bindable snapshot collections and captions, native checkbox option targets, expand/collapse
providers, count-aware localization and typeahead. Programmatic updates do not emit user actions. Self-assignment no
longer clears selected values; replacing options filters unavailable names. Disable/hide/detach and Escape/Tab dismissal
release the popup and its ancestor subscriptions. Native option targets explicitly remove Fluent's minimum row height
so Shade's default/scaled metrics win. Long labels trim visually while retaining their complete automation names/tooltips.

`PairTable.Ensure` now deduplicates repeated new names in the same batch, preventing a later Pairs() duplicate-key error.
The wider pair-editing API, ChoiceButton/Menu lifecycle/automation, tag activation and table grid semantics remain ranked
in [BACKLOG.md](BACKLOG.md), with concrete integration cases and acceptance checks.

The project also removes duplicate PublicAPI file registration already supplied by the analyzer package. The analyzer
sees each baseline once and the whitespace verification gate now runs without workspace-load warnings.

The follow-up passes 399 C# assertions and 122 browser checks, warning-free build, public API validation, design-token
lint, default/2x native rendering and package contents. A 10,000 × 10 unique/fixed/totals profile remains bounded at
905 visuals, about 55 MiB cumulative managed allocation and 24 MiB managed heap increase (~0.49 s in this run).
No external component code or dependencies were imported. Official Fluent, React Aria and Radix references are linked
in the backlog. These results do not replace the remaining desktop/screen-reader release gates.

The project remains unreleased: 0.5 contains the earlier breaking changes and 0.5.1 adds the selection follow-up.
Work is on the review branch `codex/0.5.1-selection-api`. No release tag or NuGet publication was made.
See [ADOPTION.md](ADOPTION.md) for migration and [RELEASE_PLAN.md](RELEASE_PLAN.md) for next steps.

## First 0.6 implementation audit

The whole-component review in [COMPONENT_AUDIT.md](COMPONENT_AUDIT.md) led to a native button slice and routine
integration fixes on `codex/0.6-native-controls`. ActionButton, IconButton and Capsule extend Avalonia Button with
Shade-owned control themes. Native Content/ContentTemplate, routed Click, Command, default/cancel behavior and button
automation replace the parallel action implementation. Visual extension properties now bind/style normally. Confirmation
does not write into bound content; hold completion fires once even if a handler changes HoldMs before release. Icon and
capsule variants keep a visible keyboard focus border. ActionPeer/IconPeer were removed; specialized peers remain.

ChoiceButton owns its popup and closes on effective disable, ancestor hide and detach. Commit callbacks reject disabled
input. MultiChoice preserves selected keys through empty/loading options and reconciles non-empty snapshots. Search
clear emits one edit; FormLayout reuses accessible labels/validation. TableDesigner notifies selection reset. Workbench
highlights update inherited text state without recreating containers or selection, and FitWidth measures glyph width.
Fixed-size table structures hide unsupported Add/Delete/reorder actions without disabling editable cells.

Validation: **421 C# assertions**, **124 browser checks**, warning-free Release build/public API validation, whitespace
verification, design-token lint with the repository's existing exemptions, default/2x gallery generation and live-theme
pixel equality. Affected native previews and the web buttons were inspected in light and dark modes. The 0.6.0 package
contains the DLL/XML, guides, design contract, icon and licenses; bundled guides match source (about **1.31 MiB** compressed).

The 10,000 × 10 unique/fixed/totals fixture remains bounded at **970 visuals**, about **56 MiB** cumulative managed
allocation and **25 MiB** managed heap increase (~0.51 s in the initial run). The 0.5.1 observation was 905 visuals,
55 MiB allocated and 24 MiB heap increase. Native templates add a small visual/allocation cost; this change improves
compatibility and removes duplicated behavior, with no claim of a speed improvement. These headless startup observations
do not establish GPU frame rates or platform guarantees.

The rest of the 0.6 audit sequence remains in RELEASE_PLAN.md. Custom table/date behavior and the single package/fonts/
Fluent dependency remain; package splitting and additional virtualization require a measured consuming workload.
Windows/macOS input, IME, clipboard, drag/drop and actual screen-reader checks remain release gates. No release tag,
merge or NuGet publication was made.

## 0.6 release audit

This release completes the planned native action/toggle, picker/menu and editable pair-row migration. It includes all
unreleased 0.5/0.5.1 work; those versions are not separate published prerequisites. The earlier audit sections above are
historical observations. Current migration details are in ADOPTION.md, and remaining integration work is in BACKLOG.md.

- Switch/StateToggle/FilterChip now use native ToggleButton behavior; Tick uses CheckBox. Native IsChecked, commands,
  routed clicks and automation replace the duplicated state/input/peer engines. Drawn faces and switch motion remain.
  Native radio mode segments and native clear/remove/undo buttons cover the remaining primary interaction targets.
- ChoiceButton is a native Button with bindable option/appearance snapshots and expand/collapse automation. Menu actions
  are native buttons. Shared popup ownership handles effective disable, ancestor hide and detach, with Escape/Tab focus
  behavior. Suggestion options bind; arrow navigation updates the existing popup while preserving native editor focus.
- PairTable binds observable Entry rows with notifying Name/Value/Error, case-sensitive or insensitive duplicate checks,
  visible validation and HasErrors. Blank/duplicate drafts are retained, TryGetPairs rejects invalid export, user edits
  notify once, and structure additions preserve existing editors/carets. Fixed-size sources keep editable rows and hide
  unsupported structural actions. Dictionary adapters remain; Pairs defaults to the configured comparison policy.
- Placeholder, host default port, search count, short-list picks, designer inputs, navigation commands/availability and
  status labels bind. Hosts can skip installing Fluent a second time. Non-removable tags allocate no remove button.
- Confirmation preserves bound Content and restores custom templates after disarming. Picker triggers retain native
  Button commands. Read-only search/undo/suggestion paths reject changes. Native checkbox templates/minimum heights are
  explicitly suppressed on decorative tick faces, preserving square default/scaled metrics inside native rows.

Validation: **466 C# assertions**, **2 additional host-theme assertions**, **131 browser checks**, warning-free Release
build/public API validation, whitespace verification, token lint with the existing repository exemptions, default/2x
native galleries and live-theme pixel equality. Affected native controls were inspected in both themes; web choices
and pair drafts were inspected at desktop and phone widths in light/dark modes. Package contents and bundled guides
are checked before release. The public API baseline is rolled into Shipped for 0.6.0.

The 10,000 × 10 unique/fixed/totals fixture remains at **970 visuals**, about **56 MiB** cumulative managed allocation and
**25 MiB** managed heap increase (~0.49 s in the observed run). Repeated/Fit remains about 70 MiB allocated (~0.62 s).
These are bounded headless startup observations; no faster-rendering, GPU frame-rate, trimming/AOT or OS accessibility
claim follows. Native templates retain the small overhead recorded in the first 0.6 slice.

A final component review keeps the specialized virtualized table/calculations, segmented dates, native child text
editors and concise builders. It does not force a DataGrid/DatePicker/TextBox base rewrite, add an app framework, or
split the small package into per-component assemblies. Sidebar/navigation presentation options, richer table source/grid
semantics, scoped tokens and optional font packaging remain integration-driven follow-ups rather than hidden promises.

The user explicitly authorized releasing 0.6 before app integration. Windows/macOS/Linux real input, IME, clipboard,
drag/drop and Narrator/VoiceOver/Orca checks are deferred to 0.6.x integration and are not recorded as passed. Follow the
Publish workflow result and NuGet version when confirming publication; a local package or pushed tag alone is not proof.

## SearchChoice follow-up (0.6.1 candidate)

The searchable picker keeps native ComboBox object/ID selection on the full source and uses a separate native virtualized
results list. Query/highlight changes do not alter the committed object or bubble internal selection events to callers.
Words match independently across app-selected fields, without case/accents; labels and IDs remain separate. Native row
buttons handle pointer capture/invoke, while popup search retains editor focus. Observable source changes refresh the
cache, and explicit RefreshSearch covers in-place search-field changes without thousands of permanent item subscriptions.

A 10,000-object headless fixture initially realized 10 rows / 83 visuals and allocated about 10 MiB for opening, filtering
and scrolling (~176 ms in the observed run). This demonstrates bounded row realization, not a GPU or API-fetch guarantee.
Local filtering, custom item bindings/templates and normal selection behavior are in scope; remote loading infrastructure
and multiple selection are deliberately left to a future concrete use case. The 0.6.0 published package is unchanged.

The candidate passes 490 C# assertions, 2 host-theme checks and 142 browser checks, strict API/build validation,
whitespace verification, token lint, package contents and default/2x native rendering with live-theme equality. Added
regressions cover a source row removed before its queued refresh, full-width trigger geometry, rich template updates,
explicit cached-field refresh and native display notifications. Native open/filtered/empty/disabled states and web popup
views were inspected in both themes, including phone width. Real OS and screen-reader behavior remains an integration check.
