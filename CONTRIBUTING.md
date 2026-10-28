# Contributing to Shade

This guide covers contributions and issue reports for Shade's Avalonia library and matching web components.

## Keeping the rules unified

Each topic has one owning document. Other documents summarize and link to it; do not maintain independent copies.

| Topic | Owning source |
| --- | --- |
| Principles, colour, type, space, status, page layout, writing, builder rules and button placement | `docs/DESIGN_SYSTEM.md` |
| How a component looks (sizes, colours, states) and which class to use | `DESIGN.md`, which ships to apps; never repeated in `DESIGN_SYSTEM.md` |
| How a component behaves and binds; supported API use, configuration, migration and limits | `docs/ADOPTION.md`, checked against the implementation and tests |
| Consuming-app agent rules and reporting/adoption loop | `docs/APP_AGENTS.md` |
| Shade contribution, refactor and verification workflow | This guide; `.github/workflows/ci.yml` (every PR) and `full.yml` (before a release) define the checks |
| Instructions specific to working in this repository | `AGENTS.md`; `CLAUDE.md` imports it |
| Installation and entry points | `README.md`; `src/Shade/PACKAGE.md` summarizes the package |
| Popup interaction matrix | Comments above `PopupContracts` in `tests/Shade.Tests`, with the tests |

The changelog describes historical scope. An open issue records a known gap, not a supported feature or an exception to
current rules. Old publishing instructions do not authorize a new release. An app follows the documentation of its
installed version.

When changing a contract, update its owning source and the affected summaries, examples, issue templates and tests in
the same change; add packaged-document entries and CI package checks for new shipped guidance. Remove obsolete
instructions rather than appending a contradictory rule. If sources disagree, reproduce the mismatch and resolve it
explicitly. Generated tokens stay generated: edit their sources and keep hand-written prose.

## Before starting

Read [AGENTS.md](AGENTS.md), [the design system](docs/DESIGN_SYSTEM.md) (especially section 13) and
[DESIGN.md](DESIGN.md); for app integration, [ADOPTION.md](docs/ADOPTION.md). Open gaps are GitHub issues. People and
coding agents follow the same contract.

Search existing issues first; keep one independently fixable problem per issue and link related reports. Small fixes and
documentation improvements can go straight to a pull request; propose new components and breaking API changes in an issue
first, with the use case and intended contract. App pages, routes, domain colours and business logic belong in the app.
If an app finds a shared bug or gap, follow [the app-agent workflow](docs/APP_AGENTS.md) before building a local substitute.

## Describing an issue

Use the bug report or component/improvement/refactor template. A good title names the component, trigger and result
(`ChoiceButton: Escape leaves keyboard focus outside the field`). State impact, a minimal reproduction, expected versus
actual behavior and the conditions you checked (mode, fresh versus live switch, pointer versus keyboard, desktop versus
web); mark unchecked cases unknown. Give observable acceptance criteria. A report needs no diagnosis; suspected causes are
code paths, never people, and stay apart from confirmed observations. Follow shared callers before proposing a local fix.

| Symptom | Places to inspect |
| --- | --- |
| Colour differs after a live switch | `Look`, role brushes, the control's `IRestyle` / `Restyle()` or `Render` path |
| Value or selection fails to update | Avalonia property registration, change notifications, binding mode, item identity |
| Keyboard or pointer action behaves differently | Native action targets, focus, popup lifecycle and event handling |
| Desktop and web disagree | The control, `docs/shade.css`, `docs/shade.js`, and their gallery examples |
| Generated docs differ from the control | Gallery export, generated blocks, `gallery/landing.html` |

Confirm a cause with a failing test, a minimal reproduction or a controlled comparison; timing of a suspected regression
commit is not proof.

## Making a change

### Native foundations

Improve existing components before adding another family. Shade owns the visual language; Avalonia supplies control
semantics. In order: reuse the Shade component or helper; derive from or compose the native Avalonia control and apply
Shade's theme (`ActionButton : Button`, `SearchChoice : ComboBox`), reusing its content, commands, selection, editing,
focus and automation; evaluate installed Avalonia libraries before adding one (a new dependency names the gap, why
existing tools fall short, and compatibility, maintenance, licence, transitive and package cost, and fit with Shade's
visuals); implement only the missing behavior, with focused input, accessibility and lifecycle checks.

Apps consume Shade's controls and the native Avalonia controls Shade themes (a subclass exists only for behavior or a concept Avalonia lacks); a native control Shade does not theme yet is not used in app views where a Shade control exists. A native
base must preserve Shade's artwork; do not let a framework or third-party theme change colours, geometry or states, and
do not import another design system. Application services, persistence, permissions and business validation stay in the host.

