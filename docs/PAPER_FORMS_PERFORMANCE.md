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
| Phase 0: large cases, update counts, failing budgets, undo report | Both large cases pass unchanged budgets; heap roots inspected (latest results below) |
| Phase 0: painted drag/zoom | Skia CPU probe and native Windows callback recorder implemented; GPU presentation evidence pending |
| Phase 1: shared objects | Resize, move, replace, delete, snapping, normalization and layer reorder implemented |
| Phase 1: validate once / missing values | Implemented |
| Phase 1: problem publication, indexing, cache and sweep | Implemented with regression checks |
| Phase 1: one designer pass | Property edits, moves, reorder, undo/redo and page commands verified; toolbar/inspector flush flags and retained section caches avoid rebuilding controls |
| Phase 1: retained controls | Toolbar, compatible inspector inputs and layer rows implemented; focused invalid drafts retained |
| Phase 1: reference diff / date formats | Shared edit paths, validation-site cache, date-format memoisation, per-page layer/problem reuse and unchanged-page drawing/validation preparation implemented |
| Phase 1: static drawing, snap cache, transient zoom | Implemented; wheel burst, idle commit and pointer anchor regressions pass |
| Phase 1: HTML redraw | Implemented; browser checks cover coalescing and unchanged canvas storage |
| Phase 1: PDF/PNG session reuse | Reusable detached scene for built-ins; one hidden window for custom templates; native lifecycle, multipage vector text and failure cleanup verified |
| Phase 2: drawn Design/Preview | Design enabled; Preview remains native to preserve selection/focus; painter comparisons cover shared artwork, nested groups and custom stacking |
| Phase 3: pooled Fill editor | Implemented behind internal painter override; native undo/preedit, handover and Windows UIA checks pass; Narrator/hardware IME acceptance pending |

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

### Transient zoom

- Added a render-scale host whose desired extent follows wheel zoom without remeasuring its native editor descendants.
  After 150 ms idle, one layout transform commits the new scale. Programmatic zoom remains immediate, and mode changes,
  document replacement and detach stop/commit the pending wheel gesture.
- The scrollable-page anchor regression stays within one pixel; burst input makes zero layout commits, idle makes one,
  and changing mode commits synchronously. The headless test pumps a real dispatcher loop for the idle timer.
- The existing paper-form suite and 30 focused performance checks pass. Painter work follows; final pixel checks will
  also cover the new scale host. A pointer near a non-scrollable edge remains subject to the viewport's normal limits.

### Painter foundation and first parity pass

- Added internal `PaperPainter` and a page visual, still opt-in through the test seam while parity is expanded.
  The painter reuses detached native prototypes (one per built-in kind), records Avalonia vector drawings, owns cloned
  glyph runs, and replays the result without retaining native controls for each field. Images remain image draws,
  including SVG vectors. Existing native templates remain the source of typography, borders and character shaping.
- Rejected the initial SKPicture prototype: Avalonia's `DrawingContextHelper.WrapSkiaCanvas` disables subpixel text
  rendering, which failed the live/drawn pixel check. Retained Avalonia drawings keep the live text renderer instead.
- Detached ScrollViewer prototypes use a native ScrollContentPresenter directly: the normal template's deferred
  scrollbar setup depends on visual attachment. The inactive viewport has native clipping/layout without live chrome;
  the actual editor will retain the full native ScrollViewer and popup behavior.
- First gate: all 60 comparisons (ten built-in kinds × empty/filled/error × dark/light) pass. The comparison allows
  2/255 channel difference in flat areas and 64/255 only at an existing antialiased edge; this accommodates the different
  corner/line coverage of Avalonia's retained GeometryDrawing. Interior colors, glyph placement and clipping still must
  match. A discovered error-viewport mismatch was fixed before acceptance, not masked by the edge allowance.
- Pending before enabling: overflow, alignments, valid images, nested groups, custom-control stacking, page numbers,
  several zooms and live appearance switches. Export and Fill editor pooling follow that gate.

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

The sections below record subsequent implementation. The current remaining work is listed in the latest continuation.

## Web sync

