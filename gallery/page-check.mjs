// The live page's components, used the way a person would: Node + Playwright, `node gallery/page-check.mjs`.
// Fails (exit 1) when a component stops working or the page logs an error.
import { chromium } from 'playwright';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { readFileSync } from 'node:fs';
import { popupContracts } from './popup-check.mjs';
const target = process.argv[2] || 'docs/index.html';
const page = /^https?:\/\//.test(target) ? new URL(target).href : pathToFileURL(resolve(target)).href;
let failed = 0;
const b = await chromium.launch(process.env.CHROMIUM ? { executablePath: process.env.CHROMIUM } : {});
const p = await b.newPage({ viewport: { width: 1280, height: 900 } });
const errors = []; p.on('pageerror', e => errors.push(e.message)); p.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
await p.addInitScript(() => localStorage.setItem('shade-theme', 'dark'));
await p.goto(page);
const ok = (cond, what) => { if (!cond) failed++; console.log((cond ? 'ok   ' : 'FAIL ') + what); };
await p.evaluate(() => document.fonts.ready.then(() => null));
ok(await p.evaluate(() => document.fonts.size >= 4 && document.fonts.check('13px Inter')), 'the bundled interface fonts load');
// the page's structure: every subsection sits in its chapter's column (a stray closing tag once pushed one out to the window's edge)
ok(await p.evaluate(() => [...document.querySelectorAll('.sub')].every(s => s.parentElement.classList.contains('wrap'))), 'every subsection is inside its chapter');
// dropdown: open, arrow down, enter
const sel = p.locator('.s-select').nth(1);
await sel.locator(':scope > button').click();
ok(await sel.locator('.s-menu.attached .row').count() === 4, 'dropdown opens with its 4 items');
ok(await sel.locator('.row').first().evaluate(row => row.tagName === 'BUTTON'), 'picker options use native button targets');
ok(await sel.evaluate(e => e.classList.contains('open')), 'field and list are one shape (open class)');
await p.keyboard.press('ArrowDown'); await p.keyboard.press('Enter');
ok((await sel.locator(':scope > button span').textContent()) === 'Research', 'arrow + Enter picks Research');
// multi
const multi = p.locator('.s-select[data-multi]');
await multi.locator(':scope > button').click();
ok(await multi.locator('.row:focus-visible').count() === 0, 'pointer opening multi-choice does not show a keyboard focus ring');
await multi.locator(':scope > button').click();
ok(await multi.locator('.s-menu').count() === 0, 'clicking the open multi-choice trigger closes it');
await multi.locator(':scope > button').click(); await multi.locator('.row').first().click(); await multi.locator('.row').nth(1).click();
ok((await multi.locator(':scope > button span').textContent()) === 'Anna, Ben', 'multi choice shows the picks');
await p.keyboard.press('Escape');
ok(await multi.locator('.s-menu').count() === 0, 'Esc closes');
ok(await multi.locator(':scope > button').evaluate(b => b === document.activeElement), 'multi-choice Escape restores trigger focus');
await multi.evaluate(e => { e.changes = []; e.addEventListener('change', event => e.changes.push(event.detail.values)); });
await multi.locator(':scope > button').press('ArrowDown');
ok(await multi.locator('.row:focus-visible').count() === 1, 'keyboard opening multi-choice shows its option focus ring');
ok(await multi.locator('.row').first().evaluate(e => e === document.activeElement), 'multi-choice opens on the first selected option');
await p.keyboard.press('End'); await p.keyboard.press('Space');
ok((await multi.locator(':scope > button span').textContent()) === '3 selected' && await multi.locator('.s-menu').isVisible(), 'multi-choice End and Space toggle without closing and summarize the count');
ok(await multi.evaluate(e => JSON.stringify(e.changes) === '[["Anna","Ben","Chloe"]]'), 'multi-choice user changes emit selected values once');
await multi.evaluate(e => e.setSelected(['Ben']));
ok(await multi.locator('.row[role="checkbox"][aria-checked="true"]').count() === 1 && await multi.evaluate(e => e.changes.length === 1), 'programmatic selection updates open checkboxes without emitting user changes');
await p.keyboard.press('Home'); await p.keyboard.press('c');
ok(await multi.locator('.row').nth(2).evaluate(e => e === document.activeElement), 'multi-choice typeahead focuses a matching option');
await p.keyboard.press('Escape');
await multi.locator(':scope > button').press('ArrowUp');
ok(await multi.locator('.row').last().evaluate(e => e === document.activeElement), 'multi-choice Up opens on the last option');
await p.keyboard.press('Tab');
ok(await multi.locator('.s-menu').count() === 0 && !(await multi.locator(':scope > button').evaluate(b => b === document.activeElement)), 'multi-choice Tab dismisses and continues through the page');
await multi.locator(':scope > button').click();
await multi.locator(':scope > button').evaluate(b => b.disabled = true);
await p.waitForTimeout(0);
ok(await multi.locator('.s-menu').count() === 0 && await multi.locator(':scope > button').getAttribute('aria-expanded') === 'false', 'disabling multi-choice dismisses its popup');
await multi.locator(':scope > button').evaluate(b => b.disabled = false);
await multi.evaluate(e => e.set([], [])); await multi.locator(':scope > button').click();
ok(await multi.locator('.s-menu').count() === 0 && await multi.evaluate(e => e.value().length === 0), 'empty multi-choice options stay closed');
await multi.evaluate(e => { e.setSelected(['Ben']); e.set([]); });
ok(await multi.evaluate(e => JSON.stringify(e.value()) === '["Ben"]'), 'empty picker options preserve bound selection while loading');
await multi.evaluate(e => e.set(['Anna', 'Ben', 'Chloe']));
ok(await multi.evaluate(e => JSON.stringify(e.value()) === '["Ben"]'), 'a loaded picker reconciles previously selected keys');
await multi.evaluate(e => e.set(['Anna', 'Ben', 'Chloe'], ['Anna']));

// SearchChoice: every loaded object, independent query/selection, bounded rows.
const searchable = p.locator('.s-search-choice');
await searchable.evaluate(box => { box.setValue(2); box.picks = 0; box.addEventListener('change', () => box.picks++); });
await searchable.scrollIntoViewIfNeeded();
const textPosition = () => searchable.locator(':scope > button span').evaluate(label => { const range = document.createRange(); range.selectNodeContents(label); const r = range.getBoundingClientRect(); return { x: r.x + scrollX, y: r.y + scrollY }; });
const closedPosition = await textPosition();
await searchable.locator(':scope > button').click();
const openedPosition = await textPosition();
ok(Math.abs(closedPosition.x - openedPosition.x) < 0.01 && Math.abs(closedPosition.y - openedPosition.y) < 0.01, 'searchable dropdown text does not move when opened');
const query = searchable.locator('input');
ok(await searchable.locator('.s-search-option').count() === 5 && await query.evaluate(input => input === document.activeElement), 'searchable dropdown opens all options with focus in search');
await query.fill('lá');
ok((await searchable.locator('mark.s-hit').allTextContents()).join('|') === 'lá|lá', 'searchable dropdown highlights the matching letters in Šílá and Malá');
ok(await searchable.locator('mark.s-hit').first().evaluate(mark => getComputedStyle(mark).color !== getComputedStyle(mark.parentElement).color), 'search matches use the readable accent text colour');
await query.fill('la');
ok((await searchable.locator('mark.s-hit').allTextContents()).join('|') === 'lá|lá', 'unaccented queries highlight original accented letters');
await query.fill('nov novak');
ok((await searchable.locator('mark.s-hit').allTextContents()).join('|') === 'Novák|Novák', 'overlapping search words produce a single highlight per match');
await query.fill('mgr');
ok(await searchable.locator('.s-search-option').count() === 1 && await searchable.locator('mark').count() === 0, 'hidden searchable fields filter without inventing visible label matches');
await query.fill('NOVAK mgr');
ok(await searchable.locator('.s-search-option').count() === 1 && await searchable.evaluate(box => box.value().id === 2 && box.picks === 0), 'every query word matches custom fields without case/accents and preserves selection');
await query.fill('absent employee'); await query.press('Enter');
ok(await searchable.locator('.s-search-empty').isVisible() && await searchable.evaluate(box => box.value().id === 2 && box.picks === 0), 'empty search results do not clear or commit selection');
await query.press('Escape');
ok(await searchable.locator('.s-search-popup').count() === 0 && await searchable.locator(':scope > button').evaluate(button => button === document.activeElement), 'searchable Escape dismisses and restores its trigger');
await searchable.locator(':scope > button').press('ArrowDown'); await query.fill('jan'); await query.press('ArrowDown'); await query.press('Enter');
ok(await searchable.evaluate(box => box.value().id === 5 && box.picks === 1), 'duplicate labels commit the chosen object ID once');
await searchable.locator(':scope > button').click();
ok(await query.inputValue() === '' && await searchable.locator('.s-search-option').count() === 5, 'reopening searchable dropdown resets the query');
await searchable.locator(':scope > button').evaluate(button => button.disabled = true); await p.waitForTimeout(0);
ok(await searchable.locator('.s-search-popup').count() === 0, 'disabling searchable dropdown dismisses its popup');
await searchable.locator(':scope > button').evaluate(button => button.disabled = false);
await searchable.evaluate(box => box.setItems(Array.from({ length: 10000 }, (_, id) => ({ id, label: 'Employee ' + id }))));
await searchable.locator(':scope > button').click();
ok(await searchable.locator('.s-search-option').count() <= 16, 'ten thousand searchable options realize only a bounded viewport');
await searchable.locator('.s-search-results').evaluate(view => view.scrollTop = 280000); await p.waitForTimeout(50);
ok(await searchable.locator('.s-search-option').filter({ hasText: /^Employee 9999$/ }).count() === 1, 'scrolling can reach the last loaded option');
await query.fill('employee 9999'); await query.press('Enter');
ok(await searchable.evaluate(box => box.value().id === 9999 && box.picks === 2), 'word filtering finds and commits an option beyond the initial viewport');
await searchable.locator(':scope > button').click(); await query.press('Tab');
ok(await searchable.locator('.s-search-popup').count() === 0, 'Tab dismisses searchable choices');

