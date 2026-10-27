# Working on Shade

Use the repository-local Ponytail skill at `.agents/skills/ponytail/SKILL.md` for coding work. Keep the Shade rules below as the project contract.

Instructions for coding agents. `CLAUDE.md` imports this file; edit only this one.

This file guides work inside Shade. Consuming apps use [docs/APP_AGENTS.md](docs/APP_AGENTS.md).
Read [CONTRIBUTING.md](CONTRIBUTING.md) for issue/refactor methodology and document ownership; keep rules in their
owning document and link to them from summaries. The changelog is a record, not standing task authorization.

Shade is a design-system library for Avalonia 12 / .NET 10 desktop apps: `src/Shade` (namespace `Shade`) holds
the tokens (`Look`) and every shared control; `gallery/` shows each control in each state.

- Read `docs/DESIGN_SYSTEM.md` first. Its §13 rules apply: only tokens and shared components, no literal colours in a
  control; a new component goes into `src/Shade/Controls`, the design system, `DESIGN.md` (its token key, its prose and a
  row in *Choosing a component*) and the gallery in one change. `tests/Shade.Tests` fails when a public control is
  missing from `DESIGN.md` or `gallery/Showcase.cs`.
- `DESIGN.md` is the contract apps and their agents read (it ships in the package). Its components use only design.md's
  sub-tokens (backgroundColor, textColor, typography, rounded, padding, size, height, width), a state is its own key;
  `npx @google/design.md lint DESIGN.md` runs in the release checks.
- Apps link to `docs/APP_AGENTS.md` using the README bootstrap: they use Shade, never patch it, and file issues here
  (`from-app` label). Pick those issues up, fix them in shared code, and make controls open through public API
  (placement, order, visibility, content, extra actions) rather than fixed layouts. Dark and light mode have equal priority.
- Nothing app-specific goes here (no app's pages, domain colours or routes); an app keeps those in its own `docs/DESIGN.md`.
- No button or menu action ends in dots; status text for work in progress ("Connecting…") keeps them.
- Both modes, live: paint with roles (`B(Raised)`, `Build.Text(text, Text2)`): each `Tone` is one shared brush that
  `Look.SetMode` and `Look.SetAccent` recolour. A computed colour (a blend, a tint, a state) is worked out in the control's
  `Restyle()` (the control implements `IRestyle`) or `Render`, which `Look.Repaint` runs on every switch; never once in a constructor. No `Colors.White` or
  literal hex for text; text lifted for contrast goes toward `Look.Far`. The `--docs` run fails when a live switch paints
  differently from a fresh build.
- `DESIGN.md` (the design.md format), `docs/index.html` (the live page, from `gallery/landing.html`) and `docs/preview/*`
  are generated in part: the token block, DESIGN.md's colour section and the page's values. For changes to their sources run
  `dotnet run --project gallery -c Release -- --docs .`, look at the previews you touched in both modes, and commit them.
  Write prose outside the generated blocks; edit the page in `gallery/landing.html`. A new component goes into
  `gallery/Showcase.cs` too.
  During iteration use focused checks and user-provided UI evidence per CONTRIBUTING.md; do final generation and relevant
  checks once on final code. Prose-only docs changes do not require UI testing or regeneration.
- The live page's components are the web versions in `docs/shade.css` and `docs/shade.js` (hand-written, same roles as
  CSS variables). A change to a control's look or behaviour is made there too; `node gallery/page-check.mjs` uses every
  component in a browser (release checks run it), and you look at the page in both modes.
  C# and web must remain visually identical in equivalent states; follow CONTRIBUTING.md's paired comparison workflow.
- The mark (logo) is `assets/shade.svg` (two offset discs, fixed colours, also the page's favicon). In the app it is drawn
  by `Look.DrawMark` (back disc in the accent, front in Text), unless the app passes its own to `Look.Apply` (`mark`; `Look.PaintMark` picks); on the page the same two circles sit inline in
  `gallery/landing.html`. Change the three together.
- Build: `dotnet build Shade.slnx -c Release -warnaserror`; tests: `dotnet run --project tests/Shade.Tests -c Release`.
  Those plus `python gallery/docs_check.py` are all a PR runs. The heavy checks are the manual `full.yml` workflow, once before a release.
- Prefer native Avalonia bases/composition inside Shade and preserve Shade artwork. Follow CONTRIBUTING.md for refactors,
  dependency choices, bindings, localization and lifecycle checks; apps still consume only Shade controls.
- A value people change or an app binds is an Avalonia property: reuse native properties, use `StyledProperty` for
  styleable values or `DirectProperty` raised with `SetAndRaise` for directly owned state, two-way for input. Preserve
  bindings on internal edits. A control XAML can make has a parameterless constructor; `tests/Shade.Tests/Bound.axaml` uses each one.
- The public API is listed in `src/Shade/PublicAPI.Unshipped.txt` (moved to `Shipped` at a release). A new or changed
  public member fails the build until it is listed: `dotnet format analyzers src/Shade/Shade.csproj --diagnostics RS0016 --severity info`
  adds it. Removing or changing a shipped one is a breaking change: say so in the commit.
- The library has nullable checks on: a value that can be null says so (`string?`, `Action?`); data a control takes is
  properties (records for items), not public fields.
- Before committing: `dotnet format whitespace Shade.slnx`.
- Versions (0.x): components and fixes normally advance the patch; public breaks require a minor. An explicitly requested
  minor may group non-breaking work; say so in the release notes. `CHANGELOG.md` names each release.