`PaperFormBuilder`: toolbar controls survive definition/zoom updates; property edits and moves coalesce designer passes.
`PaperFormView`: identical problems no longer publish; default value-list implementation supports range changes.
Editable HTML export: character-grid redraw scheduling and canvas-storage reuse changed, with unchanged appearance.
The export is generated by C# and belongs to this work. The hand-written web port stays at its release baseline per
`CONTRIBUTING.md`; synchronize the applicable behaviors at the minor release.

### Painter parity and activation pass

- Full live/native comparisons pass: 840 kind/state/mode/zoom cases at 96 DPI. Decorated fields and page numbers also
  pass all 360 comparisons at 125%, 150% and 200% layout DPI. The gate permits at most 2/255 in flat pixels and 64/255
  only at existing antialiased edges; layout and solid-color differences still fail.
- Fixed native rectangle clipping and title placement against the rounded page height. Glyph runs are owned by the
  retained drawings so recycling a native prototype cannot invalidate another field's text. Page numbers use the same
  recorded typography. Prototype controls share the source's actual DPI; production uses no private platform APIs.
- Enabled drawn built-ins in Design and Preview; Fill is still native pending pooled editor and virtual peer work.
  Native custom-template controls retain their stacking order between drawn ranges.
- First large run with painting enabled: load 3,802 ms, edit 356 ms, move 146 ms, type 82 ms. **Strict gate failed.**
  The reported visual count was taken after switching back to native Fill; it is not the Design-mode count. Fixing the
  measurement and excessive drawing invalidations next. Retained heap for 100 resizes fell to 880 KiB; identities 5,099.

### Continuation: page preparation, cache regressions and accurate measurements (2026-10-30)

- Continued from WIP commit `6e9d417` in a separate `paper-forms-perf` worktree on `perf/paper-forms`, preserving the
  original checkout's modified preview. That commit already contains shared artwork recordings, detached exports and
  experimental editor pooling; the latter is still disabled in production Fill.
- Drawing preparation now skips pages whose immutable structure lists are unchanged. Replacing a value collection,
  changing its entries or reattaching after detached value changes invalidates that cache. Appearance, language and
  painter recreation retain their full invalidation paths. A focused edit prepares five structures on its changed
  page and skips the unchanged second page; this is a deterministic work-count check, independent of timing noise.
- Added regression checks for artwork sharing across identities/positions, releasing one shared user, and recording a
  changed value. Design pages are checked to contain no native text editors per field. The scale report now captures
  Design and native Fill visual counts separately, before/after the mode switch.
- Corrected tests that accidentally inspected hidden painter prototypes as if they were page editors. Native identity,
  keyboard/automation read-only behavior and stacking run through the explicit native comparison path; drawing order
  is also checked against the flattened page layout. Production rendering defaults are unchanged.
- Validation: whitespace format, warning-as-error Release build, all 2,031 default checks (including 37 focused
  performance checks), docs check, all 840 live/drawn pixel comparisons, and `--docs .` fresh/live appearance checks
  passed. Regenerated and inspected `09-paper-forms.png` in dark and light; no other generated content changed.
  `--paper-form-previews out/paper-form` and `python gallery/paper-form-check.py out/paper-form` passed the existing
  PDF/vector/physical-size/multipage and HTML editing/undo/save/overflow checks in both modes.
- Final Windows headless Release run, each operation draining UI jobs, with the strict budgets unchanged:

  | Case | Load ms | Edit ms / updates | Move ms / updates | Type ms / updates | Design / native Fill visuals | Heap after 100 resizes | Distinct structures |
  | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
  | 1,000 × 5 | 1,646.42 | 64.62 / 1 | 45.28 / 1 | 3.49 / 0 | 1,007 / 108,921 | 857.6 KiB | 5,099 |
  | 500 × 10 | 464.47 | 31.43 / 1 | 28.82 / 1 | 0.54 / 0 | 1,037 / 108,951 | 4,051.7 KiB | 5,099 |

  **Both cases failed the strict timing gate**, exit 1. The first load is cold and the second runs in the same process;
  do not interpret their difference as a page-count speedup. Heap includes UI/history retention and is GC-sensitive.
  The initial continuation baseline for 1,000 × 5 was 1,616.31 / 70.18 / 57.06 / 9.58 ms (load/edit/move/type);
  these single runs do not establish a statistically reliable timing improvement.

