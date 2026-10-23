# Working on Shade

Instructions for coding agents. `CLAUDE.md` imports this file; edit only this one.

Shade is a design-system library for Avalonia 12 / .NET 10 desktop apps: `src/Shade` (namespace `Shade`) holds
the tokens (`Look`) and every shared control; `gallery/` shows each control in each state.

- Read `docs/DESIGN_SYSTEM.md` first. Its §13 rules apply: only tokens and shared components, no literal colours in a
  control; a new component goes into `src/Shade/Controls`, the design system and the gallery in one change.
- Nothing app-specific goes here (no app's pages, domain colours or routes); an app keeps those in its own `docs/DESIGN.md`.
- No button or menu action ends in dots; status text for work in progress ("Connecting…") keeps them.
- Both modes, live: paint with roles (`B(Surface)`, `Kit.Text(text, Second)`): each `Tone` is one shared brush that
  `Look.SetMode` and `Look.SetAccent` recolour. A computed colour (a blend, a tint, a state) is worked out in the control's
  `Restyle()` or `Render`, which `Look.Repaint` runs on every switch; never once in a constructor. No `Colors.White` or
  literal hex for text; text lifted for contrast goes toward `Look.Far`. The `--docs` run fails when a live switch paints
  differently from a fresh build.
- `DESIGN.md` (the design.md format), `docs/index.html` (the live page, from `gallery/landing.html`) and `docs/preview/*`
  are generated in part: the token block, DESIGN.md's colour section and the page's values. After a change run
  `dotnet run --project gallery -c Release -- --docs .`, look at the previews you touched in both modes, and commit them.
  Write prose outside the generated blocks; edit the page in `gallery/landing.html`. A new component goes into
  `gallery/Showcase.cs` too.
- The live page's components are the web versions in `docs/shade.css` and `docs/shade.js` (hand-written, same roles as
  CSS variables). A change to a control's look or behaviour is made there too; `node gallery/page-check.mjs` uses every
  component in a browser (CI runs it), and you look at the page in both modes.
- The mark (logo) lives in two places: `Look.Mark` (drawn by `DrawNodes`) and the `<symbol id="mark">` in
  `gallery/landing.html`.
- Build: `dotnet build Shade.slnx -c Release -warnaserror`.
- Before committing: `dotnet format whitespace Shade.slnx`.
