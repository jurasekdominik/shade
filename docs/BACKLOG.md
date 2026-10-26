# Component backlog toward 0.6

Shade is a desktop component library, not an application framework. Improve the existing controls before adding more
component families. Priorities below come from concrete integration gaps in this checkout and the official design-system
references reviewed on 26 October 2026. They do not imply full feature parity or accessibility certification.

The whole-component [audit](COMPONENT_AUDIT.md) supersedes this earlier shortlist where priorities differ.
The first 0.6 slice is implemented: native ActionButton/IconButton/Capsule themes, bindable visual properties, routine
form/search/selection corrections, ChoiceButton popup ownership, deferred MultiChoice selection and fixed-size table
structure guards. See [RELEASE_PLAN.md](RELEASE_PLAN.md) for the remaining sequence.

## Implemented in the 0.5.1 follow-up

`MultiChoice` was pointer-only, returned its mutable backing selection, and offered no compiled-XAML selection binding.
`Set(control.Selected)` cleared the source before enumerating it. It now uses immutable option/selection snapshots,
Avalonia properties, native checkbox targets, expand/collapse automation, keyboard navigation and popup cleanup.
The existing constructor, Selected getter, Set and Changed contracts remain available. No additional dependency was added.

`PairTable.Ensure(new[] { "A", "A" })` also created two new rows and later made `Pairs()` throw for the duplicate.
The same batch now adds a name once, retaining existing values. A regression check covers repeated calls and names.

## Next components to refactor

| Priority | Component and concrete gap | Smallest useful next change | Acceptance evidence |
| --- | --- | --- | --- |
| 1 | `ChoiceButton` / `Menu`: single-selection options remain method-only; generic menu rows are plain Borders, with keyboard highlights separate from native automation. Popup ownership/effective-enabled checks are fixed in the first 0.6 slice. | Add bindable loaded options while preserving SetItems. Give menu actions native invoke/focus targets without changing the menu design. | Compiled XAML, Escape/Tab focus, native invoke on an item and identical light/dark appearance; retain parent-disable/hide/detach coverage. |
| 2 | `PairTable`: only constructor/Set/Pairs/Draft methods integrate values; no parameterless constructor or bindable editable row collection. Pairs refuses duplicate names only when read, so the app must catch the exception. | Define a bindable row model that can retain invalid drafts and display duplicate-name errors before committing. Preserve dictionary adapters; avoid collapsing duplicate drafts into a dictionary binding. | Duplicate keys remain editable, case-sensitive and header-style case-insensitive validation, two-way XAML and focus preservation on unchanged values. This may warrant 0.6 if public contracts must change. |
| 3 | `Tag` / `TickGrid`: a removable tag's cross is a pointer-only Border and accepts any pointer release; TickGrid labels toggle on release without a matching press. Tag/badge geometry still contains fixed small heights. | Reuse native/shared activation for remove and label targets, enforce primary-button activation and cancel drag-release, then scale compact geometry with the existing metrics. | Keyboard and automation removal, right-click/drag-release do not remove/toggle, default and 2x text in both modes. |
| 4 | `EditableTable`: cells expose values, but a screen reader cannot query full grid row/column relationships. Exact Fit still shapes every unique loaded value; columns and data remain loaded. | Add grid/row relationships against the existing row metadata. Keep exact Fit and fixed sizing explicit; introduce paging or column virtualization only for a measured consuming workload. | Offscreen row identity and grid coordinates remain correct after sort/reorder; actual Narrator/VoiceOver/Orca runs; unique-data benchmark alongside repeated values. |

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

0.5 contains the earlier breaking metric/base-class migration; 0.5.1 preserves its additive selection follow-up.
0.6 intentionally breaks button API/base-class contracts to adopt Avalonia behavior directly. These branches remain
development work until the platform gates in RELEASE_PLAN.md pass. Keep app data services, storage, permissions and
navigation outside Shade; no release tag or NuGet publication is authorized by the implementation work.