The following continuation supersedes this remaining-work list.

### Continuation: retained drawing bands and native behavior guards (2026-10-30)

- Temporary method timings identified repeated native artwork recording, full layer reconciliation and validation as
  substantial costs. Artwork preparation fell from 631 ms to 240 ms in instrumented cold runs after sharing input bodies
  independently of their titles. These are diagnostic samples, not controlled end-to-end speedup claims; all temporary
  instrumentation was removed.
- Titled and untitled fields now share body drawings. Title glyphs and body glyphs have separate ownership; removing,
  renaming and clearing shared users have regressions. Filled pixel fixtures now include multiple fields with different
  titles, and the group fixture includes 70 children, a nested group, and a custom native control with overlapping
  fields before/after it.
- Drawing ranges are retained in bands of at most 64 structures. The two new regressions first failed on whole-page
  rendering, then passed: typing and title edits redraw one bounded band, retaining the unchanged bands. This refines
  the earlier one-visual-per-page plan: a 5,000-field Design surface has about 1,100 visuals including all designer chrome,
  while preserving the same field painting and custom-control order.
- Problem results and layer rows retain per-page identity. Layout edits recheck problems on changed pages and any queued
  values on other pages; explicit checks, value-list replacement and reattachment still check everything. Global ID/key
  validation is unchanged. The overlap sweep
  chooses the less crowded axis, retaining original warning order. Unique field keys index one validation site directly.
- The shared structure walker now avoids allocating arrays/iterators for every leaf: a measured traversal of 200 flat
  structures fell from 12,896 to 104 allocated bytes, protected by an allocation budget regression.
- Restored the documented native Preview behavior. The previous WIP default painted Preview but lost selectable text
  and focusable read-only checkboxes; its default-renderer regression was reproduced before the fix. Preview and Fill
  stay native until pooled editors/virtual peers preserve their interaction contracts. The painter remains explicitly
  enabled for static exports and internal Preview comparisons.
- Custom export templates now receive native attachment/layout in one hidden window per session, reused across pages;
  built-in-only exports remain detached. Skia checks verify PNG lifecycle, two-page PDF window reuse/disposal, vector
  text set during attachment, and a later-page factory failure leaving the destination untouched. PDF session cleanup
  happens before staged output is copied to the destination.
- Validation on final code: whitespace format, warning-as-error Release build, all 2,045 default checks (51 focused
  performance checks), 840 live/drawn pixel cases plus custom export lifecycle/failure checks, docs checks,
  `--docs .` fresh/live appearance checks, and existing PDF/HTML browser checks passed. The 360 filled cases also pass
  at 125%, 150% and 200% layout DPI, including the shared-body and nested/custom fixtures. Regenerated and inspected
  paper previews in both modes; no clipping or spacing differences were observed. `docs/ADOPTION.md` now describes the
  actual renderer modes, default value collection and custom export lifecycle.

Final Windows headless Release results, run separately from verification jobs, with unchanged budgets:

| Case | Load ms | Edit ms / updates | Move ms / updates | Type ms / updates | Design / native Fill visuals | Heap after 100 resizes | Distinct structures |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 × 5 | 1,278.49 | 34.98 / 1 | 27.24 / 1 | 1.72 / 0 | 1,082 / 108,921 | 4,581.1 KiB | 5,099 |
| 500 × 10 | 366.68 | 17.87 / 1 | 12.50 / 1 | 0.41 / 0 | 1,107 / 108,951 | 4,534.7 KiB | 5,099 |

The 500 × 10 case passes all budgets. The 1,000 × 5 case still fails inspector edit ≤ 30 ms; combined scale exit is 1.
The first load is cold and the second shares the process; these are individual runs, not statistical comparisons.
Managed heap includes UI/history retention and is not a pure undo-entry allocation measurement; its increase still needs
a heap profiler. Structure sharing remains deterministic and passes.

The final `--paper-frames` Skia CPU probe passed its drag/zoom assertions: drag median/p95/max 80.15/152.30/171.03 ms;
wheel zoom 80.90/102.89/114.69 ms. It includes input, layout and synchronous bitmap rendering, and does not establish
desktop compositor/GPU frame pacing.

Next work, in order:

