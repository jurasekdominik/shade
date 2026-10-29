# Paper forms performance work

Tracking [issue #115](https://github.com/jurasekdominik/shade/issues/115) on
`perf/paper-forms`. This is an implementation and review log, not a supported API contract.
Started 2026-10-29 from `origin/main` at `6b7666b`.

## Review status

Work in progress. No rendering replacement is accepted until the parity and accessibility gates below pass.
Existing local changes to `DESIGN.md`, `docs/index.html` and `inspection.pdf` predate this work and are excluded from commits.
No public API break or release is authorized by this work.

| Issue item | Current implementation status |
| --- | --- |
| Phase 0: large cases, update counts, failing budgets, undo report | Implemented; strict budgets still fail |
| Phase 0: painted drag/zoom | Skia CPU drawing probe implemented; desktop compositor/GPU pacing pending |
| Phase 1: shared objects | Resize, move, replace, delete, snapping and normalization implemented; layer reorder still to audit |
| Phase 1: validate once / missing values | Implemented |
| Phase 1: problem publication, indexing, cache and sweep | Implemented with regression checks |
| Phase 1: one designer pass | Property edits and moves verified; remaining commands and narrower section dirty flags pending |
| Phase 1: retained controls | Toolbar implemented; inspector reuse outside its existing live-edit path pending |
| Phase 1: reference diff / date formats | Edit-path identity, view comparison fast path and date-format memoisation implemented; page skipping, validation-site cache and layer row diff pending |
| Phase 1: static drawing, snap cache, transient zoom | Solution documented; implementation pending |
| Phase 1: HTML redraw | Implemented; browser checks cover coalescing and unchanged canvas storage |
| Phase 1: PDF/PNG session reuse | Solution documented; implementation pending |
| Phases 2 and 3 | Solutions and acceptance gates documented; implementation pending |

## Solutions and acceptance, in issue order

### Phase 0: measurement

- Extend `--paper-scale` to run both 1,000 × 5 and 500 × 10; count actual designer flushes for each operation, including
  synchronous callbacks. Record cold load separately from repeated edit/move/type timings.
- Enforce load ≤ 1,500 ms, edit/move ≤ 30 ms, typing ≤ 5 ms and at most one designer flush per operation. Wire the large
  cases into `full.yml`. Record failures honestly while the rendering phases are outstanding; do not relax the budgets.
- Add a Skia-rendered drag/zoom measurement separate from headless logic timings. Measure actual frames, not just event
  handler duration. Retain the same document, viewport and scale for comparisons.
- Measure retained undo memory after 100 resizes with full GC and a live builder; also count distinct retained structure
  references so GC noise cannot conceal copying unchanged branches.

### Phase 1: incremental work, unchanged appearance

1. Preserve identity first: remove/reorder/normalize helpers return the original structure and child list when unchanged.
   Skip pages without selected roots. Test nested groups, reparenting, ordering, undo/redo and unchanged page references.
2. Validate once: remember successfully validated immutable instances with weak keys; keep cross-document ID/key checks.
   Invalid definitions must still throw through both direct property assignment and edits.
3. Batch missing values: build one key set, collect all missing values, then publish one collection change. Keep duplicate
   key rejection and observable collection behavior for application-owned values.
4. Problems: compare published sequences; index sites by field key, cache local checks by structure reference plus text,
   and sweep sorted sibling bounds for overlaps. Preserve problem order, localization and global collision checks.
5. Designer updates: dirty flags for layers/toolbar/inspector/feedback/surfaces, nested synchronous edit scopes, one flush
   at the outer boundary. Property assignment and value callbacks need the same path; test counts before draining jobs.
6. Retain controls: construct toolbar choices once and refresh guarded values/items. Reuse inspector inputs when selection
   identity and kind agree; callbacks must read current selection instead of captured stale records. Check open popups,
   focus, invalid drafts and bindings after host updates.
7. Reference diff: skip shared page lists and structure instances in view reconciliation, validation and layers. Cache
   date-format parsing by string without retaining old documents. Normalize only changed paths.
8. Static design drawing: separate grid/rulers/margins from interaction overlay. Cache ruler text by culture/scale and
   invalidate on geometry, theme and language changes. Snapshot snap candidates at drag start; release on cancel/detach.
9. Wheel zoom: transient render scale, one layout commit after 150 ms idle. Preserve pointer anchor and scroll extent;
   flush/cancel the timer on detach, mode changes and export. Test repeated wheel input and programmatic zoom separately.
10. HTML export: track previously/currently selected grids, queue a Set of redraws through one animation frame, and only
    reset backing storage when size/DPR changes. Resize/theme redraw all. Browser checks cover caret and selection moves.
11. PDF/PNG: one reusable export session with one validated snapshot and one hidden view/window; move PageIndex between
    pages. Preserve custom templates, vector PDF output, resource disposal and staged writes on failure.

### Phase 2: drawn Design and Preview

- Extract built-in paint geometry from existing controls into an internal `PaperPainter`; reuse typography, character
  shaping and decorations rather than approximate them. One page visual; custom templates remain native child controls.
- Feed the same painter to PDF/PNG; remove the hidden window only after custom controls still have a supported rendering
  path. Preserve images, vector text, clipping and export failure behavior.
- Gate activation on live/drawn image comparisons for every built-in kind, empty/filled/overflow/error, alignments and
  cell decorations, both modes and multiple zooms. Include fresh/live appearance changes in `--docs`.

### Phase 3: Fill with a live editor

- Use one pooled editor per native kind, with only the active editor attached. Bind the current value with normal native
  semantics and forward the original pointer coordinates for caret placement; keep IME and date popup ownership native.
- Draw hover/error states and tab through a flattened ordered field index across pages. Automation focus/value changes
  use the same activation path as pointer and keyboard. Test editor disposal, rebinding and hiding while a popup is open.
- Supply virtual field peers with role/name/value/read-only/error; focusing or setting through a peer activates its editor.
  Run UIA and Narrator on Windows. Narrator wording cannot be established by headless tests.
- Keep caret, selection and undo with the active native editor; explicitly verify current unfocused selection behavior.
  Compare live/drawn handover pixels and record pointer, keyboard, IME and Narrator evidence before acceptance.

## Work and evidence

### Continued implementation: retained inspector, layers and static drawing

- User requested completion of every remaining phase; this branch remains active through the painter/editor gates.
- Layer reordering now shares untouched groups/pages. Add/remove-page, undo/redo, insertion and clipboard edits have
  outer synchronous update scopes; new regressions verify one flush for reorder, undo/redo and page commands.
- Inspector inputs, choices, font/color helpers and flags refresh in place when their layout remains compatible.
  Callbacks read the current selection/page, guarded refreshes do not edit the document, and invalid drafts survive
  host updates. The focused invalid-draft and retained-layer-source/row tests pass.
- Layers now reconcile an observable list with replace/move/insert/remove notifications instead of replacing its source.
  Row objects are cached by structure identity, placement and problems; zoom/selection skip rebuilding row data.
- Successful structure validation uses weak identity keys while document-wide identity/key/depth checks still run.
- Grid and rulers have retained static visuals, guides a separate dynamic visual, and selection its existing overlay.
  Their stacking preserves the original rulers → guides → grid → selection order. Ruler labels are cached, and snap
  candidates are captured once at drag start and released when pointer capture ends.
- `--paper-form` passed after the implementation; all 26 `--paper-performance` checks pass. Full visual generation is
  deferred to final code per the contribution workflow; transient wheel zoom is the next change.

- Read issue body and audit comment, repository contract, design-system rules and contribution methodology.
- Confirmed `PlaceSelected` clones every unrelated leaf in both removal and ordering; `MoveSelection` also clones
  untouched groups and pages. Identity preservation is the first implementation step as required by the issue.
- Baseline and implementation results will be appended with the commands and actual outcomes.

### First implementation: shared edit paths

- Added copy-on-change mapping for child lists and retained unchanged branches in resize/reparent, move, property edits,
  deletion and grid normalization. A move skips pages with no selected roots. This is necessary before caching by identity.
- Added regressions for nested sibling/group/page identity, repeated resizes, moving out of a group, and undo/redo.
  `dotnet run --project tests/Shade.Tests -c Release -- --paper-performance`: all seven checks passed.
- Baseline, Windows headless Release, 1,000 × 5 on the starting code: load 8,553 ms; inspector edit 174 ms;
  move 113 ms; keystroke 13 ms; full problems 23 ms; 108,506 visuals. Existing loose budgets passed this slow run.
- Remaining undo cost includes the page's flat array of references; structures off the changed path are shared. This
  change removes retained copies of each unrelated structure, not the unavoidable replacement page/list snapshots.

### Validation, values and problems

- Successful assignment validation is memoised with weak definition keys. `Edit` validates before history changes and
  the property's callback reuses that result; public `Validate()` still explicitly checks a snapshot. Collections must
  remain immutable as documented. Date-format results use weak string keys, so old documents/formats are not rooted.
- Default values use Avalonia's existing range-capable list. Missing keys are collected once and added as a range;
  application-owned lists retain their identity and notification semantics. Internal lookup/subscription rebuilds happen
  once after the batch, including for application-owned ObservableCollection instances.
- Validation sites are indexed by field key. Cached checks include structure identity, value text/error, absolute area,
  container and overlapping neighbour identities, because parent movement and neighbour renaming also affect messages.
  Sibling intersections use an X sweep, retaining the original document/pair order. Dense all-overlapping layouts still
  necessarily produce quadratic numbers of warnings.
- Identical problem sequences keep the existing list and emit no change notification. Explicit `RefreshProblems()`
  invalidates the cache, including on language refresh. Added focused error/recovery, observable-value and invalid-edit
  tests; all 14 focused checks passed before the sweep-specific regression was added.

### Synchronous updates and stricter measurement

- Added nested update scopes around property callbacks, selection, edits and moves. Toolbar/inspector rebuild requests
  coalesce with the pending designer pass and flush before the outer operation returns. Actual flushes have an internal
  counter (no public API change). Focused checks assert exactly one immediate pass for a property edit and a move.
- Toolbar choices now retain their controls, refresh guarded state, and change item sources only when captions differ.
  A focused check keeps an unrelated dropdown open across zoom and definition changes. Palette changes can still rebuild
  the toolbar. Full inspector reuse and narrower dirty flags for individual designer sections remain pending.
- `--paper-scale` without dimensions runs 1,000 × 5 and 500 × 10; `full.yml` uses it. Budgets now fail on the issue's actual
  thresholds, including load, update counts and deterministic identity counts across 100 resize snapshots. Both cases run
  even when the first fails. Memory figures include UI/history retention, so they are not pure structure allocation.
- First instrumented 1,000 × 5 run, before retaining toolbar controls: load 13,207 ms; edit 366.13 ms / 1 update;
  move 152.69 ms / 1 update; keystroke 2.67 ms / 0 updates. **Budget failed.** 100 resizes retained 19,875 KiB of managed
  heap, with exactly 5,099 distinct structures across snapshots (5,000 original + 99 replacements). Further UI/history
  retention needs investigation; do not interpret identity sharing as a complete undo-memory fix.
- Timing methodology now drains UI jobs after each individual edit/move, instead of after a batch of ten as the original
  benchmark did. The baseline's edit/move numbers therefore are not directly comparable. Run the same instrumented
  harness against the starting code for a valid before/after comparison.
- All 19 focused performance regressions passed after toolbar retention. Large-form budgets are expected to remain red
  until the remaining Phase 1 and drawn-page work land; the release gate is deliberately not weakened.

### Skia drawing probe and HTML export

- Added `--paper-frames` to the tests and `full.yml`. It uses Avalonia's real Skia renderer with a synthetic window platform
  and times pointer drag / Ctrl+wheel input, layout and synchronous bitmap drawing on the same 1,000 × 5 document.
  Twenty frames each, 1,400 × 1,000 DIPs at 96 DPI: drag median 129.87 / p95 247.40 / max 259.72 ms; zoom median
  149.92 / p95 157.13 / max 169.67 ms. The check verifies that the drag committed once and wheel zoom actually changed.
  `out/paper-frames.png` is a local diagnostic. This does not measure OS compositor/present latency or real GPU pacing.
- HTML character grids now queue only the old/current selected grid through a shared animation-frame Set. Resize, DPR,
  font readiness and root/body theme changes queue all grids. Canvas storage changes only when dimensions change;
  each draw resets its transform without accumulating scale. Print/save validation keeps its synchronous full check.
- Browser regressions in `gallery/paper-form-check.py` verify ten same-frame selection requests draw one canvas once
  without resizing storage, and that resize still draws all canvases. Existing editing, print overflow and save/reopen
  tests passed in dark and light modes; PDF physical sizes and vector text checks also passed.

### Final checks for this initial implementation

- `dotnet format whitespace Shade.slnx`: completed; unrelated files with only line-ending changes restored afterwards.
- `dotnet build Shade.slnx -c Release -warnaserror`: passed, zero warnings/errors.
- `dotnet run --project tests/Shade.Tests -c Release --no-build`: all passed, including the 19 performance regressions.
- `python gallery/docs_check.py`: passed.
- `dotnet run --project gallery -c Release -- --paper-form-previews out/paper-form`, then
  `python gallery/paper-form-check.py out/paper-form`: passed (both HTML modes, canvas scheduling/storage, PDF vector text
  and physical sizes, multipage PDF, input/undo/save/reopen/overflow checks).
- `dotnet run --project gallery -c Release --no-build -- --docs .`: passed, including fresh/live appearance checks.
  All preview image bytes match the committed baseline; inspected `09-paper-forms.png` in both modes. Generated
  `docs/paper-form.html` now contains the new export scheduler. Preserved the pre-existing Markdown/page files.
- Final `--paper-scale` run on this initial implementation (each operation drains its own UI jobs):

  | Case | Load ms | Edit ms / updates | Move ms / updates | Type ms / updates | Heap after 100 resizes | Distinct structures |
  | --- | ---: | ---: | ---: | ---: | ---: | ---: |
  | 1,000 × 5 | 11,821.83 | 298.36 / 1 | 169.16 / 1 | 8.14 / 0 | 11,740.2 KiB | 5,099 |
  | 500 × 10 | 8,766.26 | 141.87 / 1 | 104.57 / 1 | 1.92 / 0 | 10,207.8 KiB | 5,099 |

  **Failed the strict timing gate**, exit 1; both cases executed. Update counts and structure sharing passed. Full
  rendering trees are still created (108,517 / 108,532 visuals). Toolbar retention adds hidden action controls; this
  small increase in visual count is expected. Do not claim an overall speedup from these measurements.

Next implementation work: audit layer-reorder identity and command-wide scopes, finish inspector/layer/page diffing,
then static drawing and transient zoom. Investigate remaining heap retention with a profiler. Implement the painter
and pooled editor only with the pixel, keyboard, IME and automation gates above. Real desktop frame pacing, Narrator
and hardware IME have not been exercised. No release or full remote workflow was run.

## Web sync

`PaperFormBuilder`: toolbar controls survive definition/zoom updates; property edits and moves coalesce designer passes.
`PaperFormView`: identical problems no longer publish; default value-list implementation supports range changes.
Editable HTML export: character-grid redraw scheduling and canvas-storage reuse changed, with unchanged appearance.
The export is generated by C# and belongs to this work. The hand-written web port stays at its release baseline per
`CONTRIBUTING.md`; synchronize the applicable behaviors at the minor release.
