# Shade 0.6 release plan

0.6 is in progress on `codex/0.6-native-controls`, based on the unreleased 0.5/0.5.1 work below. The component audit
sets the remaining sequence. This first slice does not finish the whole 0.6 release.

## Completed first 0.6 slice

- ActionButton/IconButton/Capsule now extend Avalonia Button with Shade-owned control themes. Content, routed Click,
  Command, default/cancel behavior and automation use the native contracts. Visual extensions bind/style normally.
- Confirmation leaves bound content intact; hold, cancellation, failure and duplicate prevention retain regression coverage.
- ChoiceButton owns popup dismissal on effective disable, ancestor hide and detach; callbacks reject disabled input.
- MultiChoice retains selection while options are empty and reconciles a non-empty snapshot, independent of binding order.
- Search clears once; FormLayout uses shared labels/validation; TableDesigner selection resets notify bindings.
- Workbench highlighting updates realized text without rebuilding selection/containers; FitWidth measures actual glyphs.
- Fixed-size table collections retain cell editing while unsupported structural actions are hidden or explicitly rejected.
- Compiled-XAML/native behavior tests, default/2x galleries, live theme equality, web parity, API baselines and migration docs.

## Remaining 0.6 sequence

1. Native toggle/checkbox targets and removable tag activation, preserving Shade's geometry and explicit input semantics.
2. Bindable single-picker options and native menu action targets; extend the popup ownership work rather than duplicating it.
3. Bindable editable PairTable rows that retain invalid drafts and report duplicate keys without losing user input.
4. Field/native property forwarding and selectors where native controls remove work without compromising interaction/design.
5. Profile consuming applications before column virtualization, remote paging or package splitting. See the audit's package
   measurements; this slice keeps the existing package/fonts/Fluent dependency and adds no selective-loading switch.

Retain custom date/table drawing where it solves the dense desktop interaction. Apply the audit's likelihood/complexity
assessment to each change; do not add frameworks for hypothetical edge cases.

## Preserved 0.5 candidate

0.5 is a breaking development release. It is not published. The scope is a reusable Avalonia desktop component
library with startup configuration/localization, validated editing and bounded row realization. It does not promise
server-side data loading, every language/layout direction, or complete screen-reader certification.

## Completed in this candidate

- Bindable field validation/read-only/pending state and standard view-model validation.
- Explicit numeric input culture/parser and count-aware localization hooks.
- Async action cancellation, duplicate prevention and failure reporting.
- Observable/bindable table collections, bindable read-only state and validated edits.
- Row virtualization with wrapped rows, offscreen aggregates and a pinned editor; bounded text measurement cache.
- Distinct row identity and nonempty unique column keys; invalid replacements leave the old lists installed.
- Column replacement/reorder while editing preserves the column key and avoids stale width caches.
- Invoke/toggle/value automation for primary actions, icon actions, switches, ticks, state toggles, dates and table cells.
- Date boundary navigation/rendering; named native calendar-day actions.
- Live built-in language refresh with weak registrations and preservation of application text/invalid editor drafts.
- Summary action buttons, tab selection semantics, roving focus, repeat-safe activation and contrasting focus borders.
- Broader metric coverage: summaries, progress, navigation, color helper, tick grids and table-designer rows.
- Bindable list values/hints and action labels; compiled-XAML coverage.
- Cached displayed footer aggregates and wide/unique/fixed-width performance fixtures.
- Self-hosted documentation fonts and an HTTP option for the browser smoke test.
- Headless behavior tests, large-table realization checks, gallery checks, browser checks, public API baseline and packaging.

## Additive 0.5.1 follow-up

- `MultiChoice` now supports compiled XAML bindings for options/selection/captions and immutable collection snapshots.
- Native option checkbox semantics, expand/collapse automation, keyboard/typeahead navigation and predictable dismissal.
- Option replacement, disable/hide/detach close and release popups; programmatic selection updates do not raise user events.
- PairTable.Ensure deduplicates new names within the same batch.
- Self-assignment no longer clears selected values. Native/web examples and regression coverage follow the same behavior.
- The earlier 0.5 breaking work is preserved in a separate commit; neither version is tagged or published.

See [BACKLOG.md](BACKLOG.md) for researched priorities and reasons to refactor the next controls.

## Before publishing

- [ ] Run the gallery on Windows and macOS: keyboard focus/traversal, date popups, IME/composition, clipboard and drag/drop.
- [ ] Check Narrator/VoiceOver/Orca with the new peers, including errors and offscreen navigation. Cell value providers
      are implemented; full row/column grid semantics are incomplete; day actions exist but platform behavior is not certified.
- [ ] Review selected density/font settings in a real consuming application. Default and 2x native gallery renders pass;
      compact badges/tags, custom fonts, long app text and explicit fixed widths still need app-level review.
- [ ] Run CI on the final reviewed commit and inspect both native and web galleries.
- [ ] Review the breaking migrations in ADOPTION.md and package API changes. Recompile consumers of former constants
      and migrate native button content/events/base-class assumptions.
- [ ] Publish only after these checks and explicit release authorization; no tag or NuGet publication has been made here.

## Next work after this scope

1. Verify actual screen-reader behavior and finish full row/column grid semantics where consuming apps require them.
2. Exercise long translations and chosen locales in consuming projects. RTL layout and culture-driven date block order
   need dedicated design and tests; live built-in language refresh is implemented.
3. Review remaining compact badge/tag geometry and explicit app widths with the selected font/density.
4. Profile real wide grids. Exact Fit measurements remain proportional to unique loaded values; fixed widths already
   avoid most shaping costs. Column virtualization or remote paging should follow concrete application needs.
5. Add row-level edit transactions when a consuming project needs commit/cancel semantics beyond validated cell edits.

Keep app storage, transports, authorization and business rules outside Shade. Prefer extending existing Avalonia
contracts and Shade controls over adding an application framework or a parallel component system.
