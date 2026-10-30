# Paper forms performance

Current state of [issue #115](https://github.com/jurasekdominik/shade/issues/115) on `perf/paper-forms`. An
implementation record, not an API contract. Human acceptance steps are in [PAPER_FORMS_ACCEPTANCE.md](PAPER_FORMS_ACCEPTANCE.md).

## How it works now

- **Every mode draws the page.** `PaperPainter` records the artwork of the native controls (so typography, borders,
  character shaping and decorations are the native ones) and replays it in bands of at most 64 structures per page, so
  a field edit redraws one band. Custom `PaperStructureTemplate` kinds stay live controls, stacked in order between bands.
  (The plan said one visual per page; bands keep a 5,000-field edit cheap. A 5,000-field form is about 1,100 visuals.)
- **Fill and Preview put one pooled native editor over the focused field** (`PaperFormView.Editing.cs`): pointer press,
  Tab or an automation request activates it, binds it to the field's `PaperFormValue`, and forwards the click so the caret
  lands where the user clicked. Caret, selection, undo, IME and the date calendar are the native ones. Leaving the field
  returns the editor to the pool. Preview's editors are read-only.
- **Accessibility:** `PaperFormView.Automation.cs` exposes one virtual peer per field (role, name, value, read-only,
  error, screen bounds). Setting a value, toggling or focusing a peer activates the same editor.
- **Edits are incremental:** unchanged structures stay the same objects, so undo keeps only the changed path; validation
  runs once per edit and is cached by structure reference; missing values are added in one batch; problems publish only
  when they differ and are checked per changed page; one designer update per operation; toolbar, inspector and layer
  rows survive an edit; the grid and rulers are a static layer; wheel zoom is a transient render scale committed after
  150 ms; HTML character grids redraw only the selected grid, once per animation frame; PDF/PNG export uses one session.
- The native renderer is still reachable as an internal switch (`UsePainter(false)`). It is the oracle for the
  pixel comparisons, not a supported mode.

## Results (Windows, headless Release, `--paper-scale`)

Issue baseline (0.12.0) → now. Each operation drains its UI jobs; samples are single runs on one machine.

| Case | Load | Inspector edit | Move | Keystroke (Design) |
| --- | --- | --- | --- | --- |
| 1,000 × 5 | 8–12 s → 1.3 s | 125 ms → 23 ms | ~110 ms → 25 ms | 13–15 ms → 1.4 ms |
| 500 × 10 | 5.5 s → 0.4 s | 86 ms → 16 ms | 100 ms → 12 ms | 9 ms → 0.7 ms |

| Fill and Preview | Design → Fill | Design → Preview | `PaperFormView` first load in Fill | Keystroke in Fill | Visuals |
| --- | --- | --- | --- | --- | --- |
| before (native inputs) 1,000 × 5 | 8.6 s | | | | 108,921 |
| now, 1,000 × 5 | 0.29 s | 0.23 s | 0.37 s | 0.7–1.0 ms | 107 for the view |
| now, 500 × 10 | 0.13 s | 0.11 s | 0.18 s | 0.8 ms | 99 for the view |

- Undo: 100 resizes of one field on a 1,000-structure page share every unchanged structure (5,099 distinct structures
  for 5,099 expected). The changed path costs about 8.3 KiB per entry (heap dump), against about 390 KB before. The whole
  view keeps under 1 MiB over those 100 resizes; the scale check fails above 8 MiB.
- Skia CPU drawing of the builder on 1,000 × 5 (`--paper-frames`, input + layout + a synchronous bitmap draw, not
  display pacing): drag median 79 ms, p95 150 ms; Ctrl+wheel zoom median 75 ms, p95 88 ms (was drag 130/247, zoom 150/157).

## Budgets that fail the build (`--paper-scale`, in `full.yml`)

Load, Design → Fill, Design → Preview and a Fill form's first load ≤ 1.5 s; inspector edit and move ≤ 30 ms; typing in
Design and in Fill ≤ 5 ms; at most one designer update per operation; only changed structures retained across 100
resizes; those resizes keep ≤ 8 MiB. `--paper-frames` fails above 120 ms median or 250 ms p95 (a regression guard with
headroom over the baseline above).

## Gates

| Gate | Command | Covers |
| --- | --- | --- |
| Default suite | `dotnet run --project tests/Shade.Tests -c Release` | includes the focused performance, pooled editing and automation checks; native-input tests activate the pooled editor |
| Painter parity | `-- --paper-parity`, `-- --paper-parity filled --dpi` | every built-in kind × state × mode × zoom (840 cases), decorated fields at 125–200 % DPI |
| Handover | `-- --paper-handover`, `-- --paper-handover --dpi` | no shift or flicker when focus moves between drawn and live (384 + 1,152 cases) |
| Export parity | `-- --paper-export-parity` | native vs exported output, including custom controls |
| Windows UIA | `gallery/paper-uia-check.ps1` with `--paper-gates` running | real provider: role/name, screen bounds, SetValue, Toggle, error, offscreen, read-only, theme switch (10 checks) |

Pixel comparisons allow at most 2/255 per channel in flat areas and 64/255 only at an anti-aliased edge (Avalonia's
retained geometry covers corners and lines slightly differently from the live control). Layout or colour differences fail.

## Open (cannot be observed by the automated gates)

Narrator speech, hardware IME composition and candidate placement, GPU presentation and real frame pacing. The steps are
in [PAPER_FORMS_ACCEPTANCE.md](PAPER_FORMS_ACCEPTANCE.md); record the result in the pull request before a release.

## Web sync

`PaperFormView` and `PaperFormBuilder`: Fill and Preview are drawn with one pooled editor (no look change; the pixel
comparisons above). The generated editable HTML redraws character grids differently (scheduling and canvas storage; no
look change). The hand-written web port in `docs/shade.css` and `docs/shade.js` is unchanged; it is brought in line at the
minor release (CONTRIBUTING.md, *Releasing*). The only public API addition is the `OnCreateAutomationPeer` override.