1. Profile the remaining 1,000 × 5 inspector edit cost against the 30 ms budget, and inspect heap retention with a
   profiler. Keep the current thresholds; a single passing timing sample would not establish stable performance.
2. Extend custom-control export pixel comparisons beyond the lifecycle/vector-text/failure cases now covered.
3. Finish pooled editor pointer/keyboard/date-popup/undo/rebinding checks and virtual automation peers for Fill and
   selectable Preview. Keep their native default renderers until handover and interaction gates pass.
4. Record real desktop frame pacing, UIA/Narrator and hardware IME evidence. No release, push or remote full workflow
   was run; the changes remain local and uncommitted.

### Current continuation: remaining issue solutions

| Remaining step | Solution and acceptance |
| --- | --- |
| Inspector edit budget | Profile section work and allocations; retain the 30 ms threshold and rerun both large cases separately from other jobs. |
| Undo heap | Capture managed heap roots after 100 resizes; distinguish history arrays, painter recordings, bindings and deferred UI work. |
| Pooled Fill/Preview | One indexed field order, one active native editor host, owned/disposed bindings, native caret/undo/IME/popups; compare native/drawn handovers before activation. |
| Accessibility | Stable virtual field peers with value/toggle/read-only/error, bounds and offscreen state; native editor subtree while active; automation uses the same activation path. |
| Custom export parity | Compare attached native/custom scenes with export output; keep one hidden native window where attachment is required. |
| Automated gates | Run pooled editing and all painter comparisons from the full workflow; preserve the strict scale failure. |
| Windows evidence | Exercise a visible gallery through UIA; record desktop frame intervals and a reproducible IME/Narrator checklist. Keep unobserved human/hardware checks explicitly pending. |

- Pooled editing now has a cached field index, page-level hit testing across drawing bands, one active editor host and
  explicit binding disposal. Definition title updates retain the editor, caret and native undo. Native pointer capture
  is transferred during handover so the first checkbox click receives its release and toggles exactly once.
- `--paper-pool`: 20 focused checks pass, including pointer/caret parity, reuse without cross-writing values or errors,
  cross-page Tab and exit, read-only Preview selection, calendar ownership/cleanup, hide and detach. Production Fill
  and Preview remain native while accessibility and handover gates are completed.
- Added stable virtual automation peers and retained native input bindings. `--paper-pool` now passes 43 checks,
  including native undo, simulated IME preedit, style updates, custom controls and automation value/toggle/error paths.
- All 384 native/drawn handover cases pass at 96 DPI. Added full-workflow gates for handover, high-DPI parity and
  export comparison, with failure images uploaded as artifacts.
- All 24 native/export comparisons now pass at 96 and 144 raster DPI, including attached custom content, SVG and
  decorated character grids in both modes. Fixed an extra DPI transform after clipped drawing groups by restoring
  the logical transform for every replayed group. Existing antialiasing tolerance was unchanged.

- All 1,152 handover comparisons at 125%, 150% and 200% layout DPI pass. The default suite includes 43 pooled editing
  and automation checks; Windows provider checks separately pass all ten assertions, including real screen bounds.
  Desktop checks exposed missing virtual screen conversion: field peers now derive from the native control peer with
  the form as their coordinate owner, retaining virtual identity without per-field controls. Design chrome keeps its
  native automation peer alongside the virtual document fields.
- Layer captions/severity/tooltips update in place. The selected row and collection retain identity on a title edit,
  protected by a regression. Layers read the existing per-structure problem cache and skip collection reconciliation
  when row order is unchanged, removing repeated page problem lookups. Page grid definitions retain identity when the
  row/column count is unchanged.
- Both strict large cases pass in an isolated Windows headless Release run (`out/scale-indexed-layer.log`):

  | Case | Load ms | Edit ms / updates | Move ms / updates | Type ms / updates |
  | --- | ---: | ---: | ---: | ---: |
  | 1,000 × 5 | 1,345.96 | 25.46 / 1 | 23.23 / 1 | 2.03 / 0 |
  | 500 × 10 | 440.65 | 17.84 / 1 | 10.43 / 1 | 1.34 / 0 |

  Budgets remain 1,500/30/30/5 ms. This is a passing sample, not a statistical guarantee; rerun on final code alone.
