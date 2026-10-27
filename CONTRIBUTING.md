# Contributing to Shade

This guide covers contributions and issue reports for Shade's Avalonia library and matching web components.

## Keeping the rules unified

Each topic has one owning document. Other documents summarize and link to it; do not maintain independent copies.

| Topic | Owning source |
| --- | --- |
| Visual and interaction principles | `docs/DESIGN_SYSTEM.md`; `DESIGN.md` exposes the matching token/component contract to apps |
| Supported API use, configuration, migration and limits | `docs/ADOPTION.md`, checked against the implementation and tests |
| Consuming-app agent rules and reporting/adoption loop | `docs/APP_AGENTS.md` |
| Shade contribution, refactor and verification workflow | This guide; `.github/workflows/ci.yml` (every PR) and `full.yml` (before a release) define the checks |
| Instructions specific to working in this repository | `AGENTS.md`; `CLAUDE.md` imports it |
| Installation and entry points | `README.md`; `src/Shade/PACKAGE.md` summarizes the package |

The changelog describes historical scope. An open issue records a known gap,
not a supported feature or an exception to current rules. Old publishing instructions do not authorize a new
release. The app follows documentation from its installed version; newer repository guidance does not add APIs to it.

When changing a contract, update its owning source and affected summaries, app guidance, examples, issue templates and
tests in the same change. Update packaged-document entries and CI package checks when adding shipped guidance. Remove
obsolete instructions rather than appending a contradictory rule. If sources disagree, reproduce the mismatch and
resolve it explicitly; do not silently choose whichever wording permits the change. Review document links and guidance
on releases/upgrades. Generated tokens remain generated; edit their sources and preserve hand-written prose.

## Before starting

Read [AGENTS.md](AGENTS.md), [the design system](docs/DESIGN_SYSTEM.md) (especially section 13), and
[DESIGN.md](DESIGN.md). For app integration, check [ADOPTION.md](docs/ADOPTION.md); open gaps are GitHub issues. People and coding agents follow the same project contract.

Search existing issues before opening one. Keep one independently fixable problem per issue and link related reports.
Small fixes and documentation improvements can go straight to a pull request. Propose new components and breaking API
changes in an issue first, with the use case and intended contract.

Shade contains reusable controls and tokens. App pages, routes, domain colours and business logic belong in the app.
If an app finds a shared bug or gap, follow [the app-agent workflow](docs/APP_AGENTS.md) to report a fix, an improvement
to an existing control or a new component before building an app-local substitute.

## Describing an issue

Use the bug report or component/improvement/refactor template; it lists what to include. One independently fixable
problem per issue. A good title names the component, trigger and result: `ChoiceButton: Escape leaves keyboard focus
outside the field`. State impact, a minimal reproduction, expected versus actual behavior, and the conditions you
checked (mode, fresh versus live switch, pointer versus keyboard, desktop versus web); mark unchecked cases unknown.
Give observable acceptance criteria. A report needs no diagnosis.

Suspected causes ("possible offenders") are code paths, never people, and stay separate from confirmed observations.
Follow shared callers before proposing a local fix. Where to look first:

| Symptom | Places to inspect |
| --- | --- |
| Colour differs after a live switch | `Look`, role brushes, the control's `IRestyle` / `Restyle()` or `Render` path |
| Value or selection fails to update | Avalonia property registration, change notifications, binding mode, item identity |
| Keyboard or pointer action behaves differently | Native action targets, focus, popup lifecycle and event handling |
| Desktop and web disagree | The control, `docs/shade.css`, `docs/shade.js`, and their gallery examples |
| Generated docs differ from the control | Gallery export, generated blocks, `gallery/landing.html` |

Confirm a cause with a failing test, a minimal reproduction or a controlled comparison. A suspected regression commit is
not proof from timing alone.

## Making a change

### Native foundations and library boundaries

Improve existing components before adding another component family. Shade owns its visual language and shared interaction
contracts; Avalonia supplies the underlying control semantics. Use this order when designing or refactoring:

1. Reuse the existing Shade component or shared helper when it fits.
2. Inside Shade, derive from or compose the appropriate native Avalonia control, then apply Shade's theme and tokens.
   Examples include `ActionButton : Button`, toggles based on `ToggleButton` / `CheckBox`, and `SearchChoice : ComboBox`.
   Reuse native content, commands, selection, editing, focus and automation contracts.
