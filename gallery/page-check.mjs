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
ok(await p.locator('.s-suggest .row').count() === 2, 'typing ann suggests 2');
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
await p.locator('.mode button[data-mode="light"]').click();
ok((await accentNow()) === (await teal.getAttribute('data-light')), 'switching to light takes its light value');
await p.locator('.mode button[data-mode="dark"]').click();
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
// the date field: typed, picked from the month, the keyboard, weekends marked
const dateBox = p.locator('.s-date').first(), dateInput = dateBox.locator('input');
await dateInput.fill('24.10.2026'); await dateInput.press('Enter');
ok((await dateInput.inputValue()) === '2026-10-24', 'a typed day shows as yyyy-MM-dd');
await dateBox.locator('.cal').click();
ok(await dateBox.locator('.s-month').isVisible() && (await dateBox.locator('.s-month .day.picked').textContent()) === '24', 'the icon opens the month on the day');
ok((await dateBox.locator('.s-month .day.we').count()) === 12 && (await dateBox.locator('.s-month .band').count()) === 2, 'weekends are marked: two bands, twelve days');
await dateBox.locator('.s-month .day[data-day="2026-10-30"]').click();
ok((await dateInput.inputValue()) === '2026-10-30' && !(await dateBox.locator('.s-month').isVisible()), 'a click on a day picks it and closes the month');
await dateInput.press('ArrowDown'); await p.keyboard.press('ArrowRight'); await p.keyboard.press('ArrowDown'); await p.keyboard.press('Enter');
ok((await dateInput.inputValue()) === '2026-11-07', 'arrows move the day, Enter picks it');
await dateInput.fill('31.2.2026'); await dateInput.press('Enter');
ok(await dateBox.evaluate(e => e.classList.contains('invalid')), 'a day that does not exist is marked');
await dateInput.fill('2026-10-28'); await dateInput.press('Enter');
await p.locator('.s-date[data-weekends="grey"] .cal').click();
ok(await p.locator('.s-month.grey .band').first().isVisible(), 'the grey weekends variant opens with its bands');
await p.keyboard.press('Escape');
// the table designer: a header dragged from its middle to a gap, a field dropped in, the menu, the keyboard
const des = p.locator('.s-designer');
await des.scrollIntoViewIfNeeded();
const heads = async () => (await des.locator('th').allTextContents()).join('|');
const dragTo = async (from, x, y) => { const b = await from.boundingBox(); await p.mouse.move(b.x + b.width / 2, b.y + b.height / 2); await p.mouse.down(); await p.mouse.move(b.x + b.width / 2 + 10, b.y + 20, { steps: 3 }); await p.mouse.move(x, y, { steps: 5 }); };
let time = await des.locator('th').nth(0).boundingBox();
await dragTo(des.locator('th').nth(2), time.x + time.width, time.y + 14);
ok(await des.locator('.line').isVisible(), 'while dragging, the accent line marks the gap');
await p.mouse.up();
ok((await heads()) === 'Time|Meter ID|Price|Status', 'a header dragged from its middle lands in the gap: ' + await heads());
const status = await des.locator('th').nth(3).boundingBox();
await dragTo(p.locator('.fields [data-field="Unit"]'), status.x + status.width, status.y + 14); await p.mouse.up();
ok((await heads()) === 'Time|Meter ID|Price|Status|Unit', 'a field dropped at the end adds a column');
await des.locator('th').nth(4).click({ button: 'right' });
await p.locator('.s-menu .row', { hasText: 'Remove column' }).click();
ok((await heads()) === 'Time|Meter ID|Price|Status', 'the menu removes the column');
await des.locator('th').nth(0).focus(); await p.keyboard.press('Control+ArrowRight');
ok((await heads()) === 'Meter ID|Time|Price|Status', 'Ctrl+Right moves the focused column');
// summary cards: one filter on at a time
const cardsRow = p.locator('#data-status .s-cards').first().locator('.s-card');
await cardsRow.nth(0).click();
ok((await cardsRow.nth(0).getAttribute('aria-pressed')) === 'true' && (await cardsRow.nth(3).getAttribute('aria-pressed')) === 'false', 'a summary card filters, one at a time');
// search: filters the list, counts, clears with Esc
const search = p.locator('.s-search input');
await search.fill('web');
ok((await p.locator('.s-search .count').textContent()) === '3 of 5' && await p.locator('.s-search .clear').isVisible(), 'search counts its matches and offers ×');
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
await p.locator('.mode button[data-mode="light"]').click();
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
