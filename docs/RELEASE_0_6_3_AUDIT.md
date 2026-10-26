# Shade 0.6.3 — editable-table implementation audit

## Scope and assumptions

Reviewed the native `EditableTable` implementation, its column ribbon and paper-layout partials, their callers in the gallery, public API declarations, XAML bindings, and the corresponding CSS/JS component. Also traced the shared Field, ChoiceButton/Menu, TextTabs, ActionButton, FormLayout, and localization contracts used by the ribbon.

Assumed a desktop app on Avalonia 12/.NET 10, with UI/model changes on the UI thread. Load checks cover 10,000 rows × 10 columns, repeated and unique values, fixed widths and calculated footers. This is a focused implementation/release audit, not a claim that the entire repository or every consuming app was audited.

## Findings corrected before release

1. **Paper edits could replace application bindings.** The new paper properties now default to two-way bindings. User actions use `SetCurrentValue`, preserving binding expressions. Regression tests exercise a real native picker, view-model updates in both directions, custom dimensions, and effective `PaperSize` notifications.
2. **A focused unit draft could reach the newly selected column.** Each field now tracks its owning column. Changing selection commits a valid unit draft to its original column and refreshes the new column's field. Tests verify that the other column's unit list remains unchanged.
3. **A delete confirmation could become stale.** Removal now rechecks read-only state, collection mutability, column identity, and `CanRemoveColumn` after confirmation. Tests revoke permission while the dialog is open and replace a column with another using the same key. Neither case deletes the protected data.
4. **Permission changes could commit an outstanding configuration draft.** Field and paper callbacks now check edit/read-only state. Tests revoke editing while a unit draft is focused and verify that the saved definition stays unchanged.
5. **The initial native editor lacked paper controls and had several web interaction differences.** It now includes paper presets, orientation, validated custom dimensions, and an actual constrained preview width. Fixed-width controls are conditional, width drafts respect min/max bounds, unit lists commit on Enter/focus loss, and Add term chooses an unused numeric source before reusing one.
6. **Fixed grouping could overlap at larger text sizes.** The native toolbar and table-setting groups reuse Masonry to wrap. The formula editor is capped to the available ribbon width. Tests check native field/dropdown containment in both modes at 1× and 2× text scale.

7. **The narrow formula operator picker clipped its symbol.** The native operator column now allows two standard control heights of width for the shared dropdown chrome. Tests verify visible operator text, including at 2× text scale, so “+”/“−” are not reduced to an empty picker.

## Rules and Avalonia contracts

- The editor remains opt-in through the bindable `ShowColumnEditor` property. Existing table layouts and default behavior are preserved.
- Toolbar actions, fields, dropdowns, tabs, error labels, and layout use existing Shade controls and role brushes. No new dependencies or literal colors were introduced.
- Edit columns/Done share a stable slot. Done is a standard secondary action. Remove is separated from the creation actions and uses the existing named confirmation.
- TextTabs supplies the sliding underline and keyboard selection. Shared ChoiceButton/Menu supplies native dropdown focus, reveal, dismissal, and attached borders; it is not a ribbon-specific menu implementation.
- Numeric invalid drafts retain their accepted values. Configuration errors are visible and use the shared Field validation surface. Validation messages and built-in labels participate in live localization.
- Alignment follows `Row.Alignments` → `Column.Alignment` → automatic numeric/text placement. Display and cell editing agree. Heading/footer alignment is unchanged.
- `PaperFormat`, `PaperLandscape`, and `CustomPaperSize` are validated styled properties with two-way defaults. `PaperSize` is an observable, read-only effective size in millimetres. Invalid dimensions and undefined formats are rejected.
- Existing heading/subheading/value-size binding defaults are preserved; use explicit `Mode=TwoWay` when the view model should receive ribbon changes to these properties.
- Unit changes preserve stored row units. Formula sources retain column identity even when titles are duplicated. Calculations continue to use the native decimal engine.
- Existing independent `CanResize`, `CanEditFooter`, `CanRemoveColumn`, mutable-collection, and read-only contracts remain in force.
- UI event subscriptions are owned by the table/control tree; localization uses Shade's conditional weak registrations. No new static list of tables, external timer, service, or background worker was introduced.
- The additive public API is recorded in `PublicAPI.Shipped.txt` for 0.6.3 and is exercised by compiled XAML bindings.

## Release gates

- Release build with warnings treated as errors.
- Native control, binding, validation, permission-race, alignment, and geometry tests.
- Existing Fluent host-theme composition checks.
- Search-choice scale check and both 10,000 × 10 table scale variants used in CI.
- Native gallery generation, live/fresh theme comparisons, and inspection of affected dark/light previews, including the formula and paper panels.
- Web component interaction checks, including all column alignments, unit definitions, floating/connected dropdowns, underline geometry, paper formats, keyboard behavior, and narrow-window layouts.
- `DESIGN.md` lint with the repository's two established allowances: disabled-text contrast and the linter's unrecognized light-theme token section.
- Whitespace validation, generated-document consistency, package creation, and package-content inspection.

## Platform and integration limits

Checks run on Linux headless Avalonia and Chromium. Windows/macOS window-manager behavior, native popup placement against real monitor edges, assistive technology on a physical desktop, and physical printer output are not claimed as tested.

Paper controls configure the preview and expose `PaperSize` for the consuming app's native print/export pipeline. The library does not submit print jobs or introduce a PDF exporter. Existing app-specific validation, persistence, print margins, pagination, and deployment remain the consuming application's responsibilities.

## Verdict

No remaining blocking finding in the audited implementation after the fixes above. The native and web versions expose the same column-editor feature set through their respective shared controls; platform integration limits are recorded rather than described as verified.