3. If Avalonia lacks a needed capability, evaluate already installed Avalonia-related libraries before adding one.
   A new dependency proposal must name the concrete gap, explain why existing tools do not meet it, and assess framework
   compatibility, maintenance, licence, transitive dependencies, package cost and ability to use Shade's visuals.
4. Implement only the missing behavior in Shade. Explain why native controls or an appropriate library cannot provide it,
   and cover the custom input, accessibility and lifecycle paths with focused checks.

The ban on raw Avalonia controls in app views still applies: apps consume Shade; Shade uses Avalonia internally.
Using a native base must preserve Shade's artwork. Do not let a framework or third-party theme introduce different
colours, geometry or interaction states. Do not import another design system or add an abstraction framework for a
single control. Keep application services, persistence, permissions and business validation in the host.

### Refactoring existing components

A refactor needs a concrete reason: a reproduced defect, duplicated shared logic, missing binding/native semantics,
an accessibility or lifecycle gap, or a measured performance problem. Use the improvement proposal template for substantial
refactors. Record the current implementation and intended outcome before replacing it.

1. **Inventory the contract.** Trace callers, public members, constructors, bindings, events, item identity, validation,
   translation, focus and popup behavior. Locate the web counterpart, gallery states, tests and adoption guidance.
2. **Capture a baseline.** Reproduce the problem and capture affected C# and web states in both modes. Record what must
   remain compatible and any intentional behavior change. Measure performance with the same data before and after;
   a rewrite alone is not evidence of an improvement.
3. **Choose the smallest migration.** State the native control or library capability that replaces custom code. Keep one
   source of state and one activation path for pointer, keyboard, commands and automation. Preserve constructor/method
   adapters when they can delegate to that implementation; explicitly document unavoidable public breaks.
4. **Preserve active interaction.** Keep unchanged editors, caret, focus, selection and invalid drafts when updating data,
   language or appearance. Own popup dismissal, subscriptions, timers and cancellation through disable/hide/detach and
   reattachment. Remove obsolete code after its replacement is connected to every caller.
5. **Verify the whole contract.** Add a regression for the failure or changed behavior, exercise compiled XAML and
   application-style updates, and complete the visual comparison below. Update the API listing, adoption/migration
   notes, design docs and gallery where their contracts changed.

Do not silently fold an API redesign into an internal cleanup. Existing limitations in the backlog are investigation
inputs, not permission to change unrelated contracts or claim support that has not been tested.

### Bindings, data and validation

Use inherited Avalonia properties and routed events where they already express the contract. Register new styleable
values as `StyledProperty`; use `DirectProperty` with `SetAndRaise` for directly owned state. Inputs default to two-way
binding. Internal user updates to styled properties use `SetCurrentValue` where needed to preserve the host's binding;
do not replace a binding with a local value or recreate bound content to show a temporary state.

Test a real binding in both directions: initial view-model value, later model update, user edit, then another model
update. Programmatic updates notify bindings without synthesizing user action events. User actions emit the documented
event once; follow native event semantics rather than adding competing callbacks. Keep `Command`, disabled state and
automation on the same action path.

Document snapshot versus observable collections, stable item identity, ordering, empty/loading behavior and reconciliation
when options change. Preserve existing contracts unless explicitly migrating them. Update UI state on the UI thread;
detach old subscriptions when sources change and reconcile state on reattachment.

Use native validation where available and show an explanatory message. Preserve invalid drafts without committing them;
define commit, cancel and discard behavior. Parsing, presentation and stored values are separate contracts. Business rules
and the decision to save remain with the app. Extend `Bound.axaml` and focused behavior tests for changed bindable APIs.

### Translation and culture

Follow [ADOPTION.md](docs/ADOPTION.md)'s translation contract. Route every built-in visible string, tooltip, validation
message and automation name through Shade's localization path. Use English templates with arguments rather than
concatenating translated fragments. `Look.Localize` handles final formatting/plurals and takes precedence over
`Look.Translate`; missing keys fall back to the original English text. The app supplies translations and plural rules.

Support `Look.RefreshLanguage()` for built-in strings, preserving bindings, input, invalid drafts, caret and focus without
raising edit events. Application-provided titles, option labels and custom placeholders remain app-owned. Keep stable
keys separate from translated display text; language changes must not change selection identity or stored values.

Use the documented culture for each operation: `Look.Culture` for built-in language/culture behavior and explicit input
culture or parsers for numeric input. Do not reinterpret stored values when changing display language. Check longer
translations, count templates, calendar names, automation names and decimal-comma input where relevant. Full RTL layout
and culture-driven date input remain documented limitations; record unsupported cases explicitly.