- Captured .NET heap dumps before and after 100 resizes (`dotnet-dump` 10.0.750501, temporary instrumentation removed).
  SOS `dumpheap -live -stat` shows exactly 99 additional `PaperStructure` objects (19,800 bytes), 99 bounds (4,752),
  99 definitions (11,088), 99 pages (3,168), page arrays (6,336) and selection arrays (3,168). A flat 1,000-reference
  page array costs 8,024 bytes, so the changed snapshot path is about 8.3 KiB per retained entry, including that array;
  unchanged structures are shared. This replaces the earlier full-page structure copying, not the reference array.
  The extra live structure arrays total 850,584 bytes, including other layout/validation arrays. Most remaining live
  heap growth is rendering state: +1,988,712 bytes of server draw visuals, +1,023,264 readback data and +537,768 border
  visuals. `gcroot` traces these through the headless compositor/render loop and queued composition batches. The
  approximately 4.5 MiB whole-view GC delta must not be described as undo-entry memory.
- Added [desktop and human acceptance steps](PAPER_FORMS_ACCEPTANCE.md), a visible pooled fixture (`--paper-gates`),
  Windows UIA checker and native frame recorder. The frame checker verifies a committed drag and changed zoom, and
  rejects recordings where input was not exercised. Windows foreground restrictions can reject unattended focus;
  run with the fixture available to the interactive desktop. GPU presents, spoken Narrator output and hardware IME
  candidate placement remain explicit manual gates. No production Fill/Preview activation, release or push occurred.
- Accessibility lifetime checks now cover native screen conversion, removed-field identity through undo, and garbage
  collection of retired peers after replacing the document while the form stays alive. A single invisible coordinate
  anchor belongs to the document; there are no per-field coordinate controls. Returning to native rendering clears
  virtual tab stops and retires that anchor. The pooled/automation suite now has 47 focused checks.
- The heap benchmark now settles the headless render clock before both GC measurements. The profiler showed that
  draining only dispatcher jobs left composition batches alive; that was rendering backlog, not undo ownership.
- Final native UIA provider checks pass all ten assertions. The unattended native frame replay was rejected by Windows
  foreground restrictions, so no successful final drag/zoom frame result is claimed. The reproducible interactive
  recorder and its explicit input assertions remain available in the acceptance guide.

Final verification on the current code (2026-10-30): whitespace format, warning-as-error Release build, all 2,094
standard checks, docs contracts and fresh/live docs generation pass. Inspected the regenerated paper previews in both
modes. The 840 painter cases, 360 filled high-DPI cases, 384 handovers plus 1,152 high-DPI handovers, 24 custom/native
export comparisons, ten Windows UIA provider assertions, and PDF/HTML browser checks pass. The only public API addition
is the protected automation-peer override; no shipped member changed. Web sync: PaperFormView/PaperFormBuilder and the
generated editable HTML behavior; the hand-written web port remains for the minor release.

Final isolated scale run, with the same strict budgets and settled memory measurements:

| Case | Load ms | Edit ms / updates | Move ms / updates | Type ms / updates | Retained after 100 resizes | Distinct structures |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 × 5 | 1,192.63 | 19.75 / 1 | 20.95 / 1 | 1.14 / 0 | 913.7 KiB | 5,099 |
| 500 × 10 | 446.42 | 15.16 / 1 | 9.82 / 1 | 0.58 / 0 | 804.5 KiB | 5,099 |

Both pass; the earlier independent run also passed. These are samples on this machine, not a latency guarantee.
Settled heap is whole-view retention including history; the profiler's changed-path ownership estimate remains about
8.3 KiB per flat 1,000-field undo entry. Final synchronous Skia CPU drawing probe passes its drag/zoom exercise checks:
drag median/p95/max 64.56/136.04/140.54 ms; wheel zoom 70.90/87.81/95.29 ms. These exclude desktop presentation.

Remaining acceptance: run the interactive desktop recorder with foreground access, Narrator, hardware IME and GPU
presentation checks from `PAPER_FORMS_ACCEPTANCE.md`; then enable pooled Fill/Preview defaults and repeat the listed
gates. The implementation, fixtures and CI gates are prepared for all issue phases. Changes are local and uncommitted.
