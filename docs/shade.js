// Shade for the web: the behaviour of the components in shade.css (the Avalonia controls' rules: Esc closes, arrows
// move, Enter picks, a list is one shape with its field). Plain script, no dependencies.
(function () {
  'use strict';
  var $ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };
  var icon = function (name, cls) { return '<svg class="s-i ' + (cls || '') + '" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; };
  var openList = null;  // one open list at a time: { close }

  /** text with each match of the query's words wrapped in <mark class="s-hit">, escaped */
  function marked(text, q) {
    var esc = function (t) { return t.replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); };
    var terms = (q || '').trim().split(/\s+/).filter(Boolean).map(function (t) { return t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); });
    if (!terms.length) return esc(text);
    var re = new RegExp('(' + terms.join('|') + ')', 'gi'), out = '', at = 0, m;
    while ((m = re.exec(text))) { out += esc(text.slice(at, m.index)) + '<mark class="s-hit">' + esc(m[0]) + '</mark>'; at = m.index + m[0].length; if (!m[0]) re.lastIndex++; }
    return out + esc(text.slice(at));
  }

  function closeOpen() { if (openList) { var o = openList; openList = null; o.close(); } }
  document.addEventListener('pointerdown', function (e) { if (openList && !openList.owner.contains(e.target)) closeOpen(); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeOpen(); });

  /** A list of rows under (attached) or at a point; keyboard: arrows, Enter. pick(index) on a click or Enter. */
  function listMenu(items, opt) {
    var menu = document.createElement('div');
    menu.className = 's-menu' + (opt.attached ? ' attached' : ' floating');
    menu.setAttribute('role', 'listbox');
    var rows = [], lit = opt.current == null ? -1 : opt.current;
    items.forEach(function (it, i) {
      if (it === '-') { menu.appendChild(document.createElement('hr')); return; }
      var row = document.createElement('div');
      row.className = 'row' + (i === opt.current ? ' current' : '') + (opt.danger && opt.danger(i) ? ' danger' : '');
      row.setAttribute('role', 'option');
      if (opt.color && opt.color(i)) row.style.color = opt.color(i);
      row.innerHTML = (opt.tick ? '<span class="s-tick" aria-checked="' + !!opt.tick(i) + '">' + icon('check', 'check') + '</span>' : '') + '<span></span>';
      if (opt.mark) row.lastChild.innerHTML = marked(it, opt.mark); else row.lastChild.textContent = it;
      row.addEventListener('pointerenter', function () { light(rows.indexOf(row)); });
      row.addEventListener('click', function () { opt.pick(i); });
      row.dataset.index = i;
      rows.push(row); menu.appendChild(row);
    });
    function light(k) { lit = k; rows.forEach(function (r, j) { r.classList.toggle('lit', j === k); }); if (rows[k]) rows[k].scrollIntoView({ block: 'nearest' }); }
    light(rows.findIndex(function (r) { return +r.dataset.index === opt.current; }));
    menu.key = function (e) {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); var n = rows.length; light(lit < 0 ? (e.key === 'ArrowDown' ? 0 : n - 1) : (lit + (e.key === 'ArrowDown' ? 1 : n - 1)) % n); return true; }
      if (e.key === 'Enter' && lit >= 0) { e.preventDefault(); opt.pick(+rows[lit].dataset.index); return true; }
      return false;
    };
    return menu;
  }

  // dropdown (ChoiceButton) and several-from-a-list (MultiChoice): data-items "A|B|C", data-colors "role|role" (CSS vars)
  // box.set(items, current) swaps the items; a pick fires 'change' with detail { index, value }
  function select(box) {
    var items = box.dataset.items.split('|'), colors = box.dataset.colors ? box.dataset.colors.split('|') : null;
    var multi = box.hasAttribute('data-multi'), chosen = {}, current = Math.max(0, items.indexOf(box.dataset.current));
    var button = document.createElement('button'); button.type = 'button';
    button.setAttribute('aria-haspopup', 'listbox');
    button.innerHTML = '<span></span>' + icon('chevron-down');
    box.appendChild(button);
    function show() {
      var label = button.firstChild;
      if (multi) { var picked = items.filter(function (_, i) { return chosen[i]; }); label.textContent = picked.length ? picked.join(', ') : box.dataset.empty; }
      else { label.textContent = items[current]; label.style.color = colors ? 'var(--' + colors[current] + ')' : ''; }
    }
    function open() {
      if (openList && openList.owner === box) { closeOpen(); return; }
      closeOpen();
      var menu = listMenu(items, {
        attached: true, current: multi ? null : current,
        color: colors && function (i) { return 'var(--' + colors[i] + ')'; },
        tick: multi && function (i) { return chosen[i]; },
        pick: function (i) {
          if (multi) { chosen[i] = !chosen[i]; show(); menu.querySelectorAll('.s-tick')[i].setAttribute('aria-checked', !!chosen[i]); return; }
          current = i; show(); closeOpen(); button.focus();
          box.dispatchEvent(new CustomEvent('change', { detail: { index: i, value: items[i] } }));
        },
      });
      box.appendChild(menu); box.classList.add('open'); button.setAttribute('aria-expanded', 'true');
      openList = { owner: box, menu: menu, close: function () { menu.remove(); box.classList.remove('open'); button.setAttribute('aria-expanded', 'false'); } };
    }
    button.addEventListener('click', open);
    button.addEventListener('keydown', function (e) {
      if (openList && openList.owner === box && openList.menu.key(e)) return;
      if (e.key === 'ArrowDown' && !(openList && openList.owner === box)) { e.preventDefault(); open(); }
    });
    box.set = function (list, cur) { items = list; current = Math.max(0, list.indexOf(cur)); show(); };
    box.value = function () { return items[current]; };
    show();
  }
  $('.s-select').forEach(select);

  // suggestions (SuggestionField): data-items "a|b|c"; up to eight matches, ranked prefix first
  $('.s-suggest').forEach(function (box) {
    var input = box.querySelector('input'), items = box.dataset.items.split('|');
    function matches(q) {
      q = q.trim().toLowerCase(); if (!q) return [];
      var terms = q.split(/\s+/);
      return items.filter(function (n) { return terms.every(function (t) { return n.toLowerCase().indexOf(t) >= 0; }); })
        .sort(function (a, b) { return rank(a) - rank(b) || a.length - b.length || (a < b ? -1 : 1); }).slice(0, 8);
      function rank(n) { n = n.toLowerCase(); return n === q ? 0 : n.indexOf(q) === 0 ? 1 : n.split('.').some(function (p) { return p.indexOf(q) === 0; }) ? 2 : 3; }
    }
    function refresh() {
      closeOpen();
      var found = matches(input.value);
      if (!found.length || (found.length === 1 && found[0].toLowerCase() === input.value.trim().toLowerCase())) return;
      var menu = listMenu(found, { attached: true, mark: input.value, pick: function (i) { input.value = found[i]; closeOpen(); input.focus(); } });
      box.appendChild(menu); box.classList.add('open');
      openList = { owner: box, menu: menu, close: function () { menu.remove(); box.classList.remove('open'); } };
    }
    input.addEventListener('input', refresh);
    input.addEventListener('keydown', function (e) {
      if (openList && openList.owner === box && openList.menu.key(e)) return;
      if (e.key === 'ArrowDown') { refresh(); if (openList) openList.menu.key(e); }
    });
  });

  // a context menu at the pointer: data-menu "Open|Duplicate|-|Delete" on any element (danger: the last item after a line)
  $('[data-menu]').forEach(function (el) {
    el.addEventListener('contextmenu', function (e) {
      e.preventDefault(); closeOpen();
      var items = el.dataset.menu.split('|'), line = items.indexOf('-');
      var menu = listMenu(items, { danger: function (i) { return line >= 0 && i > line; }, pick: function () { closeOpen(); } });
      document.body.appendChild(menu);
      menu.style.left = Math.min(e.clientX, innerWidth - menu.offsetWidth - 8) + 'px';
      menu.style.top = Math.min(e.clientY, innerHeight - menu.offsetHeight - 8) + 'px';
      openList = { owner: menu, menu: menu, close: function () { menu.remove(); } };
    });
  });

  // switches, ticks, chips, state switches
  $('.s-switch').forEach(function (b) { b.addEventListener('click', function () { b.setAttribute('aria-checked', b.getAttribute('aria-checked') !== 'true'); b.dispatchEvent(new Event('change')); }); });
  $('button.s-tick').forEach(function (b) { b.addEventListener('click', function () { b.setAttribute('aria-checked', b.getAttribute('aria-checked') !== 'true'); }); });
  $('.s-chip').forEach(function (b) { b.addEventListener('click', function () { b.setAttribute('aria-pressed', b.getAttribute('aria-pressed') !== 'true'); }); });
  $('.s-state').forEach(function (b) {
    function word() { b.dataset.word = b.dataset.state === 'on' ? 'ON' : 'OFF'; b.textContent = b.dataset.state === 'unknown' ? 'unknown' : ''; b.setAttribute('aria-pressed', b.dataset.state === 'on'); }
    b.addEventListener('click', function () { b.dataset.state = b.dataset.state === 'on' ? 'off' : 'on'; word(); });
    word();
  });

  // tabs: the line slides under the active tab
  $('.s-tabs').forEach(function (tabs) {
    var line = document.createElement('i'); line.className = 'line'; tabs.appendChild(line);
    var buttons = $('button', tabs);
    function place() { var a = tabs.querySelector('[aria-selected="true"]'); if (a) { line.style.left = a.offsetLeft + 'px'; line.style.width = a.offsetWidth + 'px'; } }
    buttons.forEach(function (b, index) {
      b.setAttribute('role', 'tab'); b.tabIndex = b.getAttribute('aria-selected') === 'true' ? 0 : -1;
      b.addEventListener('click', function () { buttons.forEach(function (x) { x.setAttribute('aria-selected', x === b); x.tabIndex = x === b ? 0 : -1; }); place(); });
      b.addEventListener('keydown', function (e) {
        var direction = getComputedStyle(tabs).direction === 'rtl' ? -1 : 1;
        var step = (e.key === 'ArrowRight' ? 1 : e.key === 'ArrowLeft' ? -1 : 0) * direction;
        var next = e.key === 'Home' ? 0 : e.key === 'End' ? buttons.length - 1 : Math.max(0, Math.min(buttons.length - 1, index + step));
        if (step || e.key === 'Home' || e.key === 'End') { e.preventDefault(); buttons[next].click(); buttons[next].focus(); }
      });
    });
    tabs.setAttribute('role', 'tablist');
    place(); new ResizeObserver(place).observe(tabs);
  });

  // removable tags
  $('.s-tag [data-remove]').forEach(function (b) { b.addEventListener('click', function () { b.parentElement.remove(); }); });

  // tables: a click selects a row (Ctrl adds); a header click sorts up, the next down, the third back to the original order
  $('.s-table.rows').forEach(function (table) {
    var body = table.tBodies[0];
    body.addEventListener('click', function (e) {
      var tr = e.target.closest('tr'); if (!tr) return;
      if (!(e.ctrlKey || e.metaKey)) $('tr.selected', body).forEach(function (r) { if (r !== tr) r.classList.remove('selected'); });
      tr.classList.toggle('selected', e.ctrlKey || e.metaKey ? !tr.classList.contains('selected') : true);
    });
    if (!table.classList.contains('sortable')) return;
    $('tr', body).forEach(function (r, i) { r.dataset.order = i; });
    $('th', table).forEach(function (th, col) {
      th.addEventListener('click', function () {
        var was = th.getAttribute('aria-sort'), dir = was === 'ascending' ? -1 : was === 'descending' ? 0 : 1;
        $('th', table).forEach(function (h) { h.removeAttribute('aria-sort'); });
        if (dir) th.setAttribute('aria-sort', dir > 0 ? 'ascending' : 'descending');
        var num = th.classList.contains('num');
        $('tr', body).sort(function (a, b) {
          if (!dir) return a.dataset.order - b.dataset.order;
          var x = a.cells[col].textContent, y = b.cells[col].textContent;
          return (num ? parseFloat(x) - parseFloat(y) : x.localeCompare(y)) * dir;
        }).forEach(function (r) { body.appendChild(r); });
      });
    });
  });

  // tooltips: data-tip, and the whole text of a cell that ends in "…"
  var tip = document.createElement('div'); tip.className = 's-tip'; tip.hidden = true; document.body.appendChild(tip);
  document.addEventListener('pointerover', function (e) {
    var el = e.target.closest('[data-tip], .s-table td');
    if (!el || el.classList.contains('s-nav') && !el.closest('.collapsed')) { tip.hidden = true; return; }  // an entry's name is its tip only while collapsed
    var text = el.dataset.tip || (el.scrollWidth > el.clientWidth ? el.textContent : '');
    if (!text) { tip.hidden = true; return; }
    tip.textContent = text; tip.hidden = false;
    var r = el.getBoundingClientRect();
    tip.style.left = Math.max(8, Math.min(r.left, innerWidth - tip.offsetWidth - 8)) + 'px';
    tip.style.top = (r.bottom + 6 + tip.offsetHeight > innerHeight ? r.top - tip.offsetHeight - 6 : r.bottom + 6) + 'px';
  });

  // dialogs: data-dialog="id" opens it; its buttons close it (Esc too)
  $('[data-dialog]').forEach(function (b) { b.addEventListener('click', function () { document.getElementById(b.dataset.dialog).showModal(); }); });
  $('.s-dialog').forEach(function (d) { $('button', d).forEach(function (b) { b.addEventListener('click', function () { d.close(); }); }); });

  // the shell: entries select, a group opens its children, the mark collapses the bar, Back and Forward replay the visits
  $('.s-shell').forEach(function (shell) {
    var bar = shell.querySelector('.s-sidebar'), title = shell.querySelector('.s-header h5'), hint = shell.querySelector('.s-header + p');
    var history = [], at = -1, back = shell.querySelector('.back'), fwd = shell.querySelector('.fwd');
    function go(route, record) {
      $('.s-nav', bar).forEach(function (n) {
        var group = n.dataset.group;
        n.classList.toggle('active', group ? bar.classList.contains('collapsed') && route.indexOf(group + '/') === 0 : n.dataset.route === route);
        n.classList.toggle('open', !!group && route.indexOf(group + '/') === 0);
        if (n.classList.contains('child')) n.hidden = route.indexOf(n.dataset.route.split('/')[0] + '/') !== 0 || bar.classList.contains('collapsed');
      });
      var entry = bar.querySelector('.s-nav:not([data-group])[data-route="' + route + '"]');
      title.textContent = entry.textContent.trim(); hint.textContent = entry.dataset.hint || '';
      if (record) { history = history.slice(0, at + 1); history.push(route); at = history.length - 1; }
      back.disabled = at <= 0; fwd.disabled = at >= history.length - 1;
      shell.dataset.route = route;
    }
    $('.s-nav', bar).forEach(function (n) {
      n.addEventListener('click', function () {
        if (n.dataset.group && bar.classList.contains('collapsed')) bar.classList.remove('collapsed');
        go(n.dataset.route, true);
      });
    });
    shell.querySelector('.brand').addEventListener('click', function () {
      bar.classList.toggle('collapsed');
      shell.querySelector('.brand').dataset.tip = bar.classList.contains('collapsed') ? 'Expand navigation' : 'Collapse navigation';
      go(shell.dataset.route, false);
    });
    back.addEventListener('click', function () { if (at > 0) go(history[--at], false); });
    fwd.addEventListener('click', function () { if (at < history.length - 1) go(history[++at], false); });
    go(shell.dataset.start, true);
  });

  // the responsive demo: its width and column count, live
  $('[data-measure]').forEach(function (box) {
    var out = document.getElementById(box.dataset.measure), grid = box.firstElementChild;
    new ResizeObserver(function () {
      var w = grid.clientWidth, cols = Math.max(1, Math.min(3, Math.floor((w + 16) / (140 + 16))));
      out.textContent = Math.round(w) + ' px · ' + cols + (cols === 1 ? ' column' : ' columns');
    }).observe(grid);
  });

  // summary cards: a card that filters toggles; one on at a time
  $('.s-cards').forEach(function (row) {
    var cards = $('button.s-card', row);
    cards.forEach(function (c, index) {
      c.addEventListener('click', function () { var on = c.getAttribute('aria-pressed') !== 'true'; cards.forEach(function (x) { x.setAttribute('aria-pressed', x === c && on); }); });
      c.addEventListener('keydown', function (e) {
        var cols = Math.max(1, Math.round(row.clientWidth / c.getBoundingClientRect().width));
        var step = e.key === 'ArrowLeft' ? -1 : e.key === 'ArrowRight' ? 1 : e.key === 'ArrowUp' ? -cols : e.key === 'ArrowDown' ? cols : 0;
        var next = e.key === 'Home' ? 0 : e.key === 'End' ? cards.length - 1 : Math.max(0, Math.min(cards.length - 1, index + step));
        if (step || e.key === 'Home' || e.key === 'End') { e.preventDefault(); cards[next].focus(); }
      });
    });
  });

  // search: × once typed, Esc or × clears; data-of="12" shows "n of 12" for the rows of data-list that match
  $('.s-search').forEach(function (box) {
    var input = box.querySelector('input'), clear = box.querySelector('.clear'), count = box.querySelector('.count');
    var list = box.dataset.list && document.querySelector(box.dataset.list);
    function show() {
      var q = input.value.trim().toLowerCase(); clear.hidden = !q;
      if (list) {
        var rows = $('tr, li', list).filter(function (r) { return !r.closest('thead'); }), n = 0;
        rows.forEach(function (r) {
          var hit = !q || r.textContent.toLowerCase().indexOf(q) >= 0; r.hidden = !hit; if (hit) n++;
          $('td, li', r).concat(r.tagName === 'LI' ? [r] : []).forEach(function (c) {
            if (c.children.length && !c.querySelector('mark.s-hit')) return;  // a cell with its own markup keeps it
            if (c.dataset.text == null) c.dataset.text = c.textContent;
            c.innerHTML = marked(c.dataset.text, hit ? q : '');
          });
        });
        if (count) { count.hidden = !q; count.textContent = n + ' of ' + rows.length; }
      }
    }
    input.addEventListener('input', show);
    input.addEventListener('keydown', function (e) { if (e.key === 'Escape' && input.value) { e.stopPropagation(); input.value = ''; show(); } });
    clear.addEventListener('click', function () { input.value = ''; show(); input.focus(); });
    box.addEventListener('mousedown', function (e) { if (e.target === box) { e.preventDefault(); input.focus(); } });
    show();
  });

  // reference cards: a click on an example fills the field it documents (data-for)
  $('.s-ref [data-use]').forEach(function (chip) {
    chip.addEventListener('click', function () { var f = document.getElementById(chip.closest('.s-ref').dataset.for); if (f) { f.value = chip.dataset.use; f.focus(); } });
  });

  // the tray icon: its menu opens above it; a pick, Esc or a click elsewhere closes it
  $('.s-taskbar .tray').forEach(function (icon) {
    var items = icon.dataset.items.split('|'), current = +(icon.dataset.current || -1);
    icon.addEventListener('click', function (e) {
      e.stopPropagation();
      if (openList && openList.owner === icon.parentElement) { closeOpen(); return; }
      closeOpen();
      var menu = listMenu(items, { current: current, pick: function (i) { current = i; closeOpen(); } });
      menu.classList.remove('floating'); icon.parentElement.appendChild(menu); icon.classList.add('open');
      openList = { owner: icon.parentElement, menu: menu, close: function () { menu.remove(); icon.classList.remove('open'); } };
    });
  });

  // the accent picker: a new accent for the whole page, made by the same rules as Look.SetAccent
  function rgb(hex) { var n = parseInt(hex.slice(1), 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; }
  function hexOf(c) { return '#' + c.map(function (v) { return ('0' + Math.round(v).toString(16)).slice(-2); }).join('').toUpperCase(); }
  function blend(a, b, t) { return a.map(function (v, i) { return v + (b[i] - v) * t; }); }
  function lum(c) { return c.map(function (v) { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }).reduce(function (s, v, i) { return s + v * [0.2126, 0.7152, 0.0722][i]; }, 0); }
  function contrast(a, b) { var p = lum(a), q = lum(b); return (Math.max(p, q) + 0.05) / (Math.min(p, q) + 0.05); }
  function css(name) { return getComputedStyle(document.documentElement).getPropertyValue('--' + name).trim(); }
  var accent = null;  // { dark, light }: a curated accent has a value for each mode, a custom one the same for both
  function dark() { return document.documentElement.dataset.theme === 'dark'; }
  function applyAccent() {
    var root = document.documentElement.style;
    ['accent', 'accent-hover', 'accent-tint', 'accent-text', 'on-accent', 'focus-border'].forEach(function (k) { root.removeProperty('--' + k); });
    if (accent) {
    var hex = dark() ? accent.dark : accent.light;
    var a = rgb(hex), raised = rgb(css('raised')), far = rgb(css('far')), ink = [21, 21, 25], white = [255, 255, 255], text = a, tint = blend(raised, a, 0.15);
    // small accent text reads at 4.5:1 on Raised and on its own tint, as in Look
    for (var i = 0; i < 20 && (contrast(text, raised) < 4.5 || contrast(text, tint) < 4.5); i++) text = blend(text, far, 0.08);
    root.setProperty('--accent', hex);
    root.setProperty('--accent-hover', hexOf(blend(a, white, 0.10)));
    root.setProperty('--accent-tint', hexOf(tint));
    root.setProperty('--accent-text', hexOf(text));
    root.setProperty('--focus-border', hexOf(Math.min(contrast(a, raised), contrast(a, rgb(css('background')))) >= 3 ? a : text));
    root.setProperty('--on-accent', contrast(a, ink) >= contrast(a, white) ? hexOf(ink) : '#FFFFFF');
    }
    // the page's accent swatches name the picked accent's values, and their own again once it's cleared
    $('.swatch').forEach(function (sw) {
      var m = /var\(--((?:on-)?accent[\w-]*)\)/.exec(sw.querySelector('.chip').getAttribute('style') || '');
      if (m) sw.querySelectorAll('.mono').forEach(function (t) { if (!t.dataset.code) t.dataset.code = t.textContent; t.textContent = accent ? css(m[1]).toUpperCase() : t.dataset.code; });
    });
  }
  function hsv(h, s, v) {
    var f = function (n) { var k = (n + h / 60) % 6; return v - v * s * Math.max(0, Math.min(k, 4 - k, 1)); };
    return [f(5), f(3), f(1)].map(function (x) { return x * 255; });
  }
  $('.s-picker').forEach(function (pick) {
    var foot = pick.querySelector('.foot'), hexOut = foot.querySelector('.mono'), what = foot.querySelector('.what'), ratio = foot.querySelector('.ratio');
    var field = pick.querySelector('.field'), hue = pick.querySelector('.hue'), h = 250, sat = 0.5, val = 0.9;
    function show(label) {
      var bg = rgb(css('background')), cur = css('accent'), c = contrast(rgb(cur), bg);
      $('.palette button', pick).forEach(function (b) { b.classList.toggle('faded', contrast(rgb(b.dataset.c), bg) < 4.5); });
      hexOut.textContent = cur.toUpperCase(); if (label) what.textContent = label;
      ratio.textContent = c.toFixed(1) + ' : 1 ' + (c >= 4.5 ? 'readable' : c >= 3 ? 'borderline' : dark() ? 'too dark' : 'too light');
      ratio.classList.toggle('low', c < 4.5);
    }
    function choose(el, pair, label) {
      $('.cur, .palette button', pick).forEach(function (x) { x.classList.toggle('current', x === el); });
      accent = pair; applyAccent(); show(label);
      document.dispatchEvent(new Event('shade-accent'));
    }
    $('.cur', pick).forEach(function (b) {
      b.addEventListener('click', function () { choose(b, { dark: b.dataset.dark, light: b.dataset.light, name: b.dataset.name }, b.dataset.name); });
    });
    $('.palette button', pick).forEach(function (b) {
      b.style.background = b.dataset.c; b.setAttribute('data-tip', b.getAttribute('aria-label') + ' · ' + b.dataset.c);
      b.addEventListener('click', function () { choose(b, { dark: b.dataset.c, light: b.dataset.c }, b.getAttribute('aria-label')); });
    });
    // the mixer: saturation left to right, brightness top to bottom, hue below; the accent follows the drag
    function mix() {
      field.style.setProperty('--hue', h); hue.style.setProperty('--hue', h);
      field.firstElementChild.style.left = sat * 100 + '%'; field.firstElementChild.style.top = (1 - val) * 100 + '%';
      hue.firstElementChild.style.left = h / 360 * 100 + '%';
    }
    function drag(el, at) {
      el.addEventListener('pointerdown', function (e) {
        el.setPointerCapture(e.pointerId); at(e);
        function move(ev) { at(ev); }
        function up() { el.removeEventListener('pointermove', move); el.removeEventListener('pointerup', up); }
        el.addEventListener('pointermove', move); el.addEventListener('pointerup', up);
      });
    }
    function take() { var c = hexOf(hsv(h, sat, val)); mix(); choose(null, { dark: c, light: c }, 'mixed'); }
    drag(field, function (e) { var r = field.getBoundingClientRect(); sat = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width)); val = Math.max(0, Math.min(1, 1 - (e.clientY - r.top) / r.height)); take(); });
    drag(hue, function (e) { var r = hue.getBoundingClientRect(); h = Math.max(0, Math.min(359.9, (e.clientX - r.left) / r.width * 360)); take(); });
    // the tabs switch the panes
    $('.s-tabs button', pick).forEach(function (t) { t.addEventListener('click', function () { $('[data-pane]', pick).forEach(function (p) { p.hidden = p.dataset.pane !== t.dataset.show; }); }); });
    mix(); pick.show = show; show('Violet');
  });
  // the mode switch (ModeSwitch): Light, Dark or System, which follows prefers-color-scheme as it changes; every switch shows the choice
  (function () {
    var groups = $('.s-mode'); if (!groups.length) return;
    var media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: light)') : null, choice = 'system';
    try { choice = localStorage.getItem('shade-theme') || 'system'; } catch (e) {}
    function apply() {
      document.documentElement.dataset.theme = choice === 'system' ? (media && media.matches ? 'light' : 'dark') : choice;
      groups.forEach(function (g) { $('button', g).forEach(function (b) { b.setAttribute('aria-checked', String(b.dataset.mode === choice)); b.tabIndex = b.dataset.mode === choice ? 0 : -1; }); });
    }
    function pick(mode) { choice = mode; try { localStorage.setItem('shade-theme', mode); } catch (e) {} apply(); }
    groups.forEach(function (g) {
      var buttons = $('button', g);
      buttons.forEach(function (b, i) {
        b.addEventListener('click', function () { pick(b.dataset.mode); });
        b.addEventListener('keydown', function (e) {
          var to = e.key === 'ArrowLeft' ? i - 1 : e.key === 'ArrowRight' ? i + 1 : -1;
          if (to < 0 || to >= buttons.length) return;
          e.preventDefault(); pick(buttons[to].dataset.mode); buttons[to].focus();
        });
      });
    });
    if (media) media.addEventListener('change', function () { if (choice === 'system') apply(); });
    apply();
  })();

  // the editable table (EditableTable): its columns and rows come from a JSON script inside it. Cells edit in place (Enter
  // and Shift+Enter move down and up the column); a value with a unit cycles its unit on a click; a computed column joins
  // any number of columns with + − × ÷ (× and ÷ first); the footer sums, averages, counts. Headings resize by their edge
  // (a double-click fits) and sort on a click: up, down, back to the original order. Row numbers select rows (Ctrl adds,
  // Shift takes a range); a row's menu duplicates, inserts and deletes; Add takes several. In design mode the headings
  // are the table designer: drag one to a gap, right-click to insert or remove, Ctrl+arrows to move.
  var OPS = { '+': function (a, b) { return a + b; }, '−': function (a, b) { return a - b; }, '×': function (a, b) { return a * b; }, '÷': function (a, b) { return b ? a / b : NaN; } };
  var FNS = { none: 'None', sum: 'Sum', average: 'Average', min: 'Min', max: 'Max', count: 'Count' };
  var ink = document.createElement('canvas').getContext('2d');
  function textWidth(t, px, weight) { ink.font = (weight || 400) + ' ' + px + 'px Inter, sans-serif'; return ink.measureText(t || '').width; }
  function esc(t) { return String(t).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  var confirmBox = null;
  function confirmAsk(text, action, then) {
    if (!confirmBox) {
      confirmBox = document.createElement('dialog'); confirmBox.className = 's-dialog';
      confirmBox.innerHTML = '<h4></h4><div class="buttons"><button class="s-btn" value="no">Cancel</button><button class="s-btn danger" value="yes"></button></div>';
      document.body.appendChild(confirmBox);
      $('button', confirmBox).forEach(function (b) { b.addEventListener('click', function () { confirmBox.close(b.value); }); });
    }
    confirmBox.querySelector('h4').textContent = text; confirmBox.querySelector('.danger').textContent = action;
    confirmBox.onclose = function () { if (confirmBox.returnValue === 'yes') then(); };
    confirmBox.returnValue = ''; confirmBox.showModal(); confirmBox.querySelector('.danger').focus();
  }
  var TYPES = { text: 'Text', number: 'Number', unit: 'Value with unit', computed: 'Computed', general: 'General' }, SIZES = { fit: 'Fit', fill: 'Fill', fixed: 'Fixed' };
  $('.s-grid').forEach(function (box) {
    var cfg = JSON.parse(box.querySelector('script').textContent), seq = 0;
    // sizing: fit (its content, never cut), fill (the room the others leave) or fixed (width); min and max hold any of them
    var cols = cfg.columns.map(function (c) { return Object.assign({ type: 'text', footer: 'none', width: null, min: null, max: null, sizing: c.grow ? 'fill' : c.width ? 'fixed' : 'fit' }, c); });
    var rows = cfg.rows.map(function (r) { return { id: ++seq, v: Object.assign({}, r) }; });
    var opt = { head: 12, sub: 11, cell: 13, sorting: true, design: false }, sort = { key: null, dir: 0 };
    var selected = {}, anchor = null, current = cols[0].key, line = document.createElement('div');
    box.innerHTML = '<div class="scroll"><table></table></div><div class="add"><button type="button" class="s-btn ghost rows">' + icon('plus') + 'Add</button>' +
      '<input class="s-field count" value="1" inputmode="numeric" aria-label="Rows to add"><span>rows</span><button type="button" class="s-btn ghost column">' + icon('plus') + 'Add column</button></div>';
    line.className = 'line'; line.hidden = true; box.appendChild(line);
    var scroll = box.firstChild, table = scroll.firstChild, countField = box.querySelector('.count');
    function col(k) { return cols.find(function (c) { return c.key === k; }); }
    function row(id) { return rows.find(function (r) { return r.id === +id; }); }
    function numeric(c) { return c.type !== 'text'; }
    function num(t) { t = String(t == null ? '' : t).trim().replace(/\s/g, '').replace(',', '.'); return t === '' ? NaN : Number(t); }
    function value(r, c, depth) {
      if (c.type !== 'computed') return num(r.v[c.key]);
      depth = (depth || 0) + 1;
      if (depth > 8 || !c.terms || !c.terms.length) return NaN;  // a formula that points at itself stops here
      var vals = c.terms.map(function (t) { return col(t.k) ? value(r, col(t.k), depth) : NaN; }), sums = [vals[0]], ops = [];
      for (var i = 1; i < c.terms.length; i++) {
        var op = c.terms[i].op;
        if (op === '×' || op === '÷') sums[sums.length - 1] = OPS[op](sums[sums.length - 1], vals[i]); else { ops.push(op); sums.push(vals[i]); }
      }
      return ops.reduce(function (n, op, i) { return OPS[op](n, sums[i + 1]); }, sums[0]);
    }
    function formula(c) { return c.terms.map(function (t, i) { return (i ? ' ' + t.op + ' ' : '') + (col(t.k) ? col(t.k).title : '?'); }).join(''); }
    function fmt(c, n) { return isFinite(n) ? n.toLocaleString('en', { minimumFractionDigits: c.decimals || 0, maximumFractionDigits: c.decimals == null ? 4 : c.decimals }) : '—'; }
    function shown(r, c) { return c.type === 'computed' ? fmt(c, value(r, c)) : r.v[c.key] == null ? '' : String(r.v[c.key]); }
    // the row's unit as stored, one no longer in the list included (a click moves it to the list's first)
    function unitOf(r, c) { var u = r.v[c.key + ':unit']; return u ? u : c.units[0]; }
    // right-aligned: a number column's every cell, a general column's numbers
    function right(c, v) { return c.type === 'general' ? isFinite(num(v)) : numeric(c); }
    function footer(c) {
      if (c.footer === 'none') return '';
      if (c.footer === 'count') return String(rows.filter(function (r) { return c.type === 'computed' ? isFinite(value(r, c)) : String(r.v[c.key] || '').trim() !== ''; }).length);
      var ns = rows.map(function (r) { return value(r, c); }).filter(isFinite);
      if (!ns.length) return '—';
      var n = c.footer === 'sum' ? ns.reduce(function (a, b) { return a + b; }, 0) : c.footer === 'average' ? ns.reduce(function (a, b) { return a + b; }, 0) / ns.length
        : c.footer === 'min' ? Math.min.apply(null, ns) : Math.max.apply(null, ns);
      return fmt(c, n);
    }
    function order() {
      if (!sort.dir) return rows.slice();
      var c = col(sort.key);
      return rows.slice().sort(function (x, y) {
        var a = numeric(c) ? value(x, c) : shown(x, c).toLowerCase(), b = numeric(c) ? value(y, c) : shown(y, c).toLowerCase();
        if (numeric(c)) {  // empty cells last, either way; a general column's text after its numbers, by name
          if (!isFinite(a) || !isFinite(b)) return isFinite(a) ? -1 : isFinite(b) ? 1 : c.type === 'general' ? shown(x, c).localeCompare(shown(y, c)) : 0;
        }
        return (a < b ? -1 : a > b ? 1 : 0) * sort.dir;
      });
    }
    // a column's width from its whole content: the heading, every value with its unit or suffix, the footer
    function natural(c) {
      var w = Math.max(textWidth(c.title, opt.head, 600), textWidth(c.subtitle, opt.sub)) + (sort.key === c.key ? 22 : 0);
      var extra = c.type === 'unit' ? Math.max.apply(null, c.units.concat(rows.map(function (r) { return unitOf(r, c); })).map(function (u) { return textWidth(u, opt.cell); })) + 14 : c.suffix ? textWidth(c.suffix, 11) + 6 : 0;
      rows.forEach(function (r) { w = Math.max(w, textWidth(shown(r, c), opt.cell, c.bold ? 600 : 400) + extra); });
      w = Math.max(w, textWidth(footer(c), opt.cell, 600) + (c.footer !== 'none' ? textWidth(FNS[c.footer], 11, 500) + 6 : 0));
      return Math.ceil(Math.max(40, w + 18));
    }
    // a fill column gives way down to its longest word (its text wraps)
    function least(c) {
      var w = 0;
      [c.title].concat(rows.map(function (r) { return shown(r, c); })).forEach(function (t) { String(t).split(/\s+/).forEach(function (word) { w = Math.max(w, textWidth(word, opt.cell, 600)); }); });
      return Math.ceil(Math.max(60, w + 18));
    }
    // fit and fixed columns take their width; the fill columns share what is left, equally, each held to its min and max.
    // Past every minimum, it scrolls.
    function layout() {
      var fixed = 36 + 32, lo = cols.map(function (c) { return Math.max(40, c.min || (c.sizing === 'fill' ? least(c) : 0)); });
      var clamp = function (i, w) { return Math.max(lo[i], Math.min(w, cols[i].max || Infinity)); };
      var widths = cols.map(function (c, i) { return c.sizing === 'fill' ? 0 : clamp(i, c.sizing === 'fixed' && c.width ? c.width : natural(c)); });
      var room = scroll.clientWidth - 2 - fixed - widths.reduce(function (a, b) { return a + b; }, 0);
      var open = cols.map(function (c, i) { return c.sizing === 'fill' ? i : -1; }).filter(function (i) { return i >= 0; });
      while (open.length) {
        var share = Math.max(0, room) / open.length;
        var held = open.filter(function (i) { return lo[i] > share; });  // the ones that need more than a share first, then the capped ones
        if (!held.length) held = open.filter(function (i) { return (cols[i].max || Infinity) < share; });
        if (!held.length) { open.forEach(function (i) { widths[i] = share; }); break; }
        held.forEach(function (i) { widths[i] = clamp(i, share); room -= widths[i]; open.splice(open.indexOf(i), 1); });
      }
      widths = widths.map(Math.floor);
      $('col.c', table).forEach(function (el, i) { el.style.width = widths[i] + 'px'; });
      table.style.width = widths.reduce(function (a, b) { return a + b; }, 0) + fixed + 'px';
    }
    function render(focus) {
      var list = order(), h = '<colgroup><col style="width:36px">' + cols.map(function () { return '<col class="c">'; }).join('') + '<col style="width:32px"></colgroup><thead><tr>';
      var all = list.length && list.every(function (r) { return selected[r.id]; });
      h += '<th class="gut"><button type="button" class="pick" aria-label="' + (all ? 'Select none' : 'Select all rows') + '" data-tip="' + (all ? 'Select none' : 'Select all') + '">#</button></th>';
      cols.forEach(function (c) {
        var dir = sort.key === c.key ? sort.dir : 0;
        h += '<th data-k="' + c.key + '" class="' + (numeric(c) && c.type !== 'general' ? 'num' : '') + (c.key === current ? ' current' : '') + '"' + (dir ? ' aria-sort="' + (dir > 0 ? 'ascending' : 'descending') + '"' : '') +
          (opt.design ? ' tabindex="0"' : '') + (c.type === 'computed' ? ' data-tip="= ' + esc(formula(c)) + '"' : '') + '>' +
          (opt.design ? icon('grip-vertical', 'grip') : '') +
          '<div class="lbl"><span class="h">' + esc(c.title) + '</span>' + (c.subtitle ? '<span class="subt">' + esc(c.subtitle) + '</span>' : '') + '</div>' +
          (dir ? icon(dir > 0 ? 'chevron-up' : 'chevron-down', 'sort') : '') + '<span class="rz"></span></th>';
      });
      h += '<th class="act"></th></tr></thead><tbody>';
      list.forEach(function (r, i) {
        h += '<tr data-id="' + r.id + '"' + (selected[r.id] ? ' class="selected"' : '') + '><td class="gut"><button type="button" class="pick" tabindex="-1" aria-label="Select row ' + (i + 1) + '">' + (i + 1) + '</button></td>';
        cols.forEach(function (c) {
          var cls = (right(c, r.v[c.key]) ? 'num' : '') + (c.type === 'computed' ? ' ro' : '') + (c.bold ? ' bold' : '');
          h += '<td data-k="' + c.key + '" class="' + cls + '">';
          h += c.type === 'computed' ? '<div class="v" tabindex="0">' + esc(shown(r, c)) + '</div>'  // read only, still focusable: its column's controls follow
            : '<div class="v" contenteditable="plaintext-only" spellcheck="false" role="textbox" aria-label="' + esc(c.title) + '">' + esc(shown(r, c)) + '</div>';
          if (c.type === 'unit') h += '<button type="button" class="unit" data-tip="Click to change the unit" aria-label="Unit: ' + unitOf(r, c) + '">' + unitOf(r, c) + '</button>';
          if (c.suffix) h += '<span class="suf">' + esc(c.suffix) + '</span>';
          h += '</td>';
        });
        h += '<td class="act"><button type="button" class="more" data-tip="Row actions" aria-label="Row actions">' + icon('ellipsis') + '</button></td></tr>';
      });
      h += '</tbody><tfoot><tr><td class="gut"></td>' + cols.map(function (c) {
        return '<td data-k="' + c.key + '" class="' + (numeric(c) ? 'num' : '') + '"><button type="button" class="fn' + (c.footer === 'none' ? ' none' : '') + '" data-tip="Choose what the footer shows">' +
          (c.footer === 'none' ? 'Calculate' : '<span class="fl">' + FNS[c.footer] + '</span><b>' + esc(footer(c)) + '</b>') + '</button></td>';
      }).join('') + '<td class="act"></td></tr></tfoot>';
      table.innerHTML = h;
      box.classList.toggle('sorting', opt.sorting && !opt.design); box.classList.toggle('design', opt.design);
      box.style.setProperty('--head', opt.head + 'px'); box.style.setProperty('--sub', opt.sub + 'px'); box.style.setProperty('--cell', opt.cell + 'px');
      layout();
      if (focus && focus.head) { var th = table.querySelector('th[data-k="' + focus.head + '"]'); if (th) th.focus(); }
      else if (focus) { var cell = table.querySelector('tr[data-id="' + focus.id + '"] td[data-k="' + focus.key + '"] .v'); if (cell) { cell.focus(); selectAll(cell); } }
      box.dispatchEvent(new CustomEvent('column', { detail: col(current) }));
    }
    function selectAll(el) { var r = document.createRange(); r.selectNodeContents(el); var sel = getSelection(); sel.removeAllRanges(); sel.addRange(r); }
    // after an edit: the row's computed cells, the footers and the widths, without drawing the table again (the caret stays)
    function refresh(tr) {
      var r = row(tr.dataset.id);
      cols.forEach(function (c) {
        if (c.type === 'computed') tr.querySelector('td[data-k="' + c.key + '"] .v').textContent = shown(r, c);
        var f = table.querySelector('tfoot td[data-k="' + c.key + '"] b'); if (f) f.textContent = footer(c);
      });
      layout();
    }
    function blank() { var v = {}; cols.forEach(function (c) { if (c.type === 'unit') v[c.key + ':unit'] = c.units[0]; }); return { id: ++seq, v: v }; }
    function at(menu, x, y) {
      document.body.appendChild(menu);
      menu.style.left = Math.min(x, innerWidth - menu.offsetWidth - 8) + 'px'; menu.style.top = Math.min(y, innerHeight - menu.offsetHeight - 8) + 'px';
      openList = { owner: menu, menu: menu, close: function () { menu.remove(); } };
    }
    function targets(id) { if (!selected[id]) { selected = {}; selected[id] = true; anchor = +id; } return order().filter(function (r) { return selected[r.id]; }); }
    function rowMenu(id, x, y) {
      closeOpen();
      var t = targets(id), n = t.length, many = n > 1 ? ' ' + n + ' rows' : '';
      render();
      at(listMenu(['Duplicate' + many, 'Insert row above', 'Insert row below', '-', 'Delete' + (many || ' row')], { danger: function (i) { return i === 4; }, pick: function (i) {
        closeOpen();
        var last = rows.indexOf(t[t.length - 1]);
        if (i === 0) { var copies = t.map(function (r) { return { id: ++seq, v: Object.assign({}, r.v) }; }); rows.splice.apply(rows, [last + 1, 0].concat(copies)); selected = {}; copies.forEach(function (c) { selected[c.id] = true; }); render(); }
        else if (i === 1 || i === 2) { var b = blank(); rows.splice(i === 1 ? rows.indexOf(t[0]) : last + 1, 0, b); render({ id: b.id, key: cols[0].key }); }
        else if (i === 4) confirmAsk(n > 1 ? 'Delete ' + n + ' rows?' : 'Delete this row?', n > 1 ? 'Delete ' + n + ' rows' : 'Delete row', function () { rows = rows.filter(function (r) { return t.indexOf(r) < 0; }); selected = {}; render(); });
      } }), x, y);
    }
    function footMenu(c, el) {
      closeOpen();
      var keys = numeric(c) ? Object.keys(FNS) : ['none', 'count'], r = el.getBoundingClientRect();
      var menu = listMenu(keys.map(function (k) { return FNS[k]; }), { current: keys.indexOf(c.footer), pick: function (i) { closeOpen(); c.footer = keys[i]; render(); } });
      at(menu, r.left, 0); menu.style.top = (r.top - menu.offsetHeight - 4 > 0 ? r.top - menu.offsetHeight - 4 : r.bottom + 4) + 'px';
    }
    // the designer: columns move, come and go
    function moveCol(from, to) { if (to === from || to < 0 || to >= cols.length) return false; cols.splice(to, 0, cols.splice(from, 1)[0]); return true; }
    function insertCol(i) {
      var c = { key: 'c' + (++seq), title: 'New column', type: 'text', footer: 'none', width: null };
      cols.splice(i, 0, c); current = c.key; render({ head: c.key });
      var f = document.querySelector('[data-grid="' + box.id + '"][data-col="title"]'); if (f) { f.focus(); f.select(); }
    }
    function removeCol(k) {
      var c = col(k);
      confirmAsk('Remove the column “' + c.title + '” and its values?', 'Remove column', function () {
        cols = cols.filter(function (x) { return x !== c; });
        cols.forEach(function (x) { if (x.terms) x.terms = x.terms.filter(function (t) { return t.k !== k; }); if (x.terms && x.terms[0]) delete x.terms[0].op; });
        rows.forEach(function (r) { delete r.v[k]; delete r.v[k + ':unit']; });
        if (current === k) current = cols[0] && cols[0].key; render();
      });
    }
    function headMenu(k, x, y) {
      closeOpen(); var i = cols.indexOf(col(k));
      at(listMenu(['Insert column before', 'Insert column after', '-', 'Remove column'], { danger: function (n) { return n === 3; }, pick: function (n) {
        closeOpen(); if (n === 3) removeCol(k); else insertCol(i + n);
      } }), x, y);
    }
    function gapAt(x, y) {  // the gap between headings nearest x (0..count), or -1 away from the table
      var t = table.getBoundingClientRect(); if (y < t.top - 24 || y > t.bottom + 24 || x < t.left - 40 || x > t.right + 40) return -1;
      var edges = [table.querySelector('th.gut').getBoundingClientRect().right].concat($('th[data-k]', table).map(function (th) { return th.getBoundingClientRect().right; })), best = 0;
      edges.forEach(function (e, i) { if (Math.abs(x - e) < Math.abs(x - edges[best])) best = i; });
      return best;
    }
    function showGap(g, from) {
      var shown = g >= 0 && g !== from && g !== from + 1; line.hidden = !shown; if (!shown) return;
      var b = box.getBoundingClientRect(), t = table.getBoundingClientRect();
      var x = g === 0 ? table.querySelector('th.gut').getBoundingClientRect().right : $('th[data-k]', table)[g - 1].getBoundingClientRect().right;
      line.style.left = (x - b.left) + 'px'; line.style.top = (t.top - b.top) + 'px'; line.style.height = t.height + 'px';
    }
    table.addEventListener('input', function (e) {
      var v = e.target.closest('.v'), tr = v && v.closest('tbody tr'); if (!tr) return;
      var td = v.parentElement, c = col(td.dataset.k);
      row(tr.dataset.id).v[c.key] = v.textContent; td.classList.toggle('num', right(c, v.textContent)); refresh(tr);
    });
    table.addEventListener('keydown', function (e) {
      var th = e.target.closest('th[data-k]');
      if (th && opt.design) {
        var i = cols.indexOf(col(th.dataset.k));
        if (e.ctrlKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) { if (moveCol(i, i + (e.key === 'ArrowLeft' ? -1 : 1))) render({ head: th.dataset.k }); }
        else if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') { var to = e.key === 'ArrowLeft' ? th.previousElementSibling : th.nextElementSibling; if (to && to.dataset.k) to.focus(); }
        else if (e.key === 'ContextMenu' || (e.shiftKey && e.key === 'F10')) { var r = th.getBoundingClientRect(); headMenu(th.dataset.k, r.left, r.bottom); }
        else if (e.key === 'Delete') removeCol(th.dataset.k);
        else return;
        e.preventDefault(); return;
      }
      var v = e.target.closest('.v'); if (!v || e.key !== 'Enter') return;
      e.preventDefault();  // no new lines: Enter goes down the column, Shift+Enter up
      var tr = v.closest('tr'), next = e.shiftKey ? tr.previousElementSibling : tr.nextElementSibling;
      var target = next && next.querySelector('td[data-k="' + v.parentElement.dataset.k + '"] .v[contenteditable]');
      if (target) { target.focus(); selectAll(target); }
    });
    table.addEventListener('focusin', function (e) {
      var cell = e.target.closest('td[data-k], th[data-k]'); if (!cell || cell.dataset.k === current) return;
      current = cell.dataset.k; $('th[data-k]', table).forEach(function (th) { th.classList.toggle('current', th.dataset.k === current); });
      box.dispatchEvent(new CustomEvent('column', { detail: col(current) }));
    });
    table.addEventListener('mousedown', function (e) { if (e.target.closest('.unit, .pick, .more, .fn')) e.preventDefault(); });  // the caret stays where it was
    table.addEventListener('click', function (e) {
      var unit = e.target.closest('.unit'), pick = e.target.closest('.pick'), more = e.target.closest('.more'), fn = e.target.closest('.fn'), th = e.target.closest('th[data-k]');
      if (unit) {
        var tr = unit.closest('tr'), r = row(tr.dataset.id), c = col(unit.parentElement.dataset.k), u = c.units[(c.units.indexOf(unitOf(r, c)) + 1) % c.units.length];
        r.v[c.key + ':unit'] = u; unit.textContent = u; unit.setAttribute('aria-label', 'Unit: ' + u); refresh(tr);
      } else if (pick && pick.closest('thead')) {
        var list = order(), all = list.every(function (r) { return selected[r.id]; }); selected = {}; if (!all) list.forEach(function (r) { selected[r.id] = true; }); render();
      } else if (pick) {
        var id = +pick.closest('tr').dataset.id, ids = order().map(function (r) { return r.id; });
        if (e.shiftKey && anchor != null) { var a = ids.indexOf(anchor), b = ids.indexOf(id); if (!(e.ctrlKey || e.metaKey)) selected = {}; ids.slice(Math.min(a, b), Math.max(a, b) + 1).forEach(function (x) { selected[x] = true; }); }
        else if (e.ctrlKey || e.metaKey) { if (selected[id]) delete selected[id]; else selected[id] = true; anchor = id; }
        else { var only = selected[id] && Object.keys(selected).length === 1; selected = {}; if (!only) selected[id] = true; anchor = id; }
        render();
      } else if (more) { var rr = more.getBoundingClientRect(); rowMenu(more.closest('tr').dataset.id, rr.right - 4, rr.bottom); }
      else if (fn) footMenu(col(fn.parentElement.dataset.k), fn);
      else if (th && opt.sorting && !opt.design && !e.target.closest('.rz')) {
        var k = th.dataset.k; sort = { key: k, dir: sort.key !== k ? 1 : sort.dir === 1 ? -1 : 0 }; if (!sort.dir) sort.key = null; render();
      }
    });
    table.addEventListener('contextmenu', function (e) {
      var th = e.target.closest('th[data-k]'), tr = e.target.closest('tbody tr');
      if (th && opt.design) { e.preventDefault(); headMenu(th.dataset.k, e.clientX, e.clientY); }
      else if (tr) { e.preventDefault(); rowMenu(tr.dataset.id, e.clientX, e.clientY); }
    });
    // a double-click on the edge fits the column again (a fill column loses its minimum)
    table.addEventListener('dblclick', function (e) { var rz = e.target.closest('.rz'); if (rz) { var c = col(rz.parentElement.dataset.k); if (c.sizing === 'fill') c.min = null; else { c.width = null; c.sizing = 'fit'; } layout(); box.dispatchEvent(new CustomEvent('column', { detail: col(current) })); } });
    table.addEventListener('pointerdown', function (e) {
      if (e.button !== 0) return;
      var rz = e.target.closest('.rz'), th = e.target.closest('th[data-k]');
      if (rz) {  // a heading's edge: a fill column's minimum, any other column's fixed width follows the pointer
        e.preventDefault(); var c = col(rz.parentElement.dataset.k), start = e.clientX, w = $('col.c', table)[cols.indexOf(c)].getBoundingClientRect().width;
        rz.setPointerCapture(e.pointerId); box.classList.add('resizing'); rz.classList.add('on');
        var mv = function (ev) { var v = Math.max(40, Math.round(w + ev.clientX - start)); if (c.sizing === 'fill') c.min = v; else { c.width = v; c.sizing = 'fixed'; } layout(); };
        var up = function () { rz.removeEventListener('pointermove', mv); rz.removeEventListener('pointerup', up); box.classList.remove('resizing'); rz.classList.remove('on'); box.dispatchEvent(new CustomEvent('column', { detail: col(current) })); };
        rz.addEventListener('pointermove', mv); rz.addEventListener('pointerup', up);
        return;
      }
      if (!th || !opt.design) return;
      // design mode: a heading dragged from anywhere on it to a gap; a click without a drag makes it the current column
      var k = th.dataset.k, from = cols.indexOf(col(k)), sx = e.clientX, sy = e.clientY, moving = false, ghost = null;
      th.setPointerCapture(e.pointerId);
      var drag = function (ev) {
        if (!moving && Math.abs(ev.clientX - sx) + Math.abs(ev.clientY - sy) < 4) return;
        if (!moving) {
          moving = true; ghost = document.createElement('div'); ghost.className = 's-ghost'; ghost.innerHTML = icon('grip-vertical') + '<span></span>'; ghost.lastChild.textContent = col(k).title; document.body.appendChild(ghost);
          $('[data-k="' + k + '"]', table).forEach(function (el) { el.classList.add('dim'); });
        }
        ghost.style.left = (ev.clientX - 12) + 'px'; ghost.style.top = (ev.clientY - 14) + 'px'; showGap(gapAt(ev.clientX, ev.clientY), from);
      };
      var drop = function (ev) {
        th.removeEventListener('pointermove', drag); th.removeEventListener('pointerup', drop);
        if (ghost) ghost.remove(); line.hidden = true;
        if (!moving) { current = k; render({ head: k }); return; }
        var g = gapAt(ev.clientX, ev.clientY); moveCol(from, g > from ? g - 1 : g); render({ head: k });
      };
      th.addEventListener('pointermove', drag); th.addEventListener('pointerup', drop);
    });
    box.addEventListener('keydown', function (e) { if (e.key === 'Escape' && Object.keys(selected).length && !openList) { selected = {}; render(); } });
    box.querySelector('.add .rows').addEventListener('click', function () {
      var n = Math.max(1, Math.min(100, parseInt(countField.value, 10) || 1)), first = null;
      for (var i = 0; i < n; i++) { var b = blank(); rows.push(b); if (!first) first = b; }
      countField.value = n; if (sort.dir) sort = { key: null, dir: 0 };  // new rows show at the end, where they were added
      render({ id: first.id, key: cols[0].key });
    });
    box.querySelector('.add .column').addEventListener('click', function () { insertCol(cols.length); });
    countField.addEventListener('keydown', function (e) { if (e.key === 'Enter') box.querySelector('.add .rows').click(); });
    // the controls for this table elsewhere on the page: data-grid="its id", data-opt (head, sub, cell, sorting, design) or
    // data-col (title, subtitle, type, footer, sizing, min, max; terms is the formula's editor)
    function bind(el, apply, read) { el.addEventListener(el.matches('input') ? 'input' : 'change', function () { apply(read(el)); }); }
    $('[data-grid="' + box.id + '"]').forEach(function (el) {
      var o = el.dataset.opt, k = el.dataset.col;
      if (k === 'terms') return;
      var read = el.classList.contains('s-switch') ? function () { return el.getAttribute('aria-checked') === 'true'; } : el.matches('input') ? function () { return el.value; } : function () { return el.value(); };
      if (o) bind(el, function (v) {
        opt[o] = o === 'sorting' || o === 'design' ? v : parseInt(v, 10);
        if ((o === 'sorting' && !v) || (o === 'design' && v)) sort = { key: null, dir: 0 };  // back to the original order
        render();
      }, read);
      if (k) bind(el, function (v) {
        var c = col(current); if (!c) return;
        if (k === 'title' || k === 'subtitle') c[k] = v;
        else if (k === 'sizing') { c.sizing = Object.keys(SIZES).find(function (z) { return SIZES[z] === v; }); if (c.sizing === 'fixed' && !c.width) c.width = Math.round($('col.c', table)[cols.indexOf(c)].getBoundingClientRect().width); }
        else if (k === 'min' || k === 'max') { var n = parseInt(v, 10); c[k] = n > 0 ? n : null; layout(); return; }
        else if (k === 'footer') c.footer = Object.keys(FNS).find(function (f) { return FNS[f] === v; });
        else if (k === 'type') {
          c.type = Object.keys(TYPES).find(function (t) { return TYPES[t] === v; });
          if (c.type === 'unit' && !c.units) c.units = ['pcs', 'box', 'pallet'];
          if (c.type === 'computed' && !(c.terms && c.terms.length)) { var src = cols.filter(function (x) { return x !== c && numeric(x); }); c.terms = src.slice(0, 2).map(function (x, i) { return i ? { op: '+', k: x.key } : { k: x.key }; }); }
          if (!numeric(c) && c.footer !== 'count') c.footer = 'none';
        }
        render();
      }, read);
    });
    // the formula: a column, then any number of "op column" terms; × and ÷ count before + and −
    function termsEditor(host, c) {
      var sources = cols.filter(function (x) { return numeric(x) && x !== c; });
      host.innerHTML = '';
      c.terms.forEach(function (t, i) {
        var line = document.createElement('div'); line.className = 'term';
        line.innerHTML = (i ? '<span class="s-select op" data-items="+|−|×|÷" data-current="' + t.op + '"></span>' : '<span class="eq">=</span>') +
          '<span class="s-select" data-items="' + sources.map(function (x) { return esc(x.title); }).join('|') + '" data-current="' + esc(col(t.k) ? col(t.k).title : '') + '"></span>' +
          (c.terms.length > 1 ? '<button type="button" class="s-btn ghost square rm" data-tip="Remove this term" aria-label="Remove this term">' + icon('x') + '</button>' : '');
        host.appendChild(line);
        $('.s-select', line).forEach(function (sel) {
          select(sel);
          sel.addEventListener('change', function (e) { if (sel.classList.contains('op')) t.op = e.detail.value; else t.k = sources[e.detail.index].key; render(); });
        });
        var rm = line.querySelector('.rm'); if (rm) rm.addEventListener('click', function () { c.terms.splice(i, 1); if (c.terms[0]) delete c.terms[0].op; render(); });
      });
      var add = document.createElement('button'); add.type = 'button'; add.className = 's-btn ghost'; add.innerHTML = icon('plus') + 'Add a column';
      add.addEventListener('click', function () { var used = c.terms.map(function (t) { return t.k; }), next = sources.find(function (x) { return used.indexOf(x.key) < 0; }) || sources[0]; c.terms.push({ op: '+', k: next.key }); render(); });
      host.appendChild(add);
    }
    box.addEventListener('column', function (e) {
      var c = e.detail; if (!c) return;
      $('[data-grid="' + box.id + '"][data-col]').forEach(function (el) {
        var k = el.dataset.col, wrap = el.closest('[data-when]');
        if (wrap) wrap.hidden = wrap.dataset.when === 'computed' && c.type !== 'computed';
        if (k === 'title' || k === 'subtitle' || k === 'min' || k === 'max') { if (document.activeElement !== el) el.value = c[k] || ''; }
        else if (k === 'sizing') el.set(Object.keys(SIZES).map(function (z) { return SIZES[z]; }), SIZES[c.sizing]);
        else if (k === 'footer') el.set(numeric(c) ? Object.keys(FNS).map(function (f) { return FNS[f]; }) : ['None', 'Count'], FNS[c.footer]);
        else if (k === 'type') el.set(Object.keys(TYPES).map(function (t) { return TYPES[t]; }), TYPES[c.type]);
        else if (k === 'terms' && c.type === 'computed') termsEditor(el, c);
      });
      $('[data-grid-name="' + box.id + '"]').forEach(function (el) { el.textContent = c.title; });
    });
    new ResizeObserver(function () { layout(); }).observe(scroll);
    if (document.fonts) document.fonts.ready.then(layout);
    render();
  });

  // a popup that floats over the page (fixed, under its anchor, above it when there's no room below): never cut or split by a
  // scrolling area or the page's masonry columns
  function float(card, anchor) {
    if (card.parentElement !== document.body) document.body.appendChild(card);
    var r = anchor.getBoundingClientRect(), h = card.offsetHeight, w = card.offsetWidth;
    card.style.left = Math.max(8, Math.min(r.left, innerWidth - w - 8)) + 'px';
    card.style.top = (r.bottom + 4 + h > innerHeight && r.top - 4 - h > 0 ? r.top - 4 - h : r.bottom + 4) + 'px';
  }

  // the date field (DateField): YYYY, MM and DD are blocks; a click picks a block and opens the month, digits fill the block
  // and move on, arrows Up/Down change it, Left/Right move between blocks, Backspace empties it; Alt+Down opens the month for
  // the keyboard. A range (.range) has a start and an end, and the month colours the days between them.
  function iso(d) { return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2); }
  function readDay(t) {
    var m = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(t.trim()) || /^(\d{1,2})\. ?(\d{1,2})\. ?(\d{4})$/.exec(t.trim());
    if (!m) return null;
    var y = +(m[1].length === 4 ? m[1] : m[3]), mo = +m[2], d = +(m[1].length === 4 ? m[3] : m[1]), day = new Date(y, mo - 1, d);
    return day.getMonth() === mo - 1 && day.getDate() === d ? day : null;
  }
  function daysIn(y, m) { return new Date(y || 2000, m, 0).getDate(); }  // m 1..12; no year yet: a leap year, so 29 Feb can be typed
  var SPEC = [{ p: 'y', n: 4, ph: 'YYYY', min: 1, max: 9999 }, { p: 'm', n: 2, ph: 'MM', min: 1, max: 12 }, { p: 'd', n: 2, ph: 'DD', min: 1, max: 31 }];
  $('.s-date').forEach(function (box) {
    var range = box.classList.contains('range'), parts = (box.dataset.value || '').split('/');
    var ends = [readDay(parts[0] || '')].concat(range ? [readDay(parts[1] || '')] : []);  // the day of each group, or null
    var groups = [], target = 0, hover = null, cursor, keys = false, card = document.createElement('div');
    card.className = 's-month' + (box.dataset.weekends ? ' ' + box.dataset.weekends : '') + (range ? ' range' : ''); card.tabIndex = -1; card.hidden = true;
    box.innerHTML = '';
    ends.forEach(function (day, g) {
      if (g) box.insertAdjacentHTML('beforeend', '<span class="to" aria-hidden="true">–</span>');
      var el = document.createElement('span'); el.className = 'segs'; el.tabIndex = 0; el.setAttribute('role', 'group');
      el.setAttribute('aria-label', (box.getAttribute('aria-label') || 'Date') + (range ? (g ? ', end' : ', start') : ''));
      el.innerHTML = SPEC.map(function (sp, i) { return (i ? '<span class="sep">-</span>' : '') + '<span class="seg" data-p="' + sp.p + '" role="spinbutton" aria-label="' + { y: 'Year', m: 'Month', d: 'Day' }[sp.p] + '"></span>'; }).join('');
      var grp = { el: el, v: day ? { y: day.getFullYear(), m: day.getMonth() + 1, d: day.getDate() } : { y: null, m: null, d: null }, at: 0, typed: '' };
      groups.push(grp); box.appendChild(el); paint(grp);
      el.addEventListener('focus', function () { target = g; if (!card.hidden) draw(); });
      el.addEventListener('blur', function () { grp.typed = ''; SPEC.forEach(function (sp) { if (grp.v[sp.p] === 0) grp.v[sp.p] = null; }); paint(grp); el.querySelectorAll('.seg').forEach(function (s) { s.classList.remove('on'); }); });
      el.addEventListener('keydown', function (e) { key(grp, g, e); });
      el.addEventListener('paste', function (e) { e.preventDefault(); var d = readDay((e.clipboardData || window.clipboardData).getData('text')); if (d) set(g, d); else box.classList.add('invalid'); });
      $('.seg', el).forEach(function (s, i) { s.addEventListener('pointerdown', function () { grp.at = i; grp.typed = ''; setTimeout(function () { paint(grp); }); }); });
    });
    box.insertAdjacentHTML('beforeend', '<button type="button" class="cal" tabindex="-1" data-tip="Calendar" aria-label="Calendar">' + icon('calendar') + '</button>');
    function paint(grp) {
      if (!grp.sized) { grp.sized = true; $('.seg', grp.el).forEach(function (s, i) { s.style.minWidth = Math.ceil(Math.max(textWidth(SPEC[i].ph, 13), textWidth('0000'.slice(0, SPEC[i].n), 13)) + 2) + 'px'; }); }
      $('.seg', grp.el).forEach(function (s, i) {
        var sp = SPEC[i], v = grp.v[sp.p];
        s.textContent = v == null ? sp.ph : String(v).padStart(sp.n, '0');
        s.classList.toggle('empty', v == null); s.classList.toggle('on', i === grp.at && document.activeElement === grp.el);
        if (v != null) s.setAttribute('aria-valuenow', v); else s.removeAttribute('aria-valuenow');
      });
    }
    function dayOf(grp) { var v = grp.v; if (!(v.y >= 1 && v.m >= 1 && v.d >= 1)) return null; return new Date(v.y, v.m - 1, Math.min(v.d, daysIn(v.y, v.m))); }
    function changed(g) {
      var grp = groups[g], v = grp.v;
      if (v.d != null && v.m != null) v.d = Math.min(v.d, daysIn(v.y, v.m));  // 31 in a 30-day month becomes 30
      ends[g] = dayOf(grp); box.classList.remove('invalid'); paint(grp);
      if (!card.hidden) { if (ends[g]) cursor = new Date(ends[g]); draw(); }
      box.dispatchEvent(new Event('change'));
    }
    function set(g, d) { var grp = groups[g]; grp.v = d ? { y: d.getFullYear(), m: d.getMonth() + 1, d: d.getDate() } : { y: null, m: null, d: null }; changed(g); }
    function key(grp, g, e) {
      var sp = SPEC[grp.at], v = grp.v;
      if (e.altKey && e.key === 'ArrowDown' || e.key === 'F4') { e.preventDefault(); open(true); return; }
      if (/^\d$/.test(e.key)) {
        e.preventDefault(); grp.typed += e.key;
        var n = +grp.typed, full = grp.typed.length >= sp.n || n * 10 > sp.max;  // "4" in the month can only be April: move on
        v[sp.p] = full ? Math.min(Math.max(n, sp.min), sp.max) : n;            // a lone "0" waits for its second digit
        if (full) { grp.typed = ''; if (grp.at < 2) grp.at++; }
        changed(g); return;
      }
      grp.typed = '';
      if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
        e.preventDefault(); var step = e.key === 'ArrowUp' ? 1 : -1, max = sp.p === 'd' ? daysIn(v.y, v.m || 1) : sp.max, now = new Date();
        v[sp.p] = v[sp.p] == null ? (sp.p === 'y' ? now.getFullYear() : sp.p === 'm' ? now.getMonth() + 1 : now.getDate())
          : sp.p === 'y' ? Math.max(1, v.y + step) : (v[sp.p] - 1 + step + max) % max + 1;
        changed(g);
      } else if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
        var to = grp.at + (e.key === 'ArrowLeft' ? -1 : 1);
        if (to >= 0 && to <= 2) { e.preventDefault(); grp.at = to; paint(grp); }
        else if (range && groups[g + (to < 0 ? -1 : 1)]) { e.preventDefault(); var next = groups[g + (to < 0 ? -1 : 1)]; next.at = to < 0 ? 2 : 0; next.el.focus(); paint(next); }
      } else if (e.key === '-' || e.key === '.' || e.key === '/') { e.preventDefault(); if (grp.at < 2) { grp.at++; paint(grp); } }
      else if (e.key === 'Backspace' || e.key === 'Delete') { e.preventDefault(); v[sp.p] = null; changed(g); }
      else if (e.key === 'Escape' && !card.hidden) { e.preventDefault(); close(); }
    }
    function same(a, b) { return a && b && a.toDateString() === b.toDateString(); }
    function draw() {
      var first = new Date(cursor.getFullYear(), cursor.getMonth(), 1), start = new Date(first), today = new Date();
      start.setDate(1 - (first.getDay() + 6) % 7);
      var a = ends[0], b = range ? (ends[1] || (target === 1 && a && hover ? hover : null)) : null;
      if (a && b && b < a) { var t = a; a = b; b = t; }
      var h = '<div class="top"><b>' + first.toLocaleString('en', { month: 'long' }) + ' ' + first.getFullYear() + '</b>' +
        '<button type="button" class="prev" aria-label="Previous month">' + icon('chevron-left') + '</button><button type="button" class="next" aria-label="Next month">' + icon('chevron-left') + '</button></div>';
      h += '<div class="band" style="grid-column:6"></div><div class="band" style="grid-column:7"></div>';
      ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'].forEach(function (n, c) { h += '<div class="wd' + (c >= 5 ? ' we' : '') + '" style="grid-row:2;grid-column:' + (c + 1) + '">' + n + '</div>'; });
      for (var i = 0; i < 42; i++) {
        var d = new Date(start); d.setDate(start.getDate() + i);
        var inside = range && a && b && d > a && d < b, edge = range && (same(d, a) || same(d, b));
        var cls = 'day' + (d.getMonth() !== first.getMonth() ? ' out' : '') + ((d.getDay() + 6) % 7 >= 5 ? ' we' : '') + (same(d, today) ? ' today' : '') +
          (range ? (inside ? ' in' : '') + (same(d, a) && b ? ' from' : '') + (same(d, b) && a ? ' until' : '') + (edge ? ' picked' : '') : same(d, ends[0]) ? ' picked' : '') + (keys && same(d, cursor) ? ' cursor' : '');
        h += '<button type="button" class="' + cls + '" style="grid-row:' + (3 + Math.floor(i / 7)) + ';grid-column:' + (i % 7 + 1) + '" data-day="' + iso(d) + '">' + d.getDate() + '</button>';
      }
      var span = range && a && b ? Math.round((b - a) / 864e5) + 1 : 0;
      card.innerHTML = h + '<div class="foot"><button type="button" class="today">Today</button>' + (range ? '<span class="span">' + (span ? span + (span === 1 ? ' day' : ' days') : target ? 'Pick the end' : 'Pick the start') + '</span>' : '') + '<button type="button" class="clear">Clear</button></div>';
      if (card.parentElement === document.body) float(card, box);
    }
    function open(byKeys) {
      cursor = ends[target] ? new Date(ends[target]) : ends[0] ? new Date(ends[0]) : new Date(); keys = byKeys; hover = null;
      card.hidden = false; box.classList.add('open'); draw(); float(card, box); if (byKeys) card.focus();
    }
    function close() { if (card.hidden) return; card.hidden = true; box.classList.remove('open'); groups[target].el.focus(); }
    function pick(d) {
      if (!range) { set(0, d); close(); return; }
      if (!d) { set(0, null); set(1, null); target = 0; draw(); groups[0].el.focus(); return; }
      if (target === 0) { set(0, d); set(1, null); target = 1; groups[1].el.focus(); draw(); return; }  // a new start asks for its end
      set(1, d);
      if (ends[0] && ends[1] < ends[0]) { var t = ends[0]; set(0, ends[1]); set(1, t); }
      close();
    }
    function move(d) { cursor = d; keys = true; draw(); }
    // a click anywhere in the field opens the month (the icon closes it again); a click on a block also picks the block
    box.addEventListener('pointerdown', function (e) {
      var g = groups.findIndex(function (x) { return x.el.contains(e.target); });
      if (e.target.closest('.cal') && !card.hidden) { e.preventDefault(); close(); return; }
      if (g < 0) { e.preventDefault(); g = target; groups[g].el.focus(); }
      target = g; if (card.hidden) open(false); else draw();
    });
    card.addEventListener('mousedown', function (e) { e.preventDefault(); });
    card.addEventListener('pointerover', function (e) { var b = e.target.closest('[data-day]'); if (range && target === 1 && ends[0] && !ends[1] && b && !(hover && iso(hover) === b.dataset.day)) { hover = readDay(b.dataset.day); draw(); } });
    card.addEventListener('click', function (e) {
      var b = e.target.closest('button'); if (!b) return;
      if (b.classList.contains('prev')) move(new Date(cursor.getFullYear(), cursor.getMonth() - 1, cursor.getDate()));
      else if (b.classList.contains('next')) move(new Date(cursor.getFullYear(), cursor.getMonth() + 1, cursor.getDate()));
      else if (b.classList.contains('today')) pick(new Date(new Date().toDateString()));
      else if (b.classList.contains('clear')) pick(null);
      else if (b.dataset.day) pick(readDay(b.dataset.day));
    });
    card.addEventListener('keydown', function (e) {
      var d = new Date(cursor), step = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[e.key];
      if (step) d.setDate(d.getDate() + step);
      else if (e.key === 'PageUp') d.setMonth(d.getMonth() - 1);
      else if (e.key === 'PageDown') d.setMonth(d.getMonth() + 1);
      else if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); pick(new Date(cursor)); if (!card.hidden) card.focus(); return; }
      else if (e.key === 'Escape') { e.preventDefault(); close(); return; }
      else return;
      e.preventDefault(); if (range && target === 1 && ends[0] && !ends[1]) hover = new Date(d); move(d);
    });
    document.addEventListener('pointerdown', function (e) { if (!card.hidden && !box.contains(e.target) && !card.contains(e.target)) { card.hidden = true; box.classList.remove('open'); } });
    addEventListener('scroll', function () { if (!card.hidden) float(card, box); }, true);
    addEventListener('resize', function () { if (!card.hidden) float(card, box); });
    box.value = function () { return range ? [ends[0] && iso(ends[0]), ends[1] && iso(ends[1])] : ends[0] && iso(ends[0]); };
  });
  // the accent picker (AccentPicker): a swatch or a typed colour sets the accent; the field opens its colour helper
  $('.s-accent').forEach(function (row) {
    var name = row.querySelector('.name'), hex = row.querySelector('.hex'), warn = row.querySelector('.warn'), pop = row.querySelector('.pop');
    function show(typing) {
      var cur = css('accent').toUpperCase(), bg = rgb(css('background')), c = contrast(rgb(cur), bg);
      name.textContent = accent ? accent.name || cur : 'Violet';
      $('.swatches button', row).forEach(function (b) { b.classList.toggle('current', (accent ? accent.name : 'Violet') === b.dataset.name); });
      if (!typing) { hex.value = cur; hex.classList.remove('invalid'); }
      warn.textContent = c < 4.5 ? 'Low contrast (' + c.toFixed(1) + ' : 1): text in this colour is hard to read' : '';
      warn.classList.toggle('low', c < 4.5); warn.hidden = !warn.textContent;
    }
    function set(pair, typing) { accent = pair; applyAccent(); show(typing); }
    $('.swatches button', row).forEach(function (b) {
      b.addEventListener('click', function () { set({ dark: b.dataset.dark, light: b.dataset.light, name: b.dataset.name }); });
    });
    hex.addEventListener('input', function () {
      var t = hex.value.trim(); if (t[0] !== '#') t = '#' + t;
      if (/^#[0-9a-f]{6}$/i.test(t)) set({ dark: t.toUpperCase(), light: t.toUpperCase() }, true); else hex.classList.toggle('invalid', t.length >= 7);
    });
    function open() { if (pop.hidden) { pop.hidden = false; pop.querySelector('.s-picker').show(); } }
    hex.addEventListener('focus', open); hex.addEventListener('click', open);
    document.addEventListener('pointerdown', function (e) { if (!pop.hidden && !pop.contains(e.target) && e.target !== hex) pop.hidden = true; });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') pop.hidden = true; });
    document.addEventListener('shade-accent', function () { show(); });
    row.show = show; show();
  });
  // a new mode makes the accent's shades again, as SetMode does
  new MutationObserver(function () { applyAccent(); $('.s-picker, .s-accent').forEach(function (p) { p.show(); }); })
    .observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
})();