### C# and web visual parity

The C#/Avalonia implementation and web version must remain visually identical in equivalent states. Native foundations
are an implementation choice; they do not change Shade's appearance. Both versions follow the same tokens, typography,
icons, spacing, dimensions, borders, alignment, state treatments and content. Match user-visible behavior too, including
selection, validation, focus indication and popup dismissal, using each platform's native semantics.

Compare native gallery output with the web component side by side using the same content, mode, accent, logical viewport
and scale. Cover applicable states: default, hover, pressed, keyboard focus, selected, disabled, empty, invalid, busy and
open popup. Include long/localized text and supported larger text/density settings. Check fresh construction and live
mode/accent switching; check language refresh when strings change.

Include paired C#/web screenshots in the pull request for affected states in dark and light modes. Inspect clipping,
wrapping, popup geometry and scrollbars as well as colour. Browser checks and native tests verify their respective
implementations; passing them does not establish visual parity. Investigate differences; any unavoidable platform
rasterization difference must be narrowly described with evidence, not used to excuse layout or styling drift.

Complete both implementations in the same change. Record discovered pre-existing mismatches as linked issues with
paired evidence; resolve mismatches in the states touched by the change before declaring it complete. Historical notes
about deferred web work do not establish current parity or waive this requirement. Do not mark an unchecked comparison
as passed.

### Iterative UI work and user testing

During iteration use the smallest check that answers the current question; do not regenerate every preview after each
edit. The user may test the real app and supply screenshots or recordings; record the commit, environment, mode, accent,
scale, steps and result, and say which checks you ran and which the user reported. A screenshot proves only the state it
shows, not other modes, bindings, keyboard paths or accessibility. Mark missing checks pending, never passed.
Before completing a UI change run the applicable final checks once on the final code and regenerate artifacts whose
source changed. Documentation-only changes need no UI recording or regeneration.

### Implementation and documentation

Keep the fix in the shared implementation and avoid unrelated refactors. Add a focused regression check for behavior
changes. New controls belong in `src/Shade/Controls`. Checklist, all in the same change:

- [ ] Control in `src/Shade/Controls`, painted with role tokens, nullable-annotated, parameterless constructor.
- [ ] `docs/DESIGN_SYSTEM.md`, plus the token key, prose and *Choosing a component* row in `DESIGN.md`.
- [ ] `gallery/Showcase.cs`, with the relevant states.
- [ ] `src/Shade/PublicAPI.Unshipped.txt` and XAML binding coverage in `tests/Shade.Tests/Bound.axaml`.
- [ ] `docs/shade.css` and `docs/shade.js` when the control has a web version.
- [ ] A line in `CHANGELOG.md` at release.

Use role tokens; compute derived colours in `Restyle()` or `Render` so mode and accent switches work live. Values an app
changes or binds must be Avalonia properties, with two-way binding for inputs. Controls created in XAML need a
parameterless constructor. Keep nullable annotations accurate and use properties or item records for public data.
Labels follow the design system's writing and translation rules; button and menu actions do not end in dots.

Update `src/Shade/PublicAPI.Unshipped.txt` for public API changes. The analyzer can add missing entries:

```sh
dotnet format analyzers src/Shade/Shade.csproj --diagnostics RS0016 --severity info
```

Call out changes to shipped APIs as breaking in the commit and pull request. Follow the repository's 0.x version rule:
components and fixes advance the patch; breaking changes advance the minor. Add a `CHANGELOG.md` row for a release.
An explicitly requested minor release may also group non-breaking work; state that no public API break is implied.

## Checking a change

### Popup interaction coverage

`PopupContracts` in `tests/Shade.Tests/Program.cs` and the popup matrix in `gallery/page-check.mjs` exercise the same
families. Recheck the whole matrix when changing shared popup ownership; a fix to one picker does not prove the others.
`Menu.OwnPopup` owns native outside-click pass-through, optional trigger dismissal, focus leaving the owner/popup,
ancestor hide/disable, detach and subscription cleanup. The web equivalents are `watchPopup` and `ownList` in
`docs/shade.js`. Reused native popups register ownership on each opening and release their handlers on each closing.
Floating web lists also check actual anchor movement: a scroll event queued before opening must not immediately dismiss
the new list, while moving its anchor or resizing still dismisses it.

