"""Exercise generated standalone HTML, including a save/reopen round trip.

Generate fixtures with: dotnet run --project gallery -c Release -- --paper-form-previews out/paper-form
Requires: python -m pip install playwright==1.56.0 pymupdf; python -m playwright install chromium
"""
import sys
from pathlib import Path
import fitz
from playwright.sync_api import sync_playwright


def check(directory):
    for mode in ("dark", "light"):
        with fitz.open(directory / f"paper-form-{mode}.pdf") as pdf:
            assert len(pdf) == 1
            assert abs(pdf[0].rect.width - 210 * 72 / 25.4) < 0.01
            assert abs(pdf[0].rect.height - 297 * 72 / 25.4) < 0.01
            assert not list(pdf[0].widgets())
            pdf[0].get_pixmap(dpi=96).save(directory / f"paper-pdf-{mode}.png")
    with fitz.open(directory / "paper-two-pages.pdf") as pdf:
        assert len(pdf) == 2
        assert all(abs(page.rect.width - 297 * 72 / 25.4) < 0.01 and not list(page.widgets()) for page in pdf)
    print("ok PDF: physical page sizes, multipage landscape, rendered output and zero editable widgets")
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch()
        for mode in ("dark", "light"):
            page = browser.new_page(viewport={"width": 1100, "height": 1300}, device_scale_factor=1)
            errors = []
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.goto((directory / f"paper-form-{mode}.html").as_uri())
            page.evaluate("document.fonts.ready")
            page.locator(".page").screenshot(path=str(directory / f"paper-web-{mode}.png"))
            date = page.locator('input[name="date"]')
            assert date.get_attribute("data-date-format") == "dd.MM.yyyy"
            date.fill("31.02.2026")
            assert date.get_attribute("aria-invalid") == "true"
            page.evaluate("window.originalPrint=window.print;window.printCalls=0;window.print=()=>window.printCalls++;void 0")
            page.locator("#print").click()
            assert page.evaluate("window.printCalls") == 0
            date.fill("29.02.2028")
            assert date.get_attribute("aria-invalid") == "false"
            page.locator("#print").click()
            assert page.evaluate("window.printCalls") == 1
            page.evaluate("window.print=window.originalPrint;void 0")
            date.fill("29.10.2026")
            assert page.locator("footer").inner_text() == "Page 1 of 1"
            outer = page.locator('.character-container').bounding_box()
            cells = page.locator('.character-container .characters').bounding_box()
            assert abs(outer["x"]+outer["width"]-cells["x"]-cells["width"]) < 1
            reading=page.locator('input[name="reading"]')
            reading.focus()
            reading.evaluate("input=>input.setSelectionRange(2,2)")
            page.wait_for_timeout(30)
            page.locator(".page").screenshot(path=str(directory/f"paper-web-caret-{mode}.png"))
            reading.evaluate("input=>input.setSelectionRange(2,4)")
            page.wait_for_timeout(30)
            page.locator(".page").screenshot(path=str(directory/f"paper-web-selection-{mode}.png"))
            reading.blur()
            serial = page.locator('input[name="asset"]')
            assert serial.input_value() == "0026018342"
            serial.fill("01234567890123456789")
            assert serial.input_value()=="0026018342", "an oversized user replacement must be rejected atomically"
            assert float(page.locator('textarea[name="notes"]').evaluate("input=>parseFloat(getComputedStyle(input).paddingLeft)"))>3
            assert page.locator('input[type=checkbox]').evaluate("input=>getComputedStyle(input).margin") == "0px"
            serial.fill("00e\u0301 123")
            assert serial.input_value() == "00e\u0301 123"
            serial.press("End")
            serial.press("ArrowLeft")
            serial.press("Shift+ArrowLeft")
            serial.type("9")
            assert serial.input_value() == "00e\u0301 193"
            serial.press("Control+z")
            assert serial.input_value() == "00e\u0301 123"
            serial.fill("0026018342")
            for zoom in (0.75, 1, 1.5):
                page.locator(".page").evaluate("(element, zoom) => { element.style.transformOrigin='top left'; element.style.transform=`scale(${zoom})`; }", zoom)
                box = serial.bounding_box()
                page.mouse.click(box["x"] + 4 * 4 * 96 / 25.4 * zoom + 1, box["y"] + box["height"] / 2)
                selection = serial.evaluate("input => [input.selectionStart,input.selectionEnd]")
                assert selection == [4, 4], (zoom, selection)
            page.locator(".page").evaluate("element => element.style.transform=''")
            notes = page.locator('textarea[name="notes"]')
            assert notes.get_attribute("data-scroll") == "false"
            notes.fill("one")
            for _ in range(30): notes.press("End"); notes.press("Enter")
            assert notes.evaluate("input=>input.scrollHeight<=input.clientHeight+1"), "bounded multiline input grew beyond its viewport"
            before_paste = notes.input_value()
            assert not notes.evaluate("input=>{const data=new DataTransfer();data.setData('text/plain','long\\n'.repeat(100));return input.dispatchEvent(new ClipboardEvent('paste',{clipboardData:data,bubbles:true,cancelable:true}));}")
            assert notes.input_value() == before_paste
            assert page.locator('.structure.line').count() == 1
            assert not page.locator('[data-guide]').count()
            page.locator('textarea[name="notes"]').fill("Updated notes\nŽluťoučký kůň")
            page.locator('input[name="checked"]').uncheck()
            with page.expect_download() as download:
                page.locator("#save").click()
            saved = directory / f"saved-{mode}.html"
            download.value.save_as(saved)
            page.goto(saved.as_uri())
            assert page.locator('input[name="asset"]').input_value() == "0026018342"
            assert page.locator('textarea[name="notes"]').input_value() == "Updated notes\nŽluťoučký kůň"
            assert not page.locator('input[name="checked"]').is_checked()
            page.locator('input[name="asset"]').evaluate("input=>{input.value='01234567890123456789';input.dispatchEvent(new Event('input',{bubbles:true}));}")
            page.locator("#print").click()
            assert page.locator(".notice").inner_text()
            assert page.locator('input[name="asset"]').input_value() == "01234567890123456789"
            page.evaluate("window.dispatchEvent(new Event('beforeprint'))")
            page.emulate_media(media="print")
            assert page.locator(".page").is_hidden() and "01234567890123456789" in page.locator(".notice").inner_text()
            page.evaluate("window.dispatchEvent(new Event('afterprint'))")
            page.emulate_media(media="screen")
            page.locator('input[name="asset"]').fill("0026018342")
            page.evaluate("document.activeElement.blur()")
            page.evaluate("document.fonts.ready")
            assert not errors, errors
            page.close()
            print(f"ok {mode}: native HTML editing, graphemes, undo, pointer zoom, save/reopen, checkbox, multiline, overflow")
        scrolling = browser.new_page(viewport={"width":1100,"height":1300})
        scrolling.goto((directory / "paper-form-scrolling.html").as_uri())
        notes = scrolling.locator('textarea[name="notes"]')
        assert notes.get_attribute("data-scroll") == "true"
        notes.fill("draft")
        for _ in range(30): notes.press("End"); notes.press("Enter")
        assert notes.evaluate("input=>input.scrollHeight>input.clientHeight+1")
        scrolling.close()
        print("ok multiline: bounded input/paste rejection and opt-in scrolling drafts")
        browser.close()


if __name__ == "__main__":
    check(Path(sys.argv[1] if len(sys.argv) > 1 else "out/paper-form").resolve())
