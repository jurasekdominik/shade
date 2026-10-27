# Shade rules for consuming apps

This is the shared instruction source for agents working on apps that use Shade. Read it with the `DESIGN.md` and
[ADOPTION.md](ADOPTION.md) supplied by the app's installed Shade version. Record that version before investigating a gap;
repository main or the live web page may describe features the app has not installed. Recheck these documents on upgrades.

The app's `AGENTS.md` should point here rather than copy the rules. Keep the path/version of any vendored copy recorded
in the app and refresh it on upgrade. The app's own `docs/DESIGN.md` records only its domain and composition decisions.

## Building app UI

- Use Shade controls and role tokens. Find the need in `DESIGN.md` → *Choosing a component*. No raw Avalonia input
  controls, copied Shade implementations, components assembled from a `Border`, literal colours, or literal typography,
  corner and control-height values in views. Use Shade layout helpers and token spacing.
- Compose pages in the app; put reusable control behavior and appearance in Shade. Native Avalonia controls and suitable
  related libraries are implementation tools inside Shade, not substitutes for Shade controls in app views.
- Use public bindings and commands. Keep data, storage, permissions, business validation, routes and application settings
  in the app. Configure Shade through its supported startup and live-update APIs in ADOPTION.md.
- The app owns its translations and domain labels; Shade owns localization of built-in strings. Do not patch control
  internals to change language, culture or theme. Report a missing public hook as a gap.
- Follow Shade's layout, action order, keyboard, status and writing contracts. Check affected app screens in dark/light,
  live mode/accent switching, keyboard use, relevant cultures and supported scaling. Record what was actually tested.
  During iteration the user may perform manual UI testing and supply screenshots/recordings. Record the version, steps
  and environment, distinguish user results from agent checks, and leave unshown cases pending. Run focused checks while
  iterating and the relevant final checks before completion; do not demand full gallery generation for each app edit.

## When something does not fit

Stop implementing the affected substitute; continue independent app work. First check the installed version's API,
adoption guidance and known limits. Reduce the case enough to distinguish app configuration or domain logic from a
reusable control problem. Do not require certainty about the cause before reporting it.

| Finding | Where the change belongs |
| --- | --- |
| App data, business rule, route or page composition | Fix in the app using Shade's public contracts |
| Existing control fails its contract | Shade bug report and shared fix |
| Existing control lacks a generally useful binding, state, localization hook or interaction | Shade improvement/refactor proposal |
| No existing control fits a reusable need | Shade component proposal |
| C# and web differ in equivalent states | Shade parity bug with paired evidence |

One consuming app is enough to demonstrate a reusable need. Describe the capability in general terms, with the app case
as evidence; do not introduce app routes, service clients or domain policy into Shade. Prefer extending the right existing
component over creating a near-duplicate.

## Preparing a Shade report

Search [Shade issues](https://github.com/jurasekdominik/shade/issues) and the backlog first; add evidence to an existing
report when it covers the same cause. Use the repository's bug or improvement/refactor template and
[contribution methodology](https://github.com/jurasekdominik/shade/blob/main/CONTRIBUTING.md).

Include the consuming app and affected task, installed Shade version/commit, environment, minimal reproduction,
expected versus actual behavior, impact/workaround and observable acceptance criteria. For gaps, describe the public
contract needed: values/bindings, commands/events, states, validation, localization and keyboard/accessibility behavior.
Include suspected files or methods only as hypotheses with evidence and a next check. Remove private data.

For visual issues include matching C#/web states in both modes where available; missing access to one implementation
does not block reporting. Mark it unverified. Agents fixing Shade must complete the C#/web comparison before claiming
parity. App agents report the shared problem rather than independently redesigning the web or native control.

Prepare the report locally by default (for example, `docs/shade-issues/<short-name>.md`). Submit it to GitHub only when
the user has authorized posting. If access is unavailable, leave the ready-to-file report and name what is blocked.
An issue draft is a handoff artifact; it does not imply the issue was submitted or the app problem resolved.

## Temporary exceptions and adopting fixes

Do not silently create a local substitute. If a temporary exception is explicitly chosen for the task, mark its line
`// shade-exception: <why; issue or local report; remove when ...>` (use the equivalent comment in XAML). Record the
affected code, limitation and removal condition in the app's `docs/DESIGN.md`. A marker explains an exception; it does
not by itself justify one. Keep exceptions narrow and do not let them become a second component library.

Track the Shade report, fix commit and first released version. A fix merged to main is not available in the app's
installed package. Adopt the released fix (or an explicitly selected project reference), remove obsolete exceptions,
and rerun the original reproduction plus the app's build and relevant UI checks. State separately whether the problem
is reported, fixed in Shade, released, and verified in the consuming app.
