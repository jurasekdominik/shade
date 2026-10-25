// The live page's components, used the way a person would: Node + Playwright, `node gallery/page-check.mjs`.
// Fails (exit 1) when a component stops working or the page logs an error.
import { chromium } from 'playwright';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
const page = pathToFileURL(resolve(process.argv[2] || 'docs/index.html')).href;
let failed = 0;
const b = await chromium.launch(process.env.CHROMIUM ? { executablePath: process.env.CHROMIUM } : {});
const p = await b.newPage({ viewport: { width: 1280, height: 900 } });
const errors = []; p.on('pageerror', e => errors.push(e.message)); p.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
await p.addInitScript(() => localStorage.setItem('shade-theme', 'dark'));
await p.goto(page);
const ok = (cond, what) => { if (!cond) failed++; console.log((cond ? 'ok   ' : 'FAIL ') + what); };
// the page's structure: every subsection sits in its chapter's column (a stray closing tag once pushed one out to the window's edge)
ok(await p.evaluate(() => [...document.querySelectorAll('.sub')].every(s => s.parentElement.classList.contains('wrap'))), 'every subsection is inside its chapter');
// dropdown: open, arrow down, enter
const sel = p.locator('.s-select').nth(1);
await sel.locator('button').click();
ok(await sel.locator('.s-menu.attached .row').count() === 4, 'dropdown opens with its 4 items');
ok(await sel.evaluate(e => e.classList.contains('open')), 'field and list are one shape (open class)');
await p.keyboard.press('ArrowDown'); await p.keyboard.press('Enter');
ok((await sel.locator('button span').textContent()) === 'Research', 'arrow + Enter picks Research');
// multi
const multi = p.locator('.s-select[data-multi]');
await multi.locator('button').click(); await multi.locator('.row').first().click(); await multi.locator('.row').nth(1).click();
ok((await multi.locator('button span').textContent()) === 'Anna, Ben', 'multi choice shows the picks');
await p.keyboard.press('Escape');
ok(await multi.locator('.s-menu').count() === 0, 'Esc closes');
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
await p.locator('.s-tag [data-remove]').click(); ok(await p.locator('.s-tag').filter({ hasText: 'report-jan' }).count() === 0, 'tag removes');
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
await row.locator('.hex').fill('#3366FF');
ok((await accentNow()) === '#3366FF', 'a typed colour becomes the accent');
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
const cell = (r, k) => g.locator('tbody tr').nth(r).locator(`td[data-k="${k}"] .v`);
const foot = k => g.locator(`tfoot td[data-k="${k}"] b`).textContent();
await cell(0, 'qty').click(); await p.keyboard.press('Control+A'); await p.keyboard.type('10');
ok((await cell(0, 'total').textContent()) === '49.00' && (await cell(0, 'net').textContent()) === '48.20', 'an edit recomputes Total = Quantity × Price and Net = Total − Discount + Shipping');
ok((await foot('total')) === '281.50', 'and the footer sum follows');
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
const items = async () => (await g.locator('tbody td[data-k="item"] .v').allTextContents()).join('|');
const original = await items();
await g.locator('th[data-k="price"]').click({ position: { x: 10, y: 10 } });
const ps = (await g.locator('tbody td[data-k="price"] .v').allTextContents()).map(Number);
ok(ps.every((v, i, a) => !i || a[i - 1] <= v), 'a heading click sorts ascending');
await g.locator('th[data-k="price"]').click({ position: { x: 10, y: 10 } }); await g.locator('th[data-k="price"]').click({ position: { x: 10, y: 10 } });
ok((await items()) === original, 'the third click returns to the original order');
await p.locator('[data-opt="sorting"]').click();
await g.locator('th[data-k="price"]').click({ position: { x: 10, y: 10 } });
ok((await items()) === original && !(await g.locator('th[aria-sort]').count()), 'with sorting off, a heading click does not sort');
await p.locator('[data-opt="sorting"]').click();
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
await p.locator('.s-select[data-opt="cell"] button').click(); await p.locator('.s-menu .row', { hasText: '14' }).click();
ok((await cell(0, 'item').evaluate(e => getComputedStyle(e).fontSize)) === '14px', 'the values size control sets the cells to 14 px');
// a formula of several columns: Net's editor shows its three, a fourth joins with ×, which counts first
await cell(0, 'item').click(); await g.locator('tbody tr').nth(0).locator('td[data-k="net"]').click();
await g.locator('th[data-k="net"]').click({ button: 'right' });  // not in design mode: no column menu
ok(await p.locator('.s-menu.floating').count() === 0, 'outside design mode, a heading has no column menu');
await cell(0, 'net').click();
ok((await p.locator('.terms .term').count()) === 3, 'the formula editor lists Net = Total − Discount + Shipping');
await p.locator('.terms > button', { hasText: 'Add a column' }).click();
const last = p.locator('.terms .term').last();
await last.locator('.s-select.op button').click(); await p.locator('.s-menu .row', { hasText: '×' }).click();
ok((await p.locator('.terms .term').count()) === 4, 'a fourth column joins the formula');
// design mode: the headings are the designer
await p.locator('[data-opt="design"]').click();
ok(await g.evaluate(e => e.classList.contains('design')) && await g.locator('.add .column').isVisible(), 'Design columns turns the headings into the designer');
const heads = async () => (await g.locator('th[data-k] .h').allTextContents()).join('|');
await g.locator('th[data-k]').nth(0).scrollIntoViewIfNeeded();
const before = await heads(), first = await g.locator('th[data-k]').nth(0).boundingBox(), third = g.locator('th[data-k]').nth(2);
const tb = await third.boundingBox();
await p.mouse.move(tb.x + tb.width / 2, tb.y + tb.height / 2); await p.mouse.down(); await p.mouse.move(tb.x + tb.width / 2 + 10, tb.y + 20, { steps: 3 });
await p.mouse.move(first.x + first.width, first.y + 14, { steps: 6 });
ok(await g.locator('.line').isVisible(), 'while dragging, the accent line marks the gap');
await p.mouse.up();
const moved = before.split('|'); moved.splice(1, 0, moved.splice(2, 1)[0]);
ok((await heads()) === moved.join('|'), 'a heading dragged from its middle lands in the gap: ' + await heads());
await g.locator('th[data-k]').nth(1).click({ button: 'right' });
await p.locator('.s-menu .row', { hasText: 'Insert column after' }).click();
ok((await g.locator('th[data-k]').nth(2).locator('.h').textContent()) === 'New column' && await p.locator('[data-col="title"]').evaluate(e => e === document.activeElement), 'Insert column after adds one and puts its name in the heading field');
await g.locator('th[data-k]').nth(2).focus(); await p.keyboard.press('Control+ArrowRight');
ok((await g.locator('th[data-k]').nth(3).locator('.h').textContent()) === 'New column', 'Ctrl+Right moves the focused column');
await g.locator('th[data-k]').nth(3).click({ button: 'right' }); await p.locator('.s-menu .row', { hasText: 'Remove column' }).click();
await p.locator('dialog.s-dialog[open] .danger').click();
ok((await heads()) === moved.join('|'), 'Remove column asks, then removes it');
await p.locator('[data-opt="design"]').click();
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
// tray menu
await p.locator('.s-taskbar .tray').click();
ok(await p.locator('.s-taskbar .s-menu .row').count() === 5, 'the tray icon opens its menu');
await p.locator('.s-taskbar .s-menu .row').nth(2).click();
ok(await p.locator('.s-taskbar .s-menu').count() === 0, 'a pick closes the tray menu');
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
await b.close();
if (failed) { console.log(failed + ' failed'); process.exit(1); }