| Family | Trigger and selection contract | Focus and dismissal differences |
| --- | --- | --- |
| `ChoiceButton` and `Menu.Show` | Trigger toggles a picker; choosing one option closes it. Context menus open at the pointer. | Mouse opening has no keyboard ring; arrows reveal keyboard focus. Escape restores the anchor; Tab/Shift+Tab leave toward the host. Right-click context activation keeps its native semantics. |
| `MultiChoice` | Trigger toggles; checking an option keeps the list open. | Empty selection means all. Pointer focus has no keyboard ring; keyboard/typeahead selection and checkbox automation remain native. |
| `SearchChoice` | Trigger toggles; committing a result closes it. | Keep the editable search header focused. Filtering cannot change the committed object/ID; native ComboBox bindings remain the source of selection. Search resets on reopening. |
| `SuggestionField` | Editing opens matching suggestions; clicking the editor continues editing. | Preserve the editor text/caret. Escape dismisses; a suggestion pick alone commits. Read-only input dismisses suggestions. |
| `DateField` / `DateRangeField` | Clicking blocks opens the calendar or changes the active block; the calendar icon toggles it. | Preserve partial/invalid date drafts and range picking. Tab may move between blocks before leaving the field; keyboard calendar navigation keeps its own cursor. |
| `AccentPicker` | Focusing/clicking Custom opens the colour helper; clicking the editor keeps it open. | Colour changes apply live. Escape dismisses without reopening on focus restoration; native Tab traverses available helper/editor targets. |
| `ModeSwitch` | No popup; native radio segments apply the mode immediately. | Neutral outer frame; only keyboard focus on a segment has an accent ring. |

For each popup family, cover repeated trigger clicks, outside click action/focus, Escape, Tab/Shift+Tab, hide, disable,
detach and reattachment. Dismissal must not synthesize selection/edit events. Run native cases in both modes at 1x/2x
text scale; browser cases cover both modes and 1x/2x page zoom. Existing control-specific tests own bindings, commits,
search/typeahead, automation, invalid drafts and caret preservation. Keep those tests when moving shared mechanics.
Headless checks do not replace real Windows pointer testing or the paired visual checks above.
The browser matrix also writes `docs/preview/dark/multi-choice-web.png` and its light counterpart, using the native
multi-choice preview's content and viewport for paired inspection. Regenerate these with `node gallery/page-check.mjs`.

### Required checks

Run `python gallery/docs_check.py` for local Markdown file links, canonical guide references, package-document entries
and the README install version. It checks structure, not the meaning of every rule or external URLs. After packing, pass
`--package out/Shade.<version>.nupkg` to verify shipped Markdown contents too. Its regression checks run with
`python -m unittest discover -s gallery -p test_docs_check.py`.

Use the SDK selected by `global.json`. Run commands from the repository root:

```sh
dotnet format whitespace Shade.slnx
dotnet build Shade.slnx -c Release -warnaserror
dotnet run --project tests/Shade.Tests -c Release
```

For control, token or visual changes, regenerate the documentation and previews:

```sh
dotnet run --project gallery -c Release -- --docs .
```

Inspect the affected previews in both modes, including a live mode/accent switch, and commit the generated changes.
Write prose outside generated blocks; edit the page source in `gallery/landing.html`.
For a control's look or behavior change, update its web version too, inspect the page in both modes, and run:

```sh
npm install --no-save playwright@1.56.1
npx playwright install chromium
node gallery/page-check.mjs
```

Changes to `DESIGN.md` also need `npx @google/design.md@0.4.0 lint DESIGN.md`.
Every PR runs `ci.yml` (docs check, format, build, default tests). Heavy checks (extra test scenarios, package
contents, gallery regeneration, browser checks, design.md lint) are the manual `full.yml` workflow: run it once before a
release, not on every push. Documentation-only changes need link and diff review only.

## Opening and reviewing a pull request

Link the issue when one exists. Explain the trigger, resulting behavior and why the fix belongs in the changed code.
List the checks actually run and their results; state anything unverified. Include before/after images in both modes
for visual changes, and describe API compatibility or migration requirements when applicable.

When triaging an issue, reproduce it, record the affected versions and surface, and distinguish confirmed causes from
suspicions. Ask for the smallest missing detail needed to proceed. Link duplicates to the original report. Before
closing a fix, check the issue's acceptance criteria and link the pull request; distinguish a merged fix from a released one.
