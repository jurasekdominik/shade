// The live page's components, used the way a person would: Node + Playwright, `node gallery/page-check.mjs`.
// Fails (exit 1) when a component stops working or the page logs an error.
import { chromium } from 'playwright';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
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
await multi.locator(':scope > button').click(); await multi.locator('.row').first().click(); await multi.locator('.row').nth(1).click();
ok((await multi.locator(':scope > button span').textContent()) === 'Anna, Ben', 'multi choice shows the picks');
await p.keyboard.press('Escape');
ok(await multi.locator('.s-menu').count() === 0, 'Esc closes');
ok(await multi.locator(':scope > button').evaluate(b => b === document.activeElement), 'multi-choice Escape restores trigger focus');
await multi.evaluate(e => { e.changes = []; e.addEventListener('change', event => e.changes.push(event.detail.values)); });
await multi.locator(':scope > button').press('ArrowDown');
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
await p.locator('[data-col="title"]').fill('Article');
ok((await g.locator('th[data-k="item"] .h').textContent()) === 'Article', 'the column controls rename the focused column');
await cell(0, 'price').click();
ok(await p.locator('[data-when="computed"]').isHidden(), 'the formula shows only for a computed column');
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
await p.locator('.s-select[data-col="type"] button').click(); await p.locator('.s-menu .row', { hasText: 'General' }).click();
await cell(1, 'sku').click(); await p.keyboard.press('Control+A'); await p.keyboard.type('042');
ok(await g.locator('tbody tr').nth(1).locator('td[data-k="sku"]').evaluate(e => e.classList.contains('num')) && !(await g.locator('tbody tr').nth(0).locator('td[data-k="sku"]').evaluate(e => e.classList.contains('num'))) && (await cell(1, 'sku').textContent()) === '042', 'a general column: 042 sits right as typed, a code left');
await p.keyboard.press('Control+A'); await p.keyboard.type('PEN-BL-10');
await p.locator('.s-select[data-col="type"] button').click(); await p.locator('.s-menu .row', { hasText: /^Text$/ }).click();
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
await p.locator('.s-select[data-opt="cell"] button').click(); await p.locator('.s-menu .row', { hasText: '14' }).click();
ok((await cell(0, 'item').evaluate(e => getComputedStyle(e).fontSize)) === '14px', 'the values size control sets the cells to 14 px');
await g.getByRole('tab', { name: 'Column', exact: true }).click(); await g.locator('.edit-columns').click();
// a formula of several columns: Net's editor shows its three, a fourth joins with ×, which counts first
await cell(0, 'item').click(); await g.locator('tbody tr').nth(0).locator('td[data-k="net"]').click();
await g.locator('th[data-k="net"]').click({ button: 'right' });  // not in design mode: no column menu
ok(await p.locator('.s-menu.floating').count() === 0, 'outside design mode, a heading has no column menu');
await cell(0, 'net').click();
await g.locator('.edit-columns').click();
ok((await p.locator('.terms .term').count()) === 3, 'the formula editor lists Net = Total − Discount + Shipping');
await p.locator('.terms > button', { hasText: 'Add a column' }).click();
const last = p.locator('.terms .term').last();
await last.locator('.s-select.op button').click(); await p.locator('.s-menu .row', { hasText: '×' }).click();
ok((await p.locator('.terms .term').count()) === 4, 'a fourth column joins the formula');
// design mode: the headings are the designer
await g.locator('.edit-columns').click();
await p.locator('[data-opt="design"]').click();
ok(await g.evaluate(e => e.classList.contains('design')) && await g.locator('.grid-toolbar .column').isEnabled(), 'Edit columns enables column actions and the header designer');
const heads = async () => (await g.locator('th[data-k] .h').allTextContents()).join('|');
await g.locator('th[data-k]').nth(0).scrollIntoViewIfNeeded();
const before = await heads(), third = g.locator('th[data-k]').nth(2);
let first = await g.locator('th[data-k]').nth(0).boundingBox(), tb = await third.boundingBox();
await p.mouse.move(tb.x + tb.width / 2, tb.y + tb.height / 2); await p.mouse.down(); await p.mouse.move(first.x + first.width, first.y + 14, { steps: 6 }); await p.mouse.up();
ok((await heads()) === before && !(await g.locator('.line').isVisible()) && await g.locator('th .grip').count() === 0, 'plain header drags do not reorder or put a grip over the label');
// Selecting a header can collapse the computed formula row above the grid.
first = await g.locator('th[data-k]').nth(0).boundingBox(); tb = await third.boundingBox();
await p.keyboard.down('Control');
await p.mouse.move(tb.x + tb.width / 2, tb.y + tb.height / 2); await p.mouse.down(); await p.mouse.move(tb.x + tb.width / 2 + 10, tb.y + 20, { steps: 3 });
await p.mouse.move(first.x + first.width, first.y + 14, { steps: 6 });
ok(await g.locator('.line').isVisible(), 'while dragging, the accent line marks the gap');
await p.mouse.up(); await p.keyboard.up('Control');
const moved = before.split('|'); moved.splice(1, 0, moved.splice(2, 1)[0]);
ok((await heads()) === moved.join('|'), 'Ctrl+drag from a heading middle lands in the gap: ' + await heads());
const cancelFrom = await g.locator('th[data-k]').nth(1).boundingBox(), cancelTo = await g.locator('th[data-k]').nth(0).boundingBox(), orderBeforeCancel = await heads();
await p.keyboard.down('Control'); await p.mouse.move(cancelFrom.x + 20, cancelFrom.y + 10); await p.mouse.down(); await p.mouse.move(cancelTo.x + 2, cancelTo.y + 10, { steps: 6 });
ok(await g.locator('.line').isVisible(), 'Ctrl starts the insertion preview');
await p.keyboard.up('Control'); await p.mouse.up();
ok(!(await g.locator('.line').isVisible()) && await p.locator('.s-ghost').count() === 0 && (await heads()) === orderBeforeCancel, 'releasing Ctrl cancels the preview and prevents a reorder');
ok(await g.locator('th .rz').first().evaluate(handle => { const h = handle.getBoundingClientRect(), cell = handle.parentElement.getBoundingClientRect(); return h.width === 12 && h.left >= cell.left && h.right <= cell.right; }), 'the 12px resize target stays inside its header');
ok(await g.locator('.add').evaluate(actions => { const table = actions.parentElement.querySelector('table').getBoundingClientRect(); return actions.getBoundingClientRect().top - table.bottom <= 12; }), 'Add rows stays beside the rendered table edge');
await g.locator('th[data-k]').nth(1).click({ button: 'right' });
await p.locator('.s-menu .row', { hasText: 'Insert column after' }).click();
ok((await g.locator('th[data-k]').nth(2).locator('.h').textContent()) === 'New column' && await p.locator('[data-col="title"]').evaluate(e => e === document.activeElement), 'Insert column after adds one and puts its name in the heading field');
await g.locator('th[data-k]').nth(2).focus(); await p.keyboard.press('Control+ArrowRight');
ok((await g.locator('th[data-k]').nth(3).locator('.h').textContent()) === 'New column', 'Ctrl+Right moves the focused column');
await g.locator('th[data-k]').nth(3).click({ button: 'right' }); await p.locator('.s-menu .row', { hasText: 'Remove column' }).click();
await p.locator('dialog.s-dialog[open] .danger').click();
await g.locator('th[data-k] .h').filter({ hasText: 'New column' }).waitFor({ state: 'detached' });
ok((await heads()) === moved.join('|'), 'Remove column asks, then removes it');
await p.locator('[data-opt="design"]').click();
ok(await g.locator('.grid-ribbon').isHidden() && await g.locator('.column').isDisabled(), 'Done closes the ribbon and disables structural actions');
await g.locator('.edit-columns').click();
await g.locator('th[data-k="qty"]').click({ position: { x: 10, y: 10 } });
ok(await g.locator('[data-col="type"]').evaluate(e => e.value()) === 'Value with unit', 'header selection keeps the real value-with-unit type');
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
await g.getByRole('tab', { name: 'Text', exact: true }).focus(); await p.keyboard.press('ArrowRight');
ok(await g.getByRole('tab', { name: 'Width', exact: true }).getAttribute('aria-selected') === 'true', 'ribbon tabs support arrow-key navigation');
await p.keyboard.press('Escape');
ok(await g.locator('.grid-ribbon').isHidden() && await g.locator('.edit-columns').evaluate(e => e === document.activeElement), 'Escape leaves edit mode and restores focus to its toggle');
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
await b.close();
if (failed) { console.log(failed + ' failed'); process.exit(1); }
