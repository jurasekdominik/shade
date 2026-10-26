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

The project remains an unreleased breaking 0.5 candidate. Nothing was tagged, pushed or published to NuGet.
See [ADOPTION.md](ADOPTION.md) for migration and [RELEASE_PLAN.md](RELEASE_PLAN.md) for next steps.