// PairTable retains invalid drafts and validates before export.
const pairs = p.locator('.s-pair-table');
ok(await pairs.evaluate(e => e.dataset.invalid === 'true' && e.draft().length === 2 && e.pairs() === null), 'pair duplicates stay editable and prevent lossy export');
await pairs.locator('[data-name]').nth(1).fill('X-Token');
ok(await pairs.evaluate(e => e.dataset.invalid === 'false' && e.pairs().length === 2), 'renaming a pair clears case-insensitive duplicate validation');
await pairs.evaluate(e => e.originalEditor = e.querySelector('input'));
await pairs.locator('[data-add-pair]').click();
await pairs.locator('[data-value]').last().fill('unfinished draft');
ok(await pairs.evaluate(e => e.querySelector('input') === e.originalEditor && e.draft().length === 3 && e.draft()[2].value === 'unfinished draft' && e.pairs().length === 2), 'adding pairs retains editors and blank drafts without exporting empty names');
await pairs.locator('[data-name]').first().fill('x-token');
ok(await pairs.locator('[data-name][aria-invalid="true"]').count() === 2 && await pairs.locator('[data-value]').last().inputValue() === 'unfinished draft', 'case-insensitive pair validation preserves every value');
await pairs.locator('[data-remove-pair]').first().click();
ok(await pairs.evaluate(e => e.dataset.invalid === 'false' && e.draft().length === 2), 'native pair removal updates validation');

