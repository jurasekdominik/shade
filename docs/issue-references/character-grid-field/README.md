# CharacterGridField visual references

Supporting material for a Shade component proposal, prepared from the Elektroměry prototype using Shade 0.11.0 and Avalonia 12.1.4 on Windows.

- `reference.html`: the actual standalone HTML export, in the user's preferred “Původní mřížka” style. Download and open locally in a browser; no server, dependencies or account are needed. Click a serial number or reading to try editing.
- `html-reference.png`: user-provided screenshot of the HTML editor, the **almost ideal** appearance.
- `current-native-editor.png`: user-provided screenshot of the current app's oversized standard Field overlay; this is an app integration limitation, not a confirmed defect in Field.
- `character-cells.png`: the requested one-character-per-cell structure and colours.

All displayed names, addresses, identifiers and readings are synthetic prototype data. This material is a visual and interaction reference, not a proposed Shade implementation. App-specific page composition, text and colours remain in the consuming app.

The current HTML still has rough edges: focus can replace the field background with white, cell pitch varies between fields, heading backgrounds have white patches, and the sticky toolbar can obscure scrolled content. The new component should retain its grid, supplied fills and exact geometry while editing; the HTML's header/toolbar artifacts are outside the component proposal.
