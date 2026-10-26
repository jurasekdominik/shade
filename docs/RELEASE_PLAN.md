# Shade 0.6 release plan

0.6 combines the unreleased 0.5/0.5.1 work with the native Avalonia migration. The user authorized publishing 0.6.0
before integrating it into consuming applications; platform/application findings will be addressed in 0.6.x patches.

## Included in 0.6

- Native Button actions and Shade control themes: Content, routed Click, commands, default/cancel behavior and automation.
- Native ToggleButton/CheckBox state and commands, preserving Shade artwork and switch animation. Native mode radio segments.
- Bindable single/multiple/short-list choices, native menu actions, owned popup lifetimes and predictable keyboard focus.
- Bindable suggestion snapshots, native editor focus and highlight updates without recreating the popup on every arrow key.
- Observable editable pair rows, duplicate validation with explicit comparison rules, draft retention and safe export.
- Native remove/clear/undo actions; bindable placeholders, table-designer inputs, navigation availability/commands and badges.
- Host-theme composition without installing Fluent twice; one package and the standard embedded fonts remain.
- Runtime startup fonts/metrics, localization/plural forms/culture, field validation and date limits from 0.5.
- Async action cancellation/failure/duplicate prevention, observable table sources and validated cell edits from 0.5.
- Bounded row realization, cached text measurements/footers, decimal formulas and culture-aware number formatting/parsing.
- Form/search/selection corrections, fixed-size table source guards, compiled XAML, native/web examples and migration docs.

## Release verification

The release requires the warning-free build, current shipped public API baseline, C# behavior and compiled-XAML checks,
host-theme composition, bounded large-table fixtures, default/2x native rendering with live-theme pixel equality,
browser interactions in both modes, design-token lint with the existing exemptions, whitespace and package-content checks.
Run CI on the final commit before tagging v0.6.0. The Publish workflow uses NuGet Trusted Publishing; verify its result
and the actual package version rather than treating a tag as proof of publication.

## Integration follow-up for 0.6.x

- Windows/macOS/Linux real input: focus/traversal, IME/composition, clipboard, popups, drag/drop and command interactions.
- Narrator/VoiceOver/Orca: new native targets, errors, calendar navigation and offscreen table cells. Headless automation
  checks are useful evidence, not screen-reader certification. Full table grid/row/column semantics remain incomplete.
- Consuming applications' selected fonts/density, long translated text and explicit fixed widths. Default/2x gallery
  checks do not guarantee arbitrary app layouts, custom fonts, RTL or culture-driven date block order.
- Profile actual wide/text-heavy data before column virtualization, remote paging, batching or package/font splitting.
- Exercise live PairTable source updates and application save rules; validate migration callers against ADOPTION.md.

These platform/application checks are explicitly deferred to integration, not recorded as passed release checks.

## Deliberate boundaries

Keep the custom virtualized table editor/calculations and segmented date/range controls. Ordinary/composite fields use
native child editors; replacing the shared field base is deferred until it removes work without disrupting date/host
workflows. No generic form schema, app navigation/data service, remote option framework or per-component loader is added.

All loaded table rows remain in memory, exact Fit scans loaded values, and columns are not virtualized. Use bounded layout
and explicit widths when appropriate. The package is not certified for trimming/AOT. The host owns storage, permissions,
networking, business validation and save/commit transactions. See BACKLOG.md and COMPONENT_AUDIT.md for future priorities.

## 0.6.1 candidate

SearchChoice adds a native searchable object/ID picker with a fixed popup header, word-based accent/case-insensitive
filtering and virtualized loaded options. It preserves committed selection during filtering, accepts native data templates
and observes source collections. The candidate includes compiled binding, keyboard/pointer/automation, lifecycle and
10,000-option checks, native/web examples and default/scaled previews. Data fetching and multiple selection are outside
this first searchable-picker scope. Development is on codex/0.6.1-search-choice; the published 0.6.0 release is unchanged.

The same candidate includes integration feedback: pointer/keyboard menu focus, wrapping/reserved-space sidebar branding
with configurable expanded width, truly empty None footers, grid-bounded reorder markers, visible numeric validation,
per-column heading/subtitle sizes and italic cells. Default search labels highlight case/accent-insensitive matches.
Web fixes cover menu scrolling, shared accent selection, stable dropdown captions and complete mobile mode options.
