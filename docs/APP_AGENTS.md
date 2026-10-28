# Shade rules for consuming apps

For agents (and people) building an app on Shade. Read it with the `DESIGN.md` and [ADOPTION.md](ADOPTION.md) of the
installed Shade version, and note that version in the app's docs.

## Building

- Build screens from Shade controls and role tokens. Find the need in `DESIGN.md` → *Choosing a component*.
- Do not recreate a Shade control (button, dropdown, field, table…) from raw Avalonia, and do not copy or patch Shade's
  code. Shade's look is the point; a lookalike drifts on the next upgrade.
- Native Avalonia controls that Shade themes (`CheckBox`, `ToggleSwitch`, `ProgressBar`, `TextBox`; `docs/ADOPTION.md` lists them for
  your version) are used as they are, with `{DynamicResource Shade<Role>}` for any colour of your own. A native control Shade
  does not theme yet is not used where a Shade control exists; file an issue instead.
- Your own layout is yours: pages, panels and arrangements of Shade controls, painted with Shade tokens (`Look` roles,
  `Build` helpers), so both modes and the accent switch live. No colour, size or radius literals.
- Shade's defaults are open: controls expose their structure through public API (placement, order, visibility, content,
  extra actions). Use it before assuming something is fixed. Example: each app places the `EditableTable` ribbon
  controls where it needs them.
- Data, storage, business rules, routes and translations stay in the app.
- Check changes in dark and light mode, and say what you actually checked.

## When something does not fit

If the installed API cannot do what the app needs, **file an issue in Shade yourself, right away**, then keep
working on independent parts. Do not build a local substitute or a patch.

```bash
gh issue create -R jurasekdominik/shade --label from-app --title "<Control>: <what is missing>" --body "..."
```

Search open issues first and add your case to a matching one. One gap per issue. Say:

- **Need:** what the app is doing and why the current API cannot.
- **Ask:** the public API you wish existed (property, slot, event, option), as a sketch. Use a bug report if a
  control breaks its contract: repro, expected, actual, Shade version.
- Mode, accent or culture if it matters. No private data.

If the app cannot wait, use the closest public API and mark it `// shade-gap: #<issue>` so it is easy to find and remove.

## After the fix

When the fix is released, update the Shade package, remove the `shade-gap` marker, and re-run the original case.
A fix merged to Shade's `main` is not in the app until a release is installed.
