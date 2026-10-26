# Shade 0.5 / 0.5.1 release plan

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

## Before publishing 0.5

- [ ] Run the gallery on Windows and macOS: keyboard focus/traversal, date popups, IME/composition, clipboard and drag/drop.
- [ ] Check Narrator/VoiceOver/Orca with the new peers, including errors and offscreen navigation. Cell value providers
      are implemented; full row/column grid semantics are incomplete; day actions exist but platform behavior is not certified.
- [ ] Review selected density/font settings in a real consuming application. Default and 2x native gallery renders pass;
      compact badges/tags, custom fonts, long app text and explicit fixed widths still need app-level review.
- [ ] Run CI on the final reviewed commit and inspect both native and web galleries.
- [ ] Review the breaking migration in ADOPTION.md and package API changes. Recompile consumers of former constants.
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
