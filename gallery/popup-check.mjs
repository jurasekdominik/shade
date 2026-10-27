import { fileURLToPath } from 'node:url';

// Same content, logical width and spacing as the native multi-choice gallery preview.
async function previewMulti(page, theme) {
  await page.locator('.s-select[data-multi]').evaluate(owner => {
    const preview = document.createElement('div'); preview.id = 'popup-preview'; preview.oldParent = owner.parentElement;
    preview.style.cssText = 'position:fixed;left:0;top:0;z-index:1000;width:600px;height:288px;padding:28px 32px 0;box-sizing:border-box;background:var(--background)';
    preview.innerHTML = '<div class="block"><h4 style="line-height:17px">Multiple selection</h4><p class="help" style="line-height:14px;margin-bottom:6px">Arrows move, Space toggles, Esc closes.</p></div>';
    owner.set(['Anna', 'Ben', 'Chloe', 'A longer option that keeps its full accessible name'], ['Ben']); owner.style.width = '240px';
    preview.querySelector('.block').appendChild(owner); document.body.appendChild(preview);
  });
  await page.locator('#popup-preview .s-select > button').click(); await page.mouse.move(590, 280);
  await page.locator('#popup-preview').screenshot({ path: fileURLToPath(new URL(`../docs/preview/${theme}/multi-choice-web.png`, import.meta.url)) });
  await page.locator('#popup-preview .s-select > button').click();
  await page.locator('#popup-preview').evaluate(preview => { preview.oldParent.appendChild(preview.querySelector('.s-select')); preview.remove(); });
}

// Common popup ownership checks; control-specific selection/editing checks remain in page-check.mjs.
export async function popupContracts(browser, url, ok) {
  const cases = [
    ['Choice', '.s-select:not([data-multi])', ':scope > button', '.s-menu:visible', true],
    ['Floating choice', '.s-select[data-floating]', ':scope > button', 'body > .s-menu.floating:visible', true],
    ['Multi', '.s-select[data-multi]', ':scope > button', '.s-menu:visible', true],
    ['Search', '.s-search-choice', ':scope > button', '.s-search-popup:visible', true],
    ['Suggestions', '.s-suggest', 'input', '.s-menu:visible', false],
    ['Date', '.s-date:not(.range)', '.cal', '.s-month:visible', true],
    ['Range', '.s-date.range', '.cal', '.s-month:visible', true],
    ['Accent', '.s-accent', '.hex', '.pop:visible', false],
  ];
  for (const scale of [1, 2]) for (const theme of ['dark', 'light']) {
    const page = await browser.newPage({ viewport: { width: 2400, height: 1800 }, reducedMotion: 'reduce' });
    page.on('pageerror', error => ok(false, 'popup matrix script error: ' + error.message));
    await page.goto(url);
    await page.evaluate(({ scale, theme }) => { document.body.style.zoom = scale; document.documentElement.dataset.theme = theme; return document.fonts.ready; }, { scale, theme });
    if (scale === 1) await previewMulti(page, theme);
    for (const [kind, selector, triggerSelector, popupSelector, toggles] of cases) {
      await page.locator(selector).first().evaluate(owner => {
        const fixture = document.createElement('div'); fixture.id = 'popup-contract';
        fixture.style.cssText = 'position:fixed;left:16px;top:16px;width:640px;max-width:90vw;padding:16px;z-index:40;pointer-events:none';
        fixture.innerHTML = '<button class="before">Before</button><fieldset style="border:0;padding:0;margin:16px 0"></fieldset><button class="outside" style="margin-top:500px">Outside</button>';
        owner.id = 'popup-contract-owner'; owner.style.width = '320px';
        fixture.querySelector('fieldset').appendChild(owner); fixture.owner = owner;
        document.body.appendChild(fixture);
        for (const child of fixture.children) child.style.pointerEvents = 'auto';
        fixture.clicks = 0; fixture.querySelector('.outside').onclick = () => fixture.clicks++;
      });
      const fixture = page.locator('#popup-contract'), owner = fixture.locator('#popup-contract-owner');
      const trigger = owner.locator(triggerSelector), popup = ['Date', 'Range', 'Floating choice'].includes(kind) ? page.locator(popupSelector) : owner.locator(popupSelector);
      const shown = async () => await popup.count() > 0;
      const open = async () => {
        await trigger.click();
        if (kind === 'Suggestions') { await trigger.fill(''); await trigger.fill('Ann'); }
      };
      const name = `${kind} ${theme} ${scale}x web`;
      await open(); ok(await shown(), name + ': pointer opening creates a popup');
      if (kind === 'Multi') ok(await owner.locator('.row:focus').evaluate(e => !e.matches(':focus-visible') && getComputedStyle(e).backgroundColor === 'rgba(0, 0, 0, 0)'), name + ': pointer focus has neither a keyboard ring nor a keyboard-only fill');
      if (kind === 'Floating choice') {
        await page.evaluate(() => document.dispatchEvent(new Event('scroll')));
        ok(await shown(), name + ': a queued scroll with an unchanged anchor keeps the popup open');
        await fixture.evaluate(e => { e.style.top = '32px'; document.dispatchEvent(new Event('scroll')); });
        ok(!await shown(), name + ': moving the anchor dismisses the floating popup');
        await fixture.evaluate(e => e.style.top = '16px'); await open();
      }
      await trigger.click();
      ok(await shown() !== toggles, name + ': repeated trigger click preserves toggle/editor semantics');
      if (toggles) await open();
      await fixture.locator('.outside').click();
      ok(!await shown() && await fixture.locator('.outside').evaluate(e => e === document.activeElement) && await fixture.evaluate(e => e.clicks === 1), name + ': outside click preserves focus and action');
      await open(); await page.keyboard.press('Escape');
      ok(!await shown(), name + ': Escape dismisses');
      for (const backwards of [false, true]) {
        await open(); const target = fixture.locator(backwards ? '.before' : '.outside');
        for (let step = 0; step < 128 && !await target.evaluate(e => e === document.activeElement); step++) await page.keyboard.press(backwards ? 'Shift+Tab' : 'Tab');
        ok(!await shown() && await target.evaluate(e => e === document.activeElement), name + ': ' + (backwards ? 'Shift+Tab' : 'Tab') + ' leaves toward the host');
      }
      for (const state of ['disable', 'hide', 'detach']) {
        await open();
        await fixture.evaluate((e, state) => {
          const parent = e.querySelector('fieldset');
          if (state === 'disable') parent.disabled = true;
          else if (state === 'hide') parent.hidden = true;
          else e.owner.remove();
        }, state);
        await page.waitForTimeout(0);
        ok(!await shown(), name + ': ' + state + ' closes the popup');
        await fixture.evaluate(e => { const parent = e.querySelector('fieldset'); parent.disabled = false; parent.hidden = false; parent.appendChild(e.owner); });
      }
      await open(); await fixture.locator('.outside').click();
      ok(!await shown() && await fixture.evaluate(e => e.clicks === 2), name + ': reattachment does not leave stale click handlers');
      await fixture.evaluate(e => e.remove());
    }
    await page.close();
  }
}
