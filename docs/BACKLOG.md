# Component backlog toward 0.6

Shade is a desktop component library, not an application framework. Improve the existing controls before adding more
component families. Priorities below come from concrete integration gaps in this checkout and the official design-system
references reviewed on 26 October 2026. They do not imply full feature parity or accessibility certification.

The whole-component [audit](COMPONENT_AUDIT.md) records the reviewed baseline. 0.6 completes the native action/toggle,
picker/menu and editable pair-row scope, plus routine binding/lifetime fixes. The next patch work follows integration
into real applications; see [RELEASE_PLAN.md](RELEASE_PLAN.md) for the explicitly deferred platform checks.

## Implemented in the 0.5.1 follow-up

`MultiChoice` was pointer-only, returned its mutable backing selection, and offered no compiled-XAML selection binding.
`Set(control.Selected)` cleared the source before enumerating it. It now uses immutable option/selection snapshots,
Avalonia properties, native checkbox targets, expand/collapse automation, keyboard navigation and popup cleanup.
The existing constructor, Selected getter, Set and Changed contracts remain available. No additional dependency was added.

`PairTable.Ensure(new[] { "A", "A" })` also created two new rows and later made `Pairs()` throw for the duplicate.
The same batch now adds a name once, retaining existing values. A regression check covers repeated calls and names.

## Completed in the 0.6 migration

Native action/toggle/checkbox bases, bindable visual properties, ordered short-list picks, native mode radio segments,
ChoiceButton options/appearance, shared menu ownership, native menu targets, suggestion snapshots/highlight reuse,
editable PairTable.Entry collections with visible validation, and native tag/field actions are implemented. Ordinary
form/search/selection bugs and fixed-size table structure guards are fixed. Host theme composition can skip installing
Fluent again. Placeholder, designer, navigation and status bindings have compiled-XAML coverage.

## Next changes after app integration

| Priority | Concrete remaining gap | Next useful change | Acceptance evidence |
| --- | --- | --- | --- |
| 1 | Native platform input and screen-reader behavior are not certified by headless checks. | Exercise actual application forms, menus, toggles, dates and tables on the target OS; patch reproduced failures. | Real IME, focus, clipboard, drag/drop and Narrator/VoiceOver/Orca runs. |
| 2 | EditableTable cell value providers do not expose full grid/row/column relationships; row dictionaries do not notify arbitrary in-place edits. | Add relationships/source adapters when the consuming apps need them; preserve the current single editor and decimal calculation behavior. | Offscreen coordinates after sort/reorder, observable edits and unchanged caret; actual screen-reader runs. |
| 3 | Field/date composites have different native editing requirements; some date presentation and sidebar/navigation item options remain plain properties. | Extend bindings against actual integration needs; evaluate ordinary native TextBox presentation separately from segmented dates. | Compiled XAML, read-only/validation/IME behavior and unchanged visuals on the consuming layouts. |
| 4 | Unique-text Fit, very wide data, font assets and global theme scope have measurable workload-dependent costs. | Profile consuming apps before column virtualization, font/package splitting or scoped tokens. | Same-data before/after timings, allocations, rendering and functionality checks. |

## API standards to apply as each component changes

- Bind changing application state through Avalonia properties, with a parameterless constructor for XAML. Keep current
  constructor/method adapters when they can delegate to the same implementation.
- Separate model updates from user actions: both notify bindings, only user input raises the action/change event.
- State whether a collection is a snapshot or observed, how invalid keys are handled, and whether order is meaningful.
  Use immutable snapshots for small option lists; retain observable collections where table structure already supports them.
- Keep stable identity separate from display text when a consuming project needs translated or renamed domain options.
  Today's string-based pickers use stable names. MultiChoice retains keys while options are empty, then reconciles loaded
  options. Do not add a generic item framework without an integration case.
- Treat opening, dismissal, focus restoration and detach as one owned lifecycle. Prevent disabled input through pointer,
  keyboard and automation paths alike. Avoid parallel keyboard and automation implementations of the same action.
- Ship examples of default, selected, disabled, empty, long-text and scaled states with behavior checks. Gallery size alone
  does not demonstrate integration quality. Do not add a control merely to increase the count.

## What established systems contributed

- [Fluent 2 dropdown guidance](https://fluent2.microsoft.design/components/web/react/core/dropdown/usage): keep a
  multi-select checklist open until dismissal, make selected values/count understandable, and support option text navigation.
  Shade keeps that interaction while preserving its dense desktop styling and native Avalonia checkboxes.
- [React Aria Select](https://react-spectrum.adobe.com/react-aria/Select.html): explicit selection/value APIs and collection
  identity, disabled state and selection-change handling. Shade uses Avalonia's binding contract rather than introducing
  separate React-style controlled/uncontrolled component layers.
- [Radix Popover](https://www.radix-ui.com/primitives/docs/components/popover): focus and dismissal belong to the component
  lifecycle; Escape restores the trigger, while Tab continues navigation. Shade implements these rules with Avalonia Popup.

These are behavioral/API references. No external component code, visual system or dependencies were imported.

## Version and release policy

0.6 includes all unreleased 0.5/0.5.1 work and intentionally breaks action/toggle contracts to adopt Avalonia behavior
and adds the editable pair-row API. Publish 0.6.0 after automated release checks; use 0.6.x for integration fixes and
additive improvements. New unavoidable public breaks require another minor version. Keep app services outside Shade.


## 0.6.1 integration release

Included in 0.6.1: the searchable object/ID picker with accent match highlighting, input-modality
menu focus, wrapping/configurable sidebar branding, empty footer and bounded reorder-marker corrections, visible numeric
draft validation, per-column heading/subtitle sizes and italic cells, and bindable/synchronized colour helpers. The web
preview includes stable dropdown geometry, scrolling fixes, readable zone labels and complete mobile mode options.
The remaining real-platform/application gates above still apply; 0.6.1 includes these fixes for consuming applications.

## 0.6.2 native integration patch

- Center the 12 px column resize target across the divider, above both headers. Preserve pointer focus on the resized header.
- Keep the numeric editor's native validation state without Fluent's duplicate error-presenter spacing; invalid input must not grow a one-line row.
- Contain menu item reveal requests inside the menu, preserving its own keyboard scrolling without moving the surrounding page.

Manual app check: resize from both sides of a divider, enter/correct a short invalid number, and hover/navigate an embedded menu on a scrolled page.
The consuming app confirmed the native interaction fixes. Added nullable column alignment and per-cell alignment overrides using Avalonia TextAlignment; display/editing agree and row duplication retains cell overrides. Web parity remains deferred.

## EditableTable capability gaps after 0.6.3

These are separate from the reproduced defects addressed in the 0.6.4 patch:

- The ribbon cannot configure `Column.Decimals` or number/currency/date presentation. Existing precision and `Suffix`/`Display` values remain stored and applied. Design native formatting controls or a public host extension point before asking apps to replace the removed panel.
- Hosts cannot restrict the ribbon paper choices or provide ribbon-level validation callbacks. Hosts still own print/export policy and boundary validation. A future public contract must preserve bindings, show field errors and define completion behavior; do not silently turn domain constraints into library defaults.

The 0.6.4 corrections add no new public API or dependency. Windows popup/monitor-edge and printer confirmation remain manual platform checks.