### Refactoring

A refactor needs a concrete reason: a reproduced defect, duplicated shared logic, missing binding/native semantics, an
accessibility or lifecycle gap, or a measured performance problem. Use the improvement template for substantial ones.

1. **Inventory the contract**: callers, public members, constructors, bindings, events, item identity, validation,
   translation, focus and popup behavior; the web counterpart, gallery states, tests and adoption notes.
2. **Capture a baseline**: reproduce the problem and capture C# and web states in both modes; record what must stay
   compatible. Measure performance on the same data before and after.
3. **Choose the smallest migration**: one source of state and one activation path for pointer, keyboard, commands and
   automation. Keep adapters that can delegate; document unavoidable public breaks.
4. **Preserve active interaction**: keep editors, caret, focus, selection and invalid drafts across data, language and
   appearance updates. Own popup dismissal, subscriptions, timers and cancellation through hide, disable, detach and
   reattachment. Remove obsolete code once every caller uses the replacement.
5. **Verify**: add a regression for the changed behavior, exercise compiled XAML and application-style updates, complete
   the visual comparison below, and update the API listing, adoption notes, design docs and gallery.

Do not fold an API redesign into an internal cleanup; backlog limitations are not permission to change unrelated contracts.

### Bindings, data and validation

Use inherited Avalonia properties and routed events where they express the contract. New styleable values are
`StyledProperty`; directly owned state is a `DirectProperty` raised with `SetAndRaise`. Inputs default to two-way
binding; internal user updates use `SetCurrentValue` so the host's binding survives. Test a real binding both ways
(initial value, model update, user edit, another model update). Programmatic updates notify bindings without user action
events; user actions emit the documented event once, on the same path as `Command`, disabled state and automation.

Document snapshot versus observable collections, item identity, ordering, empty/loading behavior and reconciliation.
Update UI state on the UI thread and detach old subscriptions when sources change. Use native validation with an
explanatory message; preserve invalid drafts without committing them and define commit, cancel and discard. Business
rules and the decision to save stay with the app. Extend `Bound.axaml` and a focused behavior test for changed bindable APIs.

### Translation and culture

Follow [ADOPTION.md](docs/ADOPTION.md)'s translation contract. Route every built-in visible string, tooltip, validation
message and automation name through Shade's localization path, as English templates with arguments (no concatenated
fragments); `Look.Localize` wins over `Look.Translate`, and a missing key falls back to the English text. Support
`Look.RefreshLanguage()` without losing bindings, input, drafts, caret or focus and without edit events. App-provided
titles, options and placeholders stay app-owned; keys stay separate from display text, so a language change never changes
selection identity or stored values. Use `Look.Culture` for built-in language behavior and an explicit input culture or
parser for numeric input. Check longer translations, count templates, calendar and automation names and decimal-comma
input. Full RTL layout and culture-driven date input remain documented limitations.

### C# and web visual parity

The C# and web versions must look identical in equivalent states at every release: same tokens, typography, icons,
spacing, borders and state treatments, and the same behavior (selection, validation, focus indication, popup dismissal)
in each platform's native semantics. Native foundations are an implementation choice and do not change the appearance.
C# is the source of truth between releases: a PR that changes a control's look or behavior lists it under **Web sync** in
its description and leaves the web port alone; the port is brought level at the minor release (*Releasing* below).

Compare the gallery with the web component side by side: same content, mode, accent, viewport and scale, across default,
hover, pressed, keyboard focus, selected, disabled, empty, invalid, busy and open popup, long or localized text, larger
text and density, fresh construction and live mode, accent and language switches. Include paired C#/web screenshots in the
PR (at the Web sync step) for affected states in both modes, and inspect clipping, wrapping, popup geometry and scrollbars.
Passing tests does not establish parity; describe any unavoidable rasterization difference narrowly, with evidence. File
pre-existing mismatches as linked issues with paired evidence, and never mark an unchecked comparison passed.

### Iterative UI work

Use the smallest check that answers the current question; do not regenerate every preview per edit. The user may test the
real app and supply screenshots: record the commit, environment, mode, accent, scale, steps and result, and say which
checks you ran and which the user reported. A screenshot proves only the state it shows; mark missing checks pending.
Before completing a UI change run the applicable final checks once on the final code and regenerate artifacts whose
source changed. Documentation-only changes need no UI recording or regeneration.

### Implementation and documentation

Fix in the shared implementation; avoid unrelated refactors; add a focused regression check. New controls, all in one change:

- [ ] Control in `src/Shade/Controls`, painted with role tokens, nullable-annotated, parameterless constructor.
- [ ] Open through public API: content/template slots, properties for placement, order and visibility of its parts,
      events or commands, public tokens ([DESIGN_SYSTEM.md §1](docs/DESIGN_SYSTEM.md#1-principles) *A stable look, not a fixed layout*).
- [ ] The token key, prose and *Choosing a component* row in `DESIGN.md` (look); the behaviour in `docs/ADOPTION.md`; `docs/DESIGN_SYSTEM.md` only when a principle or placement rule changes.
- [ ] `gallery/Showcase.cs` with the relevant states; `src/Shade/PublicAPI.Unshipped.txt` and `tests/Shade.Tests/Bound.axaml`.
- [ ] A line under **Web sync** in the PR if the control has a web version (`docs/shade.css`, `docs/shade.js`, `gallery/landing.html` follow at the release); a `CHANGELOG.md` line at release.

Use role tokens; a colour made from roles is a derived `Tone` in `Look.MakeAccent`, one made per instance is worked out in `Restyle()` or `Render`, so mode and accent switches work live. Values an app
changes or binds are Avalonia properties; keep nullable annotations accurate and use properties or item records for
public data. Labels follow the design system's writing and translation rules; button and menu actions do not end in dots.
Update `PublicAPI.Unshipped.txt` for public API changes (`dotnet format analyzers src/Shade/Shade.csproj --diagnostics RS0016 --severity info`
adds missing entries) and call out changes to shipped APIs as breaking. Releases follow the 0.x rule in `AGENTS.md`; add a
`CHANGELOG.md` row, and publish by running `publish.yml` with the version (it tests, packs, tags and publishes).

## Checking a change

When changing shared popup ownership, recheck every popup family with `PopupContracts` and the matrix in
`gallery/page-check.mjs`; a fix to one picker does not prove the others. The family matrix and its rules are comments
above `PopupContracts` in `tests/Shade.Tests`. Headless checks do not replace real Windows pointer testing or the paired visual checks.

Run from the repository root, with the SDK from `global.json`:

```sh
python gallery/docs_check.py                          # links, canonical guide references, package documents, README install version
dotnet format whitespace Shade.slnx
dotnet build Shade.slnx -c Release -warnaserror
dotnet run --project tests/Shade.Tests -c Release
dotnet run --project gallery -c Release -- --docs .   # control, token or visual changes: previews, DESIGN.md, the page
```

`docs_check.py` takes `--package out/Shade.<version>.nupkg` to verify shipped Markdown after packing; its own tests run with
`python -m unittest discover -s gallery -p test_docs_check.py`. The `--docs` run also checks text contrast in both modes and
that a live switch paints what a fresh build paints; inspect the affected previews in both modes and commit the generated
changes. The web port is checked at the release (*Releasing*):
`npm install --no-save playwright@1.56.1 && npx playwright install chromium && node gallery/page-check.mjs`
(the matrix also writes `docs/preview/*/multi-choice-web.png` for paired inspection). Changes to `DESIGN.md` also need
`npx @google/design.md@0.4.0 lint DESIGN.md`. Every PR runs `ci.yml` (docs check, format, build, default tests); the heavy
checks are the manual `full.yml`, run once before a release. Documentation-only changes need link and diff review only.

## Releasing

In order: scope PRs merged and `ci.yml` green on main; `dotnet run --project gallery -c Release -- --docs .` and a look at the
changed `docs/preview` images in both modes (the previews are the pixel baseline a migrated control must reproduce, or the PR
names each intended difference); **Web sync**: update `docs/shade.css`, `docs/shade.js` and `gallery/landing.html` for every
control listed under Web sync in the PRs since the last release, run `node gallery/page-check.mjs`, compare the page with the
previews in both modes, run `npx @google/design.md@0.4.0 lint DESIGN.md`; one Narrator pass over the gallery (check box, toggle switch, progress bar, date field, colour grid, state toggle); the manual `full.yml`
on main; the release PR (`<Version>` in `src/Shade/Shade.csproj`, the CHANGELOG row with a migration note for a break, the README
install version, `PublicAPI.Unshipped.txt` moved to `Shipped`, a `--docs` run); then `publish.yml` with the version (it tests, packs,
tags and publishes). Afterwards check the issues the PRs closed and tell the `from-app` reporters.

## Opening and reviewing a pull request

Link the issue. Explain the trigger, resulting behavior and why the fix belongs in the changed code. List the checks
actually run and their results, and state anything unverified. Include before/after images in both modes for visual
changes and describe API compatibility or migration needs. When triaging, reproduce the issue, record versions and
surface, distinguish confirmed causes from suspicions, ask for the smallest missing detail and link duplicates. Before
closing, check the acceptance criteria and link the PR; distinguish a merged fix from a released one.