// suggestions
const sug = p.locator('.s-suggest input');
await sug.click(); await sug.type('ann');
ok(await p.locator('.s-suggest .row').count() === 2 && (await p.locator('.s-suggest .row mark.s-hit').first().textContent()) === 'Ann', 'typing ann suggests 2, the match highlighted');
await p.keyboard.press('ArrowDown'); await p.keyboard.press('Enter');
ok((await sug.inputValue()) === 'Annual budget', 'Enter takes the first suggestion');
// switch, tick, chip, state
const sw = p.locator('.s-switch').first(); await sw.click(); ok((await sw.getAttribute('aria-checked')) === 'false', 'switch toggles');
const tk = p.locator('button.s-tick').nth(1); await tk.click(); ok((await tk.getAttribute('aria-checked')) === 'true', 'mixed tick ticks');
const ch = p.locator('.s-chip').first(); await ch.click(); ok((await ch.getAttribute('aria-pressed')) === 'true', 'chip toggles');
const st = p.locator('#actions-inputs .s-state').nth(1); await st.click(); ok((await st.getAttribute('data-state')) === 'on', 'state switch flips');
// tabs
await p.locator('#navigation .masonry .s-tabs button').first().click();
ok(await p.locator('#navigation .masonry .s-tabs .line').evaluate(l => l.style.left === '0px'), 'tab line moves');
const tabStrip = p.locator('#navigation .masonry .s-tabs').first();
await tabStrip.locator('button').first().focus(); await p.keyboard.press('ArrowRight');
ok(await tabStrip.locator('button').nth(1).evaluate(e => e === document.activeElement && e.getAttribute('aria-selected') === 'true'), 'arrow keys move tab selection and focus');
await p.keyboard.press('Home');
ok(await tabStrip.locator('button').first().evaluate(e => e === document.activeElement && e.tabIndex === 0) && await tabStrip.locator('button').nth(1).getAttribute('tabindex') === '-1', 'tabs use one tab stop and Home returns to the first');
const summary = p.locator('.s-cards').first();
await summary.locator('button.s-card').first().focus(); await p.keyboard.press('ArrowRight');
ok(await summary.locator('button.s-card').nth(1).evaluate(e => e === document.activeElement), 'summary cards support arrow navigation');
// table sort + select + context menu
const t = p.locator('.s-table.sortable');
await t.locator('th').nth(2).click();
const prices = await t.locator('tbody tr td:nth-child(3)').allTextContents();
ok(prices.map(Number).every((v, i, a) => i === 0 || a[i - 1] <= v), 'sorts by price');
const firstOrder = ['93.00', '106.00'];
await t.locator('th').nth(2).click(); await t.locator('th').nth(2).click();
ok((await t.locator('tbody tr td:nth-child(3)').allTextContents()).slice(0, 2).join() === firstOrder.join() && !(await t.locator('th[aria-sort]').count()), 'the third click returns to the original order');
await t.locator('th').nth(2).click();
await t.locator('tbody tr').nth(2).click(); ok(await t.locator('tr.selected').count() === 1, 'row selects');
await t.locator('tbody tr').nth(3).click({ button: 'right' }); ok(await p.locator('.s-menu.floating').count() === 1, 'right-click opens the menu');
await p.keyboard.press('Escape');
// tag remove
await p.locator('.s-tag [data-remove]').click({ button: 'right' });
ok(await p.locator('.s-tag').filter({ hasText: 'brief-v2.pdf' }).count() === 1, 'native tag removal ignores right-click');
await p.locator('.s-tag [data-remove]').click(); ok(await p.locator('.s-tag').filter({ hasText: 'brief-v2.pdf' }).count() === 0, 'tag removes');
// dialog
await p.locator('[data-dialog="delete"]').click(); ok(await p.locator('#delete').evaluate(d => d.open), 'dialog opens');
await p.keyboard.press('Escape'); ok(!(await p.locator('#delete').evaluate(d => d.open)), 'Esc closes the dialog');
// tooltip
await p.locator('[data-tip="Tooltips: Fill, Outline and Label text"]').hover(); ok(!(await p.locator('.s-tip').evaluate(e => e.hidden)), 'tooltip shows');
// shell
const shell = p.locator('.s-shell');
await shell.locator('.s-nav[data-route="tasks"]').click(); ok((await shell.locator('h5').textContent()) === 'Tasks', 'nav goes to Tasks');
await shell.locator('.back').click(); ok((await shell.locator('h5').textContent()) === 'Overview', 'Back returns to Overview');
await shell.locator('.fwd').click(); ok((await shell.locator('h5').textContent()) === 'Tasks', 'Forward goes to Tasks');
await shell.locator('.brand').click(); await p.waitForTimeout(500);
ok(Math.round(await shell.locator('.s-sidebar').evaluate(e => e.getBoundingClientRect().width)) === 54, 'sidebar collapses to 54 px');
await shell.locator('.brand').click(); await p.waitForTimeout(500);
// accent: a curated one takes its value for each mode; the palette and the mixer take any colour
const accentNow = () => p.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--accent').trim().toUpperCase());
const teal = p.locator('#helper .s-picker .cur[data-name="Teal"]');
await teal.click();
ok((await accentNow()) === (await teal.getAttribute('data-dark')), 'a curated accent takes its dark value');
ok(await p.locator('.s-accent .swatches button[data-name="Teal"]').evaluate(button => button.classList.contains('current')) && await p.locator('.s-accent .name').textContent() === 'Teal', 'the full colour helper updates the compact accent picker');
ok((await p.locator('#colors .swatch:has(.chip[style*="var(--accent)"]) .mono.only-dark').textContent()) === (await teal.getAttribute('data-dark')), 'the Accent swatch names the picked accent');
console.log('     primary button now', await p.locator('.s-btn.primary').first().evaluate(e => getComputedStyle(e).backgroundColor));
await p.locator('.s-mode >> nth=0 >> button[data-mode="light"]').click();
ok((await accentNow()) === (await teal.getAttribute('data-light')), 'switching to light takes its light value');
await p.locator('.s-mode >> nth=0 >> button[data-mode="dark"]').click();
ok(await p.locator('.s-mode').first().evaluate(e => { const probe = document.createElement('span'); probe.style.color = 'var(--separator)'; e.appendChild(probe); const neutral = getComputedStyle(e).borderTopColor === getComputedStyle(probe).color; probe.remove(); return neutral; }), 'pointer mode selection keeps the outer frame neutral');
await p.locator('.s-mode >> nth=0 >> button[data-mode="dark"]').press('ArrowLeft');
ok(await p.locator('.s-mode button:focus-visible').count() === 1 && await p.locator('.s-mode button:focus-visible').evaluate(e => getComputedStyle(e, '::after').borderTopWidth === '1px'), 'keyboard mode selection shows a single segment focus ring');
await p.locator('.s-mode >> nth=0 >> button[data-mode="dark"]').click();
await p.locator('#helper .s-picker .s-tabs button[data-show="palette"]').click();
await p.locator('#helper .s-picker .palette button').nth(17 * 3 + 5).click();
ok((await accentNow()) === (await p.locator('#helper .s-picker .palette button').nth(17 * 3 + 5).getAttribute('data-c')), 'a palette colour becomes the accent');
await p.locator('#helper .s-picker .s-tabs button[data-show="mixer"]').click();
const f = await p.locator('#helper .s-picker .field').boundingBox();
await p.mouse.move(f.x + f.width * 0.8, f.y + f.height * 0.2); await p.mouse.down(); await p.mouse.move(f.x + f.width * 0.9, f.y + f.height * 0.1); await p.mouse.up();
ok((await p.locator('#helper .s-picker .what').textContent()) === 'mixed', 'dragging the mixer mixes an accent ' + (await accentNow()));
// the accent picker row: a swatch, a typed colour, the field's colour helper
const row = p.locator('.s-accent');
await row.locator('.swatches button[data-name="Lime"]').click();
ok((await accentNow()) === (await row.locator('.swatches button[data-name="Lime"]').getAttribute('data-dark')) && (await row.locator('.name').textContent()) === 'Lime', 'a swatch sets its curated accent and names it');
ok(await p.locator('#helper .cur[data-name="Lime"]').evaluate(button => button.classList.contains('current')) && await p.locator('#helper .what').textContent() === 'Lime', 'the compact picker updates the full helper selection and label');
await row.locator('.hex').fill('#3366FF');
ok(await p.locator('#helper .cur.current').count() === 0 && await p.locator('#helper .foot .mono').textContent() === '#3366FF', 'custom accents clear curated selection in the other helper');
ok((await accentNow()) === '#3366FF', 'a typed colour becomes the accent');
for (const mode of ['dark', 'light']) {
  await p.locator('.s-mode').first().locator('button[data-mode="' + mode + '"]').click();
  for (const color of ['#FFFFFF', '#000000']) {
    await row.locator('.hex').fill(color);
    ok(await p.evaluate(() => {
      const style = getComputedStyle(document.documentElement);
      const lum = key => {
        const color = style.getPropertyValue('--' + key).trim();
        const channels = color.match(/[a-f0-9]{2}/gi).map(c => parseInt(c, 16) / 255).map(c => c <= .04045 ? c / 12.92 : ((c + .055) / 1.055) ** 2.4);
        return channels[0] * .2126 + channels[1] * .7152 + channels[2] * .0722;
      };
      const focus = lum('focus-border');
      return ['raised', 'background'].every(key => (Math.max(focus, lum(key)) + .05) / (Math.min(focus, lum(key)) + .05) >= 3);
    }), 'focus contrast survives ' + color + ' in ' + mode);
  }
}
await p.locator('.s-mode').first().locator('button[data-mode="dark"]').click();
await row.locator('.hex').fill('#3366FF');
await row.locator('.hex').click();
ok(await row.locator('.pop').isVisible(), 'the Custom field opens the colour helper');
await row.locator('.pop .cur[data-name="Teal"]').click();
ok((await accentNow()) === (await teal.getAttribute('data-dark')) && (await row.locator('.name').textContent()) === 'Teal', 'a curated accent in the helper shows in the row');
await p.mouse.click(5, 5);
ok(!(await row.locator('.pop').isVisible()), 'a click elsewhere closes the helper');
// the date field: year, month and day are blocks; a click anywhere opens the month (a popup over the page, never cut by the columns)
const dateBox = p.locator('.s-date').first(), segs = dateBox.locator('.segs'), dayValue = () => dateBox.evaluate(e => e.value());
await dateBox.locator('.seg[data-p="d"]').click();
ok(await p.locator('.s-month:not([hidden])').count() === 1 && (await p.locator('.s-month:not([hidden]) .day.picked').textContent()) === '28', 'a click on the day block opens the month on the day');
ok(await p.locator('.s-month:not([hidden])').evaluate(m => m.parentElement === document.body && getComputedStyle(m).position === 'fixed'), 'the month floats over the page');
ok((await p.locator('.s-month:not([hidden]) .day.we').count()) === 12 && (await p.locator('.s-month:not([hidden]) .band').count()) === 2, 'weekends are marked: two bands, twelve days');
await p.keyboard.press('ArrowUp');
ok((await dayValue()) === '2026-10-29', 'Up raises the day block');
await p.keyboard.press('ArrowLeft'); await p.keyboard.type('2');
ok((await dayValue()) === '2026-02-28', 'typing 2 in the month makes February and keeps the day inside it: ' + await dayValue());
await p.keyboard.type('15');
ok((await dayValue()) === '2026-02-15', 'the typed digits move on to the day block');
await p.locator('.s-month:not([hidden]) .day[data-day="2026-02-20"]').click();
ok((await dayValue()) === '2026-02-20' && await p.locator('.s-month:not([hidden])').count() === 0, 'a click on a day picks it and closes the month');
await segs.press('Alt+ArrowDown'); await p.keyboard.press('ArrowRight'); await p.keyboard.press('ArrowDown'); await p.keyboard.press('Enter');
ok((await dayValue()) === '2026-02-28', 'Alt+Down opens the month; arrows move the day, Enter picks it');
await dateBox.locator('.seg[data-p="y"]').click(); await p.keyboard.press('Escape'); await p.keyboard.press('Backspace');
ok((await dayValue()) === null && (await dateBox.locator('.seg[data-p="y"]').textContent()) === 'YYYY', 'Backspace empties a block, which shows its placeholder');
await p.locator('.s-date[data-weekends="grey"]').click({ position: { x: 20, y: 14 } });
ok(await p.locator('.s-month.grey:not([hidden]) .band').first().isVisible(), 'the grey weekends variant opens with its bands');
await p.keyboard.press('Escape');
// the date range: start, then end; the days between them are coloured
const range = p.locator('.s-date.range');
await range.locator('.segs').first().locator('.seg').first().click();
ok((await p.locator('.s-month.range:not([hidden]) .day.in').count()) === 7, 'the month colours the 7 days inside the range');
await p.locator('.s-month.range:not([hidden]) .day[data-day="2026-10-05"]').click();
await p.locator('.s-month.range:not([hidden]) .day[data-day="2026-10-09"]').hover();
ok((await p.locator('.s-month.range:not([hidden]) .day.in').count()) === 3 && (await p.locator('.s-month.range:not([hidden]) .span').textContent()) === '5 days', 'after the start, hovering an end previews the range');
ok(await range.evaluate(r => { const b = r.getBoundingClientRect(), i = r.querySelector('.cal').getBoundingClientRect(); return i.right <= b.right - 2; }), 'with the end empty, the calendar icon stays inside the field');
await p.locator('.s-month.range:not([hidden]) .day[data-day="2026-10-09"]').click();
ok(JSON.stringify(await range.evaluate(e => e.value())) === '["2026-10-05","2026-10-09"]' && await p.locator('.s-month.range:not([hidden])').count() === 0, 'the end closes the month: 5 to 9 October');
// the editable table: edit, units, formulas, footer, resize, sorting, selection, row menu, adding rows, headings on end
const g = p.locator('#order-grid'); await g.scrollIntoViewIfNeeded();
ok(await g.locator('.grid-ribbon').isHidden() && await g.locator('.column').isDisabled(), 'data mode keeps the ribbon closed and column creation disabled');
ok(await g.locator('.edit-columns').evaluate(edit => edit.getBoundingClientRect().left < edit.parentElement.querySelector('.column').getBoundingClientRect().left), 'Edit columns is left of the column creation actions');
ok(await p.getByRole('heading', { name: 'Editable table · table', exact: true }).count() === 0 && await p.getByRole('heading', { name: 'Editable table · column', exact: true }).count() === 0, 'the detached configuration panels are removed');
const cell = (r, k) => g.locator('tbody tr').nth(r).locator(`td[data-k="${k}"] .v`);
const foot = k => g.locator(`tfoot td[data-k="${k}"] b`).textContent();
await cell(0, 'qty').click(); await p.keyboard.press('Control+A'); await p.keyboard.type('10');
ok((await cell(0, 'total').textContent()) === '49.00' && (await cell(0, 'net').textContent()) === '48.20', 'an edit recomputes Total = Quantity × Price and Net = Total − Discount + Shipping');
ok((await foot('total')) === '281.50', 'and the footer sum follows');
await cell(0, 'qty').fill('letters');
ok(await cell(0, 'qty').getAttribute('aria-invalid') === 'true' && await g.locator('.s-grid-error').isVisible() && await cell(0, 'total').textContent() === '49.00', 'invalid unit values show an error and preserve the last valid calculation');
ok(await cell(0, 'qty').evaluate(input => {
  const cell = input.parentElement, style = getComputedStyle(cell), token = style.getPropertyValue('--danger').trim();
  const rgb = 'rgb(' + token.slice(1).match(/../g).map(value => parseInt(value, 16)).join(', ') + ')';
  return style.boxShadow.includes(rgb) && (style.boxShadow.match(/rgb\(/g) || []).length === 1 && getComputedStyle(input).outlineStyle === 'none';
}), 'invalid cells have one danger border around the whole cell, without an accent ring beneath it');
await p.keyboard.press('Tab');
ok(await cell(0, 'qty').evaluate(value => value === document.activeElement && value.textContent === 'letters'), 'Tab keeps an invalid numeric draft available to correct');
await p.keyboard.press('Escape');
ok(await cell(0, 'qty').textContent() === '10' && !(await g.locator('.s-grid-error').isVisible()), 'Escape cancels an invalid numeric draft without clearing the saved cell');
await cell(0, 'qty').fill('bad'); await cell(0, 'qty').fill('10');
ok(await cell(0, 'qty').getAttribute('aria-invalid') === 'false', 'correcting to the existing saved number clears validation');
await p.keyboard.press('Enter');
ok(await cell(1, 'qty').evaluate(e => e === document.activeElement), 'Enter goes down the column');
const unit = g.locator('tbody tr').nth(0).locator('.unit');
await unit.click();
ok((await unit.textContent()) === 'pallet', 'a click on the unit cycles it (box → pallet)');
await unit.click(); ok((await unit.textContent()) === 'pcs', 'and wraps to the first');
await g.locator('tfoot td[data-k="price"] .fn').click(); await p.locator('.s-menu .row', { hasText: 'Max' }).click();
ok((await foot('price')) === '129.00', 'the footer menu switches Average to Max');
await cell(0, 'net').click();  // computed: read only, still the column's controls follow focus elsewhere
await cell(0, 'item').click();
await g.locator('.edit-columns').click();
ok(await g.locator('.grid-ribbon').isVisible() && await g.locator('.edit-columns').getAttribute('aria-pressed') === 'true', 'Edit columns opens the integrated ribbon');
await g.locator('[data-col="title"]').fill('Article');
ok((await g.locator('th[data-k="item"] .h').textContent()) === 'Article', 'the column controls rename the focused column');
await cell(0, 'price').click();
ok(await g.locator('[data-when="computed"]').isHidden(), 'the formula shows only for a computed column');
await g.locator('tbody tr').nth(0).locator('td[data-k="total"]').click();
const tableW = () => g.locator('table').evaluate(e => Math.round(e.getBoundingClientRect().width)), itemW = () => g.locator('th[data-k="item"]').evaluate(e => e.getBoundingClientRect().width);
ok((await tableW()) <= (await g.locator('.scroll').evaluate(e => e.clientWidth)), 'the table fits its box: the long item wraps instead of widening it');
const t0 = await tableW(), i0 = await itemW();
const w0 = await g.locator('th[data-k="sku"]').evaluate(e => e.getBoundingClientRect().width), rz = await g.locator('th[data-k="sku"] .rz').boundingBox();
await p.mouse.move(rz.x + 3, rz.y + 10); await p.mouse.down(); await p.mouse.move(rz.x + 63, rz.y + 10, { steps: 4 }); await p.mouse.up();
ok(Math.abs((await g.locator('th[data-k="sku"]').evaluate(e => e.getBoundingClientRect().width)) - w0 - 60) < 3, 'a heading\'s edge drags the column 60 px wider');
ok((await tableW()) === t0 && Math.abs(i0 - (await itemW()) - 60) < 3, 'the item column gives the 60 px; the table keeps its width');
const rz2 = await g.locator('th[data-k="sku"] .rz').boundingBox();
await p.mouse.move(rz2.x + 3, rz2.y + 10); await p.mouse.down(); await p.mouse.move(rz2.x - 37, rz2.y + 10, { steps: 4 }); await p.mouse.up();
ok(Math.abs((await g.locator('th[data-k="sku"]').evaluate(e => e.getBoundingClientRect().width)) - w0 - 20) < 3 && Math.abs(i0 - (await itemW()) - 20) < 3, 'dragged back 40 px, the column narrows and the item column takes the room');
await g.locator('th[data-k="sku"] .rz').dblclick();
ok(Math.abs((await g.locator('th[data-k="sku"]').evaluate(e => e.getBoundingClientRect().width)) - w0) < 3, 'a double-click fits it again');
await cell(0, 'sku').click();
ok(await g.locator('th[data-k="sku"]').evaluate(e => e.classList.contains('current') && getComputedStyle(e).backgroundColor !== getComputedStyle(e.previousElementSibling).backgroundColor), 'the current column\'s heading has the accent tint');
ok(await g.locator('th[data-k="sku"] .h').evaluate(e => e.getClientRects().length === 1), 'a fit column\'s heading stays on one line');
const ir = await g.locator('th[data-k="item"] .rz').boundingBox();
await p.mouse.move(ir.x + 3, ir.y + 10); await p.mouse.down(); await p.mouse.move(ir.x + 403, ir.y + 10, { steps: 4 }); await p.mouse.up();
ok((await tableW()) > t0 + 300, 'a fill column\'s edge sets its minimum: past the box, the rows scroll across');
await g.locator('th[data-k="item"] .rz').dblclick();
ok((await tableW()) === t0, 'a double-click clears the minimum');
// a general column: a number sits right, a code left, both kept as typed
await cell(0, 'sku').click();
await g.locator('.s-select[data-col="type"] button').click(); await p.locator('.s-menu .row', { hasText: 'General' }).click();
await cell(1, 'sku').click(); await p.keyboard.press('Control+A'); await p.keyboard.type('042');
ok(await g.locator('tbody tr').nth(1).locator('td[data-k="sku"]').evaluate(e => e.classList.contains('num')) && !(await g.locator('tbody tr').nth(0).locator('td[data-k="sku"]').evaluate(e => e.classList.contains('num'))) && (await cell(1, 'sku').textContent()) === '042', 'a general column: 042 sits right as typed, a code left');
for (const alignment of ['Left', 'Center', 'Right']) {
  await g.locator('[data-col="alignment"] > button').click(); await p.locator('.s-menu .row', { hasText: new RegExp('^' + alignment + '$') }).click();
  ok(await g.locator('tbody td[data-k="sku"] .v').evaluateAll((cells, expected) => cells.every(cell => getComputedStyle(cell).textAlign === expected), alignment.toLowerCase()), `column alignment ${alignment} applies to both numeric and text codes`);
}
await g.locator('[data-col="alignment"] > button').click(); await p.locator('.s-menu .row', { hasText: /^Auto$/ }).click();
await g.locator('[data-col="suffix"]').fill('kg'); await g.locator('[data-col="decimals"] > button').click(); await p.locator('.s-menu .row', { hasText: /^3$/ }).click();
ok((await g.locator('tbody td[data-k="sku"] .suf').first().textContent()) === 'kg' && (await g.locator('[data-col="decimals"] > button').textContent()).trim() === '3', 'the ribbon sets a column suffix and decimals');
await g.locator('[data-col="suffix"]').fill(''); await g.locator('[data-col="decimals"] > button').click(); await p.locator('.s-menu .row', { hasText: /^Auto$/ }).click();
ok(await g.locator('tbody td[data-k="sku"] .suf').count() === 0, 'an empty suffix removes it');
await cell(1, 'sku').click();
await p.keyboard.press('Control+A'); await p.keyboard.type('PEN-BL-10');
await g.locator('.s-select[data-col="type"] button').click(); await p.locator('.s-menu .row', { hasText: /^Text$/ }).click();
const items = async () => (await g.locator('tbody td[data-k="item"] .v').allTextContents()).join('|');
await g.locator('.edit-columns').click();
const original = await items();
await g.locator('th[data-k="price"]').click({ position: { x: 10, y: 10 } });
const ps = (await g.locator('tbody td[data-k="price"] .v').allTextContents()).map(Number);
ok(ps.every((v, i, a) => !i || a[i - 1] <= v), 'a heading click sorts ascending');
await g.locator('th[data-k="price"]').click({ position: { x: 10, y: 10 } }); await g.locator('th[data-k="price"]').click({ position: { x: 10, y: 10 } });
ok((await items()) === original, 'the third click returns to the original order');
await g.locator('.edit-columns').click(); await g.getByRole('tab', { name: 'Table', exact: true }).click();
await g.locator('.s-select[data-opt="sorting"] > button').click(); await p.locator('.s-menu .row', { hasText: /^Off$/ }).click();
await g.locator('.edit-columns').click();
await g.locator('th[data-k="price"]').click({ position: { x: 10, y: 10 } });
ok((await items()) === original && !(await g.locator('th[aria-sort]').count()), 'with sorting off, a heading click does not sort');
await g.locator('.edit-columns').click(); await g.getByRole('tab', { name: 'Table', exact: true }).click();
await g.locator('.s-select[data-opt="sorting"] > button').click(); await p.locator('.s-menu .row', { hasText: /^On$/ }).click();
await g.locator('.edit-columns').click();
await g.locator('tbody tr').nth(1).locator('.pick').click(); await g.locator('tbody tr').nth(3).locator('.pick').click({ modifiers: ['Shift'] });
await g.locator('tbody tr').nth(5).locator('.pick').click({ modifiers: ['Control'] });
ok((await g.locator('tbody tr.selected').count()) === 4, 'row numbers select: Shift takes a range, Ctrl adds');
await g.locator('tbody tr').nth(2).click({ button: 'right', position: { x: 200, y: 10 } });
await p.locator('.s-menu .row', { hasText: 'Duplicate 4 rows' }).click();
ok((await g.locator('tbody tr').count()) === 10 && (await g.locator('tbody tr.selected').count()) === 4, 'the row menu duplicates the 4 selected rows');
await g.locator('tbody tr.selected').first().click({ button: 'right', position: { x: 200, y: 10 } });
await p.locator('.s-menu .row', { hasText: 'Delete 4 rows' }).click();
ok(await p.locator('dialog.s-dialog[open]').count() === 1, 'Delete asks first');
await p.locator('dialog.s-dialog[open] .danger').click();
ok((await g.locator('tbody tr').count()) === 6, 'and removes them');
await g.locator('.add .count').fill('3'); await g.locator('.add .rows').click();
ok((await g.locator('tbody tr').count()) === 9 && await cell(6, 'item').evaluate(e => e === document.activeElement), 'Add takes 3 rows at once and focuses the first');
await g.locator('.edit-columns').click(); await g.getByRole('tab', { name: 'Table', exact: true }).click();
await g.locator('.s-select[data-opt="cell"] button').click(); await p.locator('.s-menu .row', { hasText: '14' }).click();
ok((await cell(0, 'item').evaluate(e => getComputedStyle(e).fontSize)) === '14px', 'the values size control sets the cells to 14 px');
await g.getByRole('tab', { name: 'Column', exact: true }).click(); await g.locator('.edit-columns').click();
// a formula of several columns: Net's editor shows its three, a fourth joins with ×, which counts first
await cell(0, 'item').click(); await g.locator('tbody tr').nth(0).locator('td[data-k="net"]').click();
await g.locator('th[data-k="net"]').click({ button: 'right' });  // not in design mode: no column menu
ok(await p.locator('.s-menu.floating').count() === 0, 'outside design mode, a heading has no column menu');
await cell(0, 'net').click();
await g.locator('.edit-columns').click();
ok((await p.locator('.terms .term').count()) === 3, 'the formula editor lists Net = Total − Discount + Shipping');
ok(await g.locator('.terms .term').evaluateAll(terms => terms[0].getBoundingClientRect().top < terms[1].getBoundingClientRect().top && terms[0].getBoundingClientRect().left === terms[1].getBoundingClientRect().left), 'formula terms form an aligned vertical list');
await g.locator('.terms .term').first().locator('.s-select > button').click();
ok(await p.locator('body > .s-menu.floating').count() === 1 && await p.locator('body > .s-menu.floating').evaluate(menu => {
  const r = menu.getBoundingClientRect(), terms = document.querySelector('#order-grid .terms').getBoundingClientRect();
  const x = r.left + 10, y = Math.min(r.bottom - 10, terms.bottom + 10);
  return r.bottom > terms.bottom && menu.contains(document.elementFromPoint(x, y));
}), 'ribbon dropdowns float above the table beyond the formula scroll area');
ok(await p.locator('body > .s-menu.attached.floating').evaluate(menu => {
  const list = menu.getBoundingClientRect(), trigger = document.querySelector('#order-grid .terms .s-select.open > button'), field = trigger.getBoundingClientRect(), style = getComputedStyle(trigger);
  const above = menu.classList.contains('above');
  return Math.abs(list.left - field.left) < 1 && Math.abs(list.width - field.width) < 1 && Math.abs(above ? list.bottom - field.top : list.top - field.bottom) < 1 && (above ? style.borderTopColor : style.borderBottomColor) === 'rgba(0, 0, 0, 0)' && (above ? style.borderTopLeftRadius : style.borderBottomLeftRadius) === '0px';
}), 'floating dropdown preserves Shade’s continuous expanded border without a gap or separate card');
await p.keyboard.press('Escape');
ok(await p.locator('body > .s-menu.floating').count() === 0 && await g.locator('.terms .term').first().locator('.s-select > button').evaluate(e => e === document.activeElement), 'Escape closes the floating formula dropdown and restores focus');
await g.locator('.add-term').click();
const last = p.locator('.terms .term').last();
await last.locator('.s-select.op button').click(); await p.locator('.s-menu .row', { hasText: '×' }).click();
ok((await p.locator('.terms .term').count()) === 4, 'a fourth column joins the formula');
// design mode: the headings are the designer
await g.locator('.edit-columns').click();
await g.locator('[data-opt="design"]').click();
ok(await g.evaluate(e => e.classList.contains('design')) && await g.locator('.grid-toolbar .column').isEnabled(), 'Edit columns enables column actions and the header designer');
const heads = async () => (await g.locator('th[data-k] .h').allTextContents()).join('|');
await g.locator('th[data-k]').nth(0).scrollIntoViewIfNeeded();
const before = await heads(), third = g.locator('th[data-k]').nth(2);
let first = await g.locator('th[data-k]').nth(0).boundingBox(), tb = await third.boundingBox();
await p.mouse.move(tb.x + tb.width / 2, tb.y + tb.height / 2); await p.mouse.down(); await p.mouse.move(first.x + first.width, first.y + 14, { steps: 6 }); await p.mouse.up();
ok((await heads()) === before && !(await g.locator(':scope > .line').isVisible()) && await g.locator('th .grip').count() === 0, 'plain header drags do not reorder or put a grip over the label');
// Selecting a header can collapse the computed formula row above the grid.
first = await g.locator('th[data-k]').nth(0).boundingBox(); tb = await third.boundingBox();
await p.keyboard.down('Control');
await p.mouse.move(tb.x + tb.width / 2, tb.y + tb.height / 2); await p.mouse.down(); await p.mouse.move(tb.x + tb.width / 2 + 10, tb.y + 20, { steps: 3 });
await p.mouse.move(first.x + first.width, first.y + 14, { steps: 6 });
ok(await g.locator(':scope > .line').isVisible(), 'while dragging, the accent line marks the gap');
await p.mouse.up(); await p.keyboard.up('Control');
const moved = before.split('|'); moved.splice(1, 0, moved.splice(2, 1)[0]);
ok((await heads()) === moved.join('|'), 'Ctrl+drag from a heading middle lands in the gap: ' + await heads());
const cancelFrom = await g.locator('th[data-k]').nth(1).boundingBox(), cancelTo = await g.locator('th[data-k]').nth(0).boundingBox(), orderBeforeCancel = await heads();
await p.keyboard.down('Control'); await p.mouse.move(cancelFrom.x + 20, cancelFrom.y + 10); await p.mouse.down(); await p.mouse.move(cancelTo.x + 2, cancelTo.y + 10, { steps: 6 });
ok(await g.locator(':scope > .line').isVisible(), 'Ctrl starts the insertion preview');
await p.keyboard.up('Control'); await p.mouse.up();
ok(!(await g.locator(':scope > .line').isVisible()) && await p.locator('.s-ghost').count() === 0 && (await heads()) === orderBeforeCancel, 'releasing Ctrl cancels the preview and prevents a reorder');
ok(await g.locator('th .rz').first().evaluate(handle => { const h = handle.getBoundingClientRect(), cell = handle.parentElement.getBoundingClientRect(); return h.width === 12 && h.left >= cell.left && h.right <= cell.right; }), 'the 12px resize target stays inside its header');
ok(await g.locator('.add').evaluate(actions => { const table = actions.parentElement.querySelector('table').getBoundingClientRect(); return actions.getBoundingClientRect().top - table.bottom <= 12; }), 'Add rows stays beside the rendered table edge');
await g.locator('th[data-k]').nth(1).click({ button: 'right' });
await p.locator('.s-menu .row', { hasText: 'Insert column after' }).click();
ok((await g.locator('th[data-k]').nth(2).locator('.h').textContent()) === 'New column' && await g.locator('[data-col="title"]').evaluate(e => e === document.activeElement), 'Insert column after adds one and puts its name in the heading field');
await g.locator('th[data-k]').nth(2).focus(); await p.keyboard.press('Control+ArrowRight');
ok((await g.locator('th[data-k]').nth(3).locator('.h').textContent()) === 'New column', 'Ctrl+Right moves the focused column');
await g.locator('th[data-k]').nth(3).click({ button: 'right' }); await p.locator('.s-menu .row', { hasText: 'Remove column' }).click();
await p.locator('dialog.s-dialog[open] .danger').click();
await g.locator('th[data-k] .h').filter({ hasText: 'New column' }).waitFor({ state: 'detached' });
ok((await heads()) === moved.join('|'), 'Remove column asks, then removes it');
await g.locator('[data-opt="design"]').click();
ok(await g.locator('.grid-ribbon').isHidden() && await g.locator('.column').isDisabled(), 'Done closes the ribbon and disables structural actions');
await g.locator('.edit-columns').click();
await g.locator('th[data-k="qty"]').click({ position: { x: 10, y: 10 } });
ok(await g.locator('[data-col="type"]').evaluate(e => e.value()) === 'Value with unit', 'header selection keeps the real value-with-unit type');
const unitDefinitions = g.locator('[data-col="units"]'), storedUnit = await g.locator('tbody .unit').first().textContent();
await unitDefinitions.fill('kg, carton, kg'); await unitDefinitions.press('Tab');
ok(await unitDefinitions.inputValue() === 'kg, carton' && await g.locator('tbody .unit').first().textContent() === storedUnit, 'unit definitions normalize duplicates and preserve existing row units');
await g.locator('tbody .unit').first().click();
ok(await g.locator('tbody .unit').first().textContent() === 'kg', 'cycling a unit no longer defined starts at the new first unit');
await unitDefinitions.fill(' , '); await unitDefinitions.press('Tab');
ok(await unitDefinitions.getAttribute('aria-invalid') === 'true' && await g.locator('tbody .unit').first().textContent() === 'kg', 'an empty unit list is rejected without changing the table');
await unitDefinitions.fill('pcs, box, pallet'); await unitDefinitions.press('Tab');

await g.locator('.computed-column').click();
ok(await g.locator('th[data-k]').last().locator('.h').textContent() === 'Calculated column' && await g.locator('.terms .term').count() === 2, 'Calculated column creates a real formula and opens its ribbon controls');
await g.locator('[data-col="title"]').fill('Custom calculation');
await g.locator('.edit-columns').click(); await g.locator('.edit-columns').click();
ok(await g.locator('[data-col="title"]').inputValue() === 'Custom calculation', 'leaving edit mode retains column settings');
await g.getByRole('tab', { name: 'Width', exact: true }).click();
await g.locator('[data-col="sizing"] > button').click(); await p.locator('.s-menu .row', { hasText: /^Fixed$/ }).click();
await g.locator('[data-col="width"]').fill('160');
ok(Math.abs(await g.locator('th[data-k]').last().evaluate(e => e.getBoundingClientRect().width) - 160) < 2, 'the inline width field resizes the real column');
await g.getByRole('tab', { name: 'Text', exact: true }).click();
await g.locator('[data-col="style"] > button').click(); await p.locator('.s-menu .row', { hasText: /^Bold italic$/ }).click();
ok(await g.locator('tbody tr').first().locator('td[data-k]').last().evaluate(e => e.classList.contains('bold') && e.classList.contains('italic')), 'inline typography applies to table cells');
for (const name of ['Column', 'Text', 'Width', 'Table']) {
  await g.getByRole('tab', { name, exact: true }).click(); await p.waitForTimeout(250);
  ok(await g.locator('.grid-ribbon-tabs .line').evaluate(line => {
    const selected = line.parentElement.querySelector('[aria-selected="true"]').getBoundingClientRect(), underline = line.getBoundingClientRect();
    return Math.abs(underline.left - selected.left) < .05 && Math.abs(underline.right - selected.right) < .05;
  }), `the ${name} underline matches both tab edges without rounding`);
}
await g.getByRole('tab', { name: 'Text', exact: true }).focus(); await p.keyboard.press('ArrowRight');
await p.waitForTimeout(250);
ok(await g.locator('.grid-ribbon-tabs .line').evaluate(line => { const tab = line.parentElement.querySelector('[aria-selected="true"]'); return Math.abs(line.getBoundingClientRect().left - tab.getBoundingClientRect().left) < 1 && Math.abs(line.getBoundingClientRect().width - tab.getBoundingClientRect().width) < 1 && getComputedStyle(line, '::before').content === 'none'; }), 'ribbon uses the shared Navigation underline, aligned to its active tab without designer handles');
ok(await g.getByRole('tab', { name: 'Width', exact: true }).getAttribute('aria-selected') === 'true', 'ribbon tabs support arrow-key navigation');
await p.keyboard.press('Escape');
ok(await g.locator('.grid-ribbon').isHidden() && await g.locator('.edit-columns').evaluate(e => e === document.activeElement), 'Escape leaves edit mode and restores focus to its toggle');
// compact ribbon, bounded widths and paper layout operate on the real table.
await g.locator('.edit-columns').click();
ok(await g.locator('.edit-columns').evaluate(e => !e.classList.contains('ghost')) && await g.locator('.grid-toolbar .remove-column').count() === 1 && await g.locator('.grid-current').count() === 0, 'Done uses a standard action button; column actions are in the toolbar without a duplicate heading');
await g.getByRole('tab', { name: 'Width', exact: true }).click();
await g.locator('[data-col="min"]').fill('180');
await g.locator('[data-col="width"]').fill('80');
ok(await g.locator('[data-col="width"]').getAttribute('aria-invalid') === 'true' && await g.locator('th[data-k]').last().evaluate(e => e.getBoundingClientRect().width >= 180), 'a below-minimum fixed-width draft is rejected without shrinking the column');
await g.locator('[data-col="max"]').focus();
ok(await g.locator('[data-col="width"]').inputValue() === '180', 'leaving the fixed-width field restores its minimum');
await g.getByRole('tab', { name: 'Table', exact: true }).click();
ok(await g.locator('[data-opt="paper"]').evaluate(e => Math.abs(e.getBoundingClientRect().top - e.closest('[role="tabpanel"]').querySelector('[data-opt="head"]').getBoundingClientRect().top) < 2), 'paper format stays on the same row as table controls');
await g.locator('[data-opt="paper"] > button').click(); await p.locator('.s-menu .row', { hasText: /^A4$/ }).click();
ok((await g.locator('.grid-paper-note').textContent()).includes('210 × 297') && Math.abs(await g.locator('.scroll').evaluate(e => e.getBoundingClientRect().width) - 210 * 96 / 25.4) < 2, 'A4 sets the real preview to 210 mm');
await g.locator('[data-opt="orientation"] > button').click(); await p.locator('.s-menu .row', { hasText: /^Landscape$/ }).click();
ok((await g.locator('.grid-paper-note').textContent()).includes('297 × 210'), 'landscape swaps paper dimensions');
await g.locator('[data-opt="paper"] > button').click(); await p.locator('.s-menu .row', { hasText: /^Custom$/ }).click();
await g.locator('[data-opt="paperWidth"]').fill('250'); await g.locator('[data-opt="paperHeight"]').fill('350');
ok((await g.locator('.grid-paper-note').textContent()).includes('350 × 250') && (await g.locator('style').textContent()).includes('size: 350mm 250mm'), 'custom dimensions update both preview and named print page');
await g.locator('[data-opt="paperWidth"]').fill('-2');
ok(await g.locator('[data-opt="paperWidth"]').evaluate(e => !e.checkValidity()) && (await g.locator('style').textContent()).includes('size: 350mm 250mm'), 'invalid paper dimensions preserve the last accepted format');
await g.locator('[data-opt="paperWidth"]').fill('250');
await g.locator('[data-opt="paper"] > button').click(); await p.locator('.s-menu .row', { hasText: /^Screen$/ }).click();
await g.locator('.edit-columns').click();
// Published 0.6.3 ribbon feedback: localized drafts, completion, custom sizes and duplicate rich headings.
for (const culture of ['cs-CZ', 'en-US']) {
  const feedback = await b.newPage({ viewport: { width: 1440, height: 1000 } });
  feedback.on('pageerror', error => { ok(false, 'ribbon page error: ' + error.message); });
  await feedback.route('**/shade.js', async route => {
    await route.fulfill({ contentType: 'text/javascript', body: `const host = document.querySelector('#order-grid'); host.dataset.culture = '${culture}'; const cfg = JSON.parse(host.querySelector('script').textContent); cfg.columns[0].head = 20; cfg.columns[0].sub = 15.5; cfg.columns[3].title = cfg.columns[5].title = 'Cena'; cfg.columns[3].subtitle = 'prodejní / MJ'; cfg.columns[5].subtitle = 'nákupní / MJ'; host.querySelector('script').textContent = JSON.stringify(cfg);\n` + readFileSync(new URL('../docs/shade.js', import.meta.url), 'utf8') });
  });
  await feedback.goto(page);
  const grid = feedback.locator('#order-grid'), min = grid.locator('[data-col="min"]'), done = grid.locator('.edit-columns');
  const tab = name => grid.getByRole('tab', { name, exact: true });
  await done.click();
  for (const side of [2, 10]) {
    await grid.locator('th[data-k="sku"]').click({ position: { x: 10, y: 10 } });
    const handle = await grid.locator('th[data-k="price"] .rz').boundingBox(), heading = grid.locator('th[data-k="price"]'), width = await heading.evaluate(e => e.getBoundingClientRect().width);
    await feedback.mouse.move(handle.x + side, handle.y + handle.height / 2); await feedback.mouse.down(); await feedback.mouse.move(handle.x + side + 16, handle.y + handle.height / 2); await feedback.mouse.up();
    ok(await heading.evaluate(e => e.classList.contains('current') && e.getBoundingClientRect().width > 0) && Math.abs(await heading.evaluate(e => e.getBoundingClientRect().width) - width - 16) < 2, `${culture}: both ends of the resize target resize and select their owning column`);
  }
  await grid.locator('th[data-k="item"]').click({ position: { x: 10, y: 10 } }); await tab('Width').click(); await min.fill('60,5');
  ok(await min.evaluate(e => e.validationMessage === '') === (culture === 'cs-CZ'), `${culture}: decimal commas are localized or explicitly rejected in ribbon widths`);
  if (culture === 'en-US') await min.fill('60.5');
  await min.fill(culture === 'cs-CZ' ? '1\u00a0000' : '1,000');
  await tab('Column').click(); await done.click();
  ok(await tab('Width').getAttribute('aria-selected') === 'true' && await min.evaluate(e => e === document.activeElement && !!e.validationMessage), `${culture}: Done reveals an invalid draft in another pane and stays in edit mode`);
  await min.press('Escape');
  ok(await min.inputValue() === (culture === 'cs-CZ' ? '60,5' : '60.5') && await min.evaluate(e => !e.validationMessage) && await grid.locator('.grid-ribbon').isVisible(), `${culture}: Escape restores the applied width and keeps the ribbon open`);
  await min.fill('invalid'); await grid.locator('th[data-k="sku"]').click({ position: { x: 10, y: 10 } }); await grid.locator('th[data-k="item"]').click({ position: { x: 10, y: 10 } });
  ok(await min.inputValue() === (culture === 'cs-CZ' ? '60,5' : '60.5') && await min.evaluate(e => !e.validationMessage), `${culture}: changing columns discards an invalid column draft without altering the saved width`);
  await tab('Text').click();
  ok(await grid.locator('[data-col="head"]').evaluate(e => e.value()) === '20' && await grid.locator('[data-col="sub"]').evaluate(e => e.value()) === '15.5', `${culture}: typography choices display supplied custom sizes`);
  await grid.locator('[data-col="head"] > button').click();
  ok(await feedback.locator('.s-menu .row', { hasText: /^20$/ }).count() === 1, `${culture}: supplied custom size is also an actual selectable option`);
  await feedback.locator('.s-menu .row', { hasText: /^20$/ }).click();
  ok(await grid.locator('th[data-k="item"] .h').evaluate(e => parseFloat(getComputedStyle(e).fontSize)) === 20, `${culture}: selecting the displayed size preserves its rendered value`);
  await tab('Table').click(); await grid.locator('[data-opt="paper"] > button').click(); await feedback.locator('.s-menu .row', { hasText: /^Custom$/ }).click();
  const paperWidth = grid.locator('[data-opt="paperWidth"]'); await paperWidth.fill('240,5');
  ok(await paperWidth.evaluate(e => !e.validationMessage) === (culture === 'cs-CZ'), `${culture}: custom paper dimensions share the width numeric contract`);
  await paperWidth.fill('invalid'); await tab('Column').click(); await done.click();
  ok(await tab('Table').getAttribute('aria-selected') === 'true' && await paperWidth.evaluate(e => e === document.activeElement && e.value === 'invalid') && await grid.locator('.grid-paper-error').isVisible(), `${culture}: Done retains an invalid paper draft and shows its message`);
  await paperWidth.press('Escape'); await grid.locator('[data-opt="paper"] > button').click(); await feedback.locator('.s-menu .row', { hasText: /^Screen$/ }).click();
  await grid.locator('th[data-k="total"]').click({ position: { x: 10, y: 10 } }); await tab('Column').click();
  const source = () => grid.locator('.term').nth(1).locator('.s-select').last(); await source().locator('button').click();
  ok(await feedback.locator('.s-menu .row', { hasText: /^Cena · prodejní \/ MJ$/ }).count() === 1 && await feedback.locator('.s-menu .row', { hasText: /^Cena · nákupní \/ MJ$/ }).count() === 1, `${culture}: formula sources distinguish duplicate titles using subtitles`);
  await feedback.locator('.s-menu .row', { hasText: /^Cena · nákupní \/ MJ$/ }).click();
  const quantity = Number(await grid.locator('tbody tr').first().locator('[data-k="qty"] .v').textContent());
  ok(Number(await grid.locator('tbody tr').first().locator('[data-k="total"] .v').textContent()) === quantity * 2, `${culture}: the rich formula label selects the correct source identity`);
  await grid.locator('th[data-k="discount"]').click({ position: { x: 10, y: 10 } }); await tab('Text').click(); await grid.locator('[data-col="subtitle"]').fill('prodejní / MJ');
  await grid.locator('th[data-k="total"]').click({ position: { x: 10, y: 10 } }); await tab('Column').click(); await source().locator('button').click();
  ok(await feedback.locator('.s-menu .row', { hasText: /^Cena · prodejní \/ MJ \(4\)$/ }).count() === 1 && await feedback.locator('.s-menu .row', { hasText: /^Cena · prodejní \/ MJ \(6\)$/ }).count() === 1, `${culture}: identical rich headings fall back to short table positions`);
  await feedback.keyboard.press('Escape'); await done.click();
  ok(await grid.locator('.grid-ribbon').isHidden(), `${culture}: valid ribbon settings allow Done`);
  await feedback.close();
}
// Screen geometry is independent of document paper, and adaptive layouts retain actual controls/drafts.
{
  const layoutPage = await b.newPage({ viewport: { width: 1664, height: 1200 } });
  layoutPage.on('pageerror', error => ok(false, 'layout page error: ' + error.message));
  await layoutPage.route('**/shade.js', route => route.fulfill({ contentType: 'text/javascript', body: `const host = document.querySelector('#order-grid'); host.dataset.culture = 'cs-CZ'; const cfg = JSON.parse(host.querySelector('script').textContent); cfg.columns[0].min = 330.0354228182; Object.assign(cfg.columns[1], { sizing: 'fixed', width: 400.034, min: 330.034, max: 700.0354228182 }); cfg.options = { paperDimensionsPlacement: 'FormatLabel', paper: 'A4', orientation: 'Landscape', constrain: false, paperWidth: 252.0354228182, paperHeight: 297.034 }; host.querySelector('script').textContent = JSON.stringify(cfg);\n` + readFileSync(new URL('../docs/shade.js', import.meta.url), 'utf8') }));
  await layoutPage.goto(page); await layoutPage.evaluate(() => { document.querySelectorAll('.wrap').forEach(e => e.style.maxWidth = 'none'); return document.fonts.ready; });
  const grid = layoutPage.locator('#order-grid'), tab = name => grid.getByRole('tab', { name, exact: true });
  const selectPane = async name => grid.evaluate((element, name) => {
    const tab = [...element.querySelectorAll('[role="tab"]')].find(tab => tab.textContent === name);
    if (tab && tab.getClientRects().length) tab.click();
  }, name);
  await grid.locator('.edit-columns').click(); await selectPane('Table');
  await layoutPage.addStyleTag({ content: '.rail { visibility: hidden; }' });
  for (const width of [960, 2400]) {
    await layoutPage.setViewportSize({ width, height: 1200 });
    await selectPane('Table');
    for (const theme of ['dark', 'light']) {
      await layoutPage.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
      await grid.screenshot({ path: `docs/preview/${theme}/editable-table-paper-${width}-web.png` });
    }
  }
  await layoutPage.evaluate(() => document.documentElement.dataset.theme = 'dark');
  await layoutPage.setViewportSize({ width: 1664, height: 1200 });

  ok(await grid.locator('.grid-paper-note').isHidden() && (await grid.locator('.grid-field-paper').textContent()).includes('Paper format \u00b7 297 \u00d7 210 mm'), 'compact dimensions appear beside the format caption without a bottom row');
  ok(await grid.locator('[data-opt="paper"]').evaluate(e => e.value()) === 'A4' && await grid.locator('[data-opt="orientation"]').evaluate(e => e.value()) === 'Landscape' && await grid.locator('.scroll').evaluate(e => e.style.width === '') && (await grid.locator('style').textContent()).includes('size: 297mm 210mm'), 'full-width screen editing preserves A4 landscape print settings');
  await selectPane('Width'); const min = grid.locator('[data-col="min"]');
  ok(await min.inputValue() === '330,04', 'applied ribbon widths use compact localized precision');
  await min.focus(); await selectPane('Text'); await selectPane('Width');
  ok(await min.inputValue() === '330,04', 'focus, blur and tab changes retain the friendly applied width display');
  await grid.locator('th[data-k="sku"]').click({ position: { x: 10, y: 10 } });
  const width = grid.locator('[data-col="width"]'); await width.focus(); await grid.locator('.edit-columns').click();
  ok(await grid.locator('.grid-ribbon').isHidden(), 'Done accepts an untouched rounded fixed-width display');
  await grid.locator('.edit-columns').click(); await selectPane('Width'); await width.fill('330,032');
  ok(await width.evaluate(e => !!e.validationMessage), 'the exact stored minimum remains enforced after showing its rounded display');
  await width.press('Escape'); await width.fill('700,038');
  ok(await width.evaluate(e => !!e.validationMessage), 'the exact stored maximum remains enforced after showing its rounded display');
  await width.press('Escape');
  await width.fill('330,03');
  ok(await width.evaluate(e => !!e.validationMessage), 'an explicit edit matching the rounded display still validates the exact minimum');
  await width.press('Escape');
  await grid.locator('th[data-k="item"]').click({ position: { x: 10, y: 10 } }); await selectPane('Width'); await min.fill('invalid'); await min.evaluate(e => { e.setSelectionRange(3, 3); window.ribbonDraft = e; });
  for (const width of [2400, 1600, 1280, 960, 840, 2400]) {
    await layoutPage.setViewportSize({ width: width + 64, height: 1200 }); await layoutPage.waitForTimeout(50);
    ok(await min.evaluate(e => e === window.ribbonDraft && e === document.activeElement && e.selectionStart === 3 && e.value === 'invalid' && !!e.validationMessage), `adaptive web ribbon at ${width}px retains control identity, focus, caret and invalid draft`);
  }
  await min.press('Escape');
  ok(await grid.locator('.grid-group-label:visible').allTextContents().then(names => ['Column', 'Column text', 'Column width', 'Table defaults and paper'].every(name => names.includes(name))), 'wide web ribbons expose labelled per-column groups and table defaults');
  await grid.locator('[data-opt="paper"]').scrollIntoViewIfNeeded();
  const paperPosition = await grid.locator('[data-opt="paper"]').boundingBox();
  await grid.locator('[data-opt="paper"] > button').click();
  ok(await layoutPage.locator('.s-menu .row', { hasText: /^A4$/ }).isVisible() && await grid.locator('.grid-group-label:visible').allTextContents().then(names => names.includes('Column text')) && Math.abs((await grid.locator('[data-opt="paper"]').boundingBox()).y - paperPosition.y) < 1, 'opening a paper dropdown keeps the wide layout stable and its overlay open');
  await layoutPage.keyboard.press('Escape');
  for (const width of [840, 960, 1280, 1600, 2400]) {
    await layoutPage.setViewportSize({ width: width + 64, height: 1200 }); await selectPane('Table'); await layoutPage.waitForTimeout(50);
    ok(await grid.locator('.grid-ribbon-panel').last().evaluate(panel => {
      const fields = [...panel.children], paper = panel.querySelector('.grid-paper-controls'), left = fields.filter(e => e !== paper), required = left.reduce((sum, e) => sum + e.getBoundingClientRect().width, 0) + paper.getBoundingClientRect().width + left.length * 16;
      if (required > panel.getBoundingClientRect().width) return true;
      return fields.every(e => Math.abs(e.getBoundingClientRect().top - fields[0].getBoundingClientRect().top) < 1);
    }), `${width}px: typography, sorting and paper share the row whenever the measured controls fit`);
  }
  await grid.locator('[data-opt="paper"] > button').click(); await layoutPage.locator('.s-menu .row', { hasText: /^Custom$/ }).click();
  for (const mode of ['dark', 'light']) {
    await layoutPage.evaluate(mode => document.documentElement.dataset.theme = mode, mode);
    for (const keyboard of [false, true]) for (const name of ['Column', 'Text', 'Width', 'Table']) {
      for (const target of [grid.locator('[data-col="title"]'), grid.locator('th[data-k="item"]'), grid.locator('.edit-columns')]) {
        await grid.locator('.edit-columns').focus();
        await layoutPage.setViewportSize({ width: 904, height: 1200 }); await layoutPage.waitForTimeout(50);
        if (keyboard) { await tab(name).focus(); await tab(name).press('Enter'); } else await tab(name).click();
        await layoutPage.setViewportSize({ width: 4864, height: 1200 }); await layoutPage.waitForTimeout(50);
        ok(await grid.locator('.grid-ribbon-tabs').isHidden() && await grid.locator('.grid-ribbon-panel').nth(['Column', 'Text', 'Width', 'Table'].indexOf(name)).evaluate(e => e.contains(document.activeElement)), `${mode} ${name} keyboard=${keyboard}: expansion moves focus into the selected group and hides tabs`);
        await target.focus();
        await layoutPage.waitForTimeout(0);
        ok(await grid.locator('.grid-ribbon-tabs').isHidden() && await target.evaluate(e => e === document.activeElement), `${mode} ${name} keyboard=${keyboard}: leaving web tabs hides the expanded strip without losing focus`);
      }
    }
    await grid.locator('th[data-k="net"]').click({ position: { x: 10, y: 10 } });
    for (const width of [960, 4800]) {
      await layoutPage.setViewportSize({ width: width + 64, height: 1200 }); await layoutPage.waitForTimeout(50); await selectPane('Column');
      ok(await grid.locator('.grid-formula').evaluate(e => e.querySelector('.add-term').getBoundingClientRect().bottom + 4 <= e.querySelector('.term').getBoundingClientRect().top + 0.5), `${mode} at ${width}px: Add term reserves its height above the first formula source`);
    }
    await grid.locator('th[data-k="item"]').click({ position: { x: 10, y: 10 } });
  }
  ok(await grid.locator('[data-opt="paperWidth"]').inputValue() === '252,04', 'custom paper dimensions show friendly precision');
  await grid.locator('[data-opt="paperWidth"]').focus(); await grid.locator('.edit-columns').click();
  ok((await grid.locator('style').textContent()).includes('size: 297.034mm 252.0354228182mm'), 'displaying, blurring and finishing custom paper fields retains exact print dimensions');
  const value = grid.locator('tbody tr').first().locator('[data-k="item"] .v'); await value.click(); await value.evaluate(e => { window.cellDraft = e; window.tableBody = e.closest('tbody'); });
  for (const width of [840, 2400]) { await layoutPage.setViewportSize({ width: width + 64, height: 1200 }); await layoutPage.waitForTimeout(50); }
  ok(await value.evaluate(e => e === window.cellDraft && e === document.activeElement && e.closest('tbody') === window.tableBody), 'full-width resizing retains the active web cell and table body');
  await grid.locator('[data-opt="constrain"]').evaluate(e => { e.set(['Paper preview', 'Full width'], 'Paper preview'); e.dispatchEvent(new CustomEvent('change', { detail: { value: 'Paper preview' } })); });
  ok(await value.evaluate(e => e === window.cellDraft && e === document.activeElement) && await grid.locator('.scroll').evaluate(e => !!e.style.width) && (await grid.locator('style').textContent()).includes('size: 297.034mm 252.0354228182mm'), 'paper preview changes the viewport without rebuilding the active cell or changing document dimensions');
  await layoutPage.close();
}
// the mode switch: System follows the browser's setting, every switch shows the choice
await p.emulateMedia({ colorScheme: 'light' });
await p.locator('.s-mode >> nth=1 >> button[data-mode="system"]').click();
ok((await p.evaluate(() => document.documentElement.dataset.theme)) === 'light', 'System follows a light setting');
await p.emulateMedia({ colorScheme: 'dark' });
ok(await p.waitForFunction(() => document.documentElement.dataset.theme === 'dark', null, { timeout: 2000 }).then(() => true, () => false), 'and switches when the setting changes');
ok((await p.locator('.s-mode >> nth=0 >> button[data-mode="system"]').getAttribute('aria-checked')) === 'true', 'the top bar shows the same choice');
await p.locator('.s-mode >> nth=0 >> button[data-mode="dark"]').click();
// summary cards: one filter on at a time
const cardsRow = p.locator('#data-status .s-cards').first().locator('.s-card');
await cardsRow.nth(0).click();
ok((await cardsRow.nth(0).getAttribute('aria-pressed')) === 'true' && (await cardsRow.nth(3).getAttribute('aria-pressed')) === 'false', 'a summary card filters, one at a time');
// search: filters the list, counts, clears with Esc
const search = p.locator('.s-search input');
await search.fill('web');
ok((await p.locator('.s-search .count').textContent()) === '3 of 5' && await p.locator('.s-search .clear').isVisible(), 'search counts its matches and offers ×');
ok((await p.locator('#search-list mark.s-hit').allTextContents()).join('|') === 'Web|Web|Web', 'search highlights the matched letters');
await search.press('Escape');
ok((await search.inputValue()) === '' && !(await p.locator('.s-search .clear').isVisible()), 'Esc clears the search');
// progress: every fill is as tall as its track, so its ends are round (a page style once made it 12 px)
ok(await p.evaluate(() => [...document.querySelectorAll('.s-progress .bar')].every(b => b.querySelector('i').getBoundingClientRect().height === b.getBoundingClientRect().height)), 'progress fills match their track');
// reference card: an example fills its field
await p.locator('.s-ref [data-use="0 0 1 * *"]').click();
ok((await p.locator('#cron').inputValue()) === '0 0 1 * *', 'a reference example fills the field');
// sidebar branding reserves the mark and keeps configured expanded geometry.
await shell.locator('.s-sidebar').evaluate(sidebar => { sidebar.style.setProperty('--sidebar-wide', '260px'); sidebar.querySelector('.name').textContent = 'Potraviny levně – objednávky pro každého'; });
await p.waitForTimeout(500);
ok(await shell.locator('.s-sidebar').evaluate(sidebar => { const text = sidebar.querySelector('.name').getBoundingClientRect(), logo = sidebar.querySelector('.s-mark').getBoundingClientRect(), header = sidebar.querySelector('.brand').getBoundingClientRect(); return text.right <= logo.left && header.height >= text.height; }), 'long localized web branding wraps without overlapping its mark');
await shell.locator('.brand').click(); await p.waitForTimeout(500); await shell.locator('.brand').click(); await p.waitForTimeout(500);
ok(Math.round(await shell.locator('.s-sidebar').evaluate(sidebar => sidebar.getBoundingClientRect().width)) === 260, 'web sidebar collapse and expand keep the configured width');
// tray menu
await p.locator('.s-taskbar .tray').click();
await p.waitForTimeout(200);
const trayScroll = await p.evaluate(() => scrollY);
await p.locator('.s-taskbar .s-menu .row').nth(2).hover();
ok(Math.abs((await p.evaluate(() => scrollY)) - trayScroll) < 0.1, 'hovering tray menu items does not scroll the masonry page');
ok(await p.locator('.s-taskbar .s-menu .row').count() === 5, 'the tray icon opens its menu');
await p.locator('.s-taskbar .s-menu .row').nth(2).click();
ok(await p.locator('.s-taskbar .s-menu').count() === 0, 'a pick closes the tray menu');
ok(Math.abs((await p.evaluate(() => scrollY)) - trayScroll) < 0.1, 'choosing a tray item does not jump the masonry page');
// the top bar: a group menu opens and its chapter link jumps there; GitHub is linked
await p.locator('.navgroup').nth(1).locator('button').click();
ok(await p.locator('.navgroup.open .navmenu a').count() === 3, 'the Components menu lists its three chapters');
await p.locator('.navgroup.open .navmenu a').nth(1).click();
ok(!(await p.locator('.navgroup.open').count()) && (await p.evaluate(() => location.hash)) === '#data-status', 'a chapter link closes the menu and jumps');
ok((await p.locator('header.top a.gh').getAttribute('href')) === 'https://github.com/jurasekdominik/shade', 'the GitHub link is in the top bar');
// the chapter rail: one stroke a chapter, the one on screen marked, and a click jumps there
ok(await p.locator('.rail li a').count() === 8, 'the rail has a stroke for each chapter');
await p.locator('.rail li a').nth(6).click(); await p.waitForTimeout(900);
ok((await p.locator('.rail li a[aria-current]').getAttribute('href')) === '#navigation' && (await p.locator('.rail .num').textContent()).startsWith('07'), 'a stroke jumps to its chapter and the rail follows');
// mode
await p.locator('.s-mode >> nth=0 >> button[data-mode="light"]').click();
ok((await p.evaluate(() => getComputedStyle(document.body).backgroundColor)) === 'rgb(244, 244, 247)', 'light mode');
// responsive measure
console.log('     measure:', await p.locator('#measure').textContent());
await p.evaluate(() => { document.querySelector('.resizer').style.width = '330px'; });
await p.waitForTimeout(200);
console.log('     measure after resize:', await p.locator('#measure').textContent());
ok(errors.length === 0, 'no script errors ' + errors.join(' | '));
const ph = await b.newPage({ viewport: { width: 390, height: 900 } });
await ph.goto(page); await ph.waitForTimeout(400);
ok(!(await ph.evaluate(() => document.documentElement.scrollWidth > innerWidth)), 'no sideways scroll on a phone');
for (const width of [320, 360, 390, 760]) {
  await ph.setViewportSize({ width, height: 900 });
  for (const theme of ['dark', 'light']) {
    await ph.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
    ok(await ph.locator('.s-mode').evaluateAll(groups => groups.every(group => { const r = group.getBoundingClientRect(); const last = group.lastElementChild.getBoundingClientRect(); return last.right <= r.right + 1 && r.width <= innerWidth; })), `every mode option fits at ${width}px in ${theme}`);
  }
}
await popupContracts(b, page, ok);
await b.close();
if (failed) { console.log(failed + ' failed'); process.exit(1); }
