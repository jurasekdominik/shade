// Shade for the web: the behaviour of the components in shade.css (the Avalonia controls' rules: Esc closes, arrows
// move, Enter picks, a list is one shape with its field). Plain script, no dependencies.
(function () {
  'use strict';
  var $ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };
  var icon = function (name, cls) { return '<svg class="s-i ' + (cls || '') + '" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; };
  var openList = null;  // one open list at a time: { close }

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
      row.lastChild.textContent = it;
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
  $('.s-select').forEach(function (box) {
    var items = box.dataset.items.split('|'), colors = box.dataset.colors ? box.dataset.colors.split('|') : null;
    var multi = box.hasAttribute('data-multi'), chosen = {}, current = 0;
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
    show();
  });

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
      var menu = listMenu(found, { attached: true, pick: function (i) { input.value = found[i]; closeOpen(); input.focus(); } });
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
  $('.s-switch').forEach(function (b) { b.addEventListener('click', function () { b.setAttribute('aria-checked', b.getAttribute('aria-checked') !== 'true'); }); });
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
    buttons.forEach(function (b) { b.setAttribute('role', 'tab'); b.addEventListener('click', function () { buttons.forEach(function (x) { x.setAttribute('aria-selected', x === b); }); place(); }); });
    tabs.setAttribute('role', 'tablist');
    place(); new ResizeObserver(place).observe(tabs);
  });

  // removable tags
  $('.s-tag [data-remove]').forEach(function (b) { b.addEventListener('click', function () { b.parentElement.remove(); }); });

  // tables: a click selects a row (Ctrl adds); a header click sorts, again reverses
  $('.s-table.rows').forEach(function (table) {
    var body = table.tBodies[0];
    body.addEventListener('click', function (e) {
      var tr = e.target.closest('tr'); if (!tr) return;
      if (!(e.ctrlKey || e.metaKey)) $('tr.selected', body).forEach(function (r) { if (r !== tr) r.classList.remove('selected'); });
      tr.classList.toggle('selected', e.ctrlKey || e.metaKey ? !tr.classList.contains('selected') : true);
    });
    if (!table.classList.contains('sortable')) return;
    $('th', table).forEach(function (th, col) {
      th.addEventListener('click', function () {
        var up = th.getAttribute('aria-sort') !== 'ascending';
        $('th', table).forEach(function (h) { h.removeAttribute('aria-sort'); });
        th.setAttribute('aria-sort', up ? 'ascending' : 'descending');
        var num = th.classList.contains('num');
        $('tr', body).sort(function (a, b) {
          var x = a.cells[col].textContent, y = b.cells[col].textContent;
          var d = num ? parseFloat(x) - parseFloat(y) : x.localeCompare(y);
          return up ? d : -d;
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
    cards.forEach(function (c) { c.addEventListener('click', function () { var on = c.getAttribute('aria-pressed') !== 'true'; cards.forEach(function (x) { x.setAttribute('aria-pressed', x === c && on); }); }); });
  });

  // search: × once typed, Esc or × clears; data-of="12" shows "n of 12" for the rows of data-list that match
  $('.s-search').forEach(function (box) {
    var input = box.querySelector('input'), clear = box.querySelector('.clear'), count = box.querySelector('.count');
    var list = box.dataset.list && document.querySelector(box.dataset.list);
    function show() {
      var q = input.value.trim().toLowerCase(); clear.hidden = !q;
      if (list) {
        var rows = $('tr, li', list).filter(function (r) { return !r.closest('thead'); }), n = 0;
        rows.forEach(function (r) { var hit = !q || r.textContent.toLowerCase().indexOf(q) >= 0; r.hidden = !hit; if (hit) n++; });
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
    ['accent', 'accent-hover', 'accent-tint', 'accent-text', 'on-accent'].forEach(function (k) { root.removeProperty('--' + k); });
    if (accent) {
    var hex = dark() ? accent.dark : accent.light;
    var a = rgb(hex), raised = rgb(css('raised')), far = rgb(css('far')), ink = [21, 21, 25], white = [255, 255, 255], text = a;
    for (var i = 0; i < 20 && contrast(text, raised) < 4.5; i++) text = blend(text, far, 0.08);
    root.setProperty('--accent', hex);
    root.setProperty('--accent-hover', hexOf(blend(a, white, 0.10)));
    root.setProperty('--accent-tint', hexOf(blend(raised, a, 0.15)));
    root.setProperty('--accent-text', hexOf(text));
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
  // the table designer (TableDesigner): drag a header from anywhere on it, or a field, to a gap; menu and keyboard
  $('.s-designer').forEach(function (box) {
    var cols = box.dataset.columns.split('|').map(function (c) { var p = c.split(':'); return { h: p[0], num: p[1] === 'num' }; });
    var rows = box.dataset.rows.split(';').map(function (r) { return r.split('|'); }), selected = 0, line = document.createElement('div');
    line.className = 'line'; line.hidden = true;
    function icon(n) { return '<svg class="s-i" aria-hidden="true"><use href="#i-' + n + '"/></svg>'; }
    function draw(focus) {
      var h = '<table><thead><tr>' + cols.map(function (c, i) { return '<th tabindex="0" data-i="' + i + '" class="' + (i === selected ? 'selected' : '') + '">' + icon('grip-vertical') + c.h + '</th>'; }).join('') + '</tr></thead><tbody>';
      rows.forEach(function (r) { h += '<tr>' + cols.map(function (c, i) { return '<td class="' + (c.num ? 'num' : '') + '">' + (r[i] || '') + '</td>'; }).join('') + '</tr>'; });
      box.innerHTML = h + '</tbody></table>'; box.appendChild(line);
      $('th', box).forEach(wire);
      if (focus != null && box.querySelectorAll('th')[focus]) box.querySelectorAll('th')[focus].focus();
    }
    function move(from, to) {
      if (to === from || to < 0 || to >= cols.length) return;
      cols.splice(to, 0, cols.splice(from, 1)[0]); rows.forEach(function (r) { r.splice(to, 0, r.splice(from, 1)[0]); });
      if (selected === from) selected = to; else if (from < selected && to >= selected) selected--; else if (from > selected && to <= selected) selected++;
    }
    function insert(at, name) { cols.splice(at, 0, { h: name, num: false }); rows.forEach(function (r) { r.splice(at, 0, '—'); }); selected = at; }
    // the gap nearest x (0..count), or -1 off the table
    function gapAt(x, y) {
      var t = box.querySelector('table').getBoundingClientRect();
      if (y < t.top - 16 || y > t.bottom + 16 || x < t.left - 40 || x > t.right + 40) return -1;
      var edges = [t.left].concat($('th', box).map(function (th) { return th.getBoundingClientRect().right; })), best = 0;
      edges.forEach(function (e, i) { if (Math.abs(x - e) < Math.abs(x - edges[best])) best = i; });
      return best;
    }
    function show(g, from) {
      var shown = g >= 0 && (from < 0 || (g !== from && g !== from + 1)); line.hidden = !shown; if (!shown) return;
      var t = box.querySelector('table'), b = box.getBoundingClientRect(), r = t.getBoundingClientRect(), ths = $('th', box);
      var x = g === 0 ? r.left : ths[g - 1].getBoundingClientRect().right;
      line.style.left = (x - b.left + box.scrollLeft) + 'px'; line.style.top = (r.top - b.top) + 'px'; line.style.height = r.height + 'px';
    }
    // a drag from el: label for the ghost, from = the column's index (-1: a field), done(gap)
    function drag(el, label, from, done, click) {
      el.addEventListener('pointerdown', function (e) {
        if (e.button !== 0) return;
        var sx = e.clientX, sy = e.clientY, moving = false, ghost = null; el.setPointerCapture(e.pointerId);
        function mv(ev) {
          if (!moving && Math.abs(ev.clientX - sx) + Math.abs(ev.clientY - sy) < 4) return;
          if (!moving) { moving = true; ghost = document.createElement('div'); ghost.className = 's-ghost'; ghost.innerHTML = icon('grip-vertical') + '<span></span>'; ghost.lastChild.textContent = label; document.body.appendChild(ghost);
            if (from >= 0) $('tr', box).forEach(function (tr) { tr.children[from].classList.add('dim'); }); }
          ghost.style.left = (ev.clientX - 12) + 'px'; ghost.style.top = (ev.clientY - 14) + 'px'; show(gapAt(ev.clientX, ev.clientY), from);
        }
        function up(ev) {
          el.removeEventListener('pointermove', mv); el.removeEventListener('pointerup', up);
          if (ghost) ghost.remove(); line.hidden = true;
          if (moving) { var g = gapAt(ev.clientX, ev.clientY); if (g >= 0) done(g); else draw(); } else if (click) click();
        }
        el.addEventListener('pointermove', mv); el.addEventListener('pointerup', up);
      });
    }
    function menu(th, i, x, y) {
      closeOpen();
      var m = listMenu(['Insert column before', 'Insert column after', 'Remove column'], { pick: function (k) { closeOpen(); if (k === 2) { cols.splice(i, 1); rows.forEach(function (r) { r.splice(i, 1); }); selected = -1; } else insert(i + k, 'New column'); draw(); } });
      document.body.appendChild(m);
      m.style.left = Math.min(x, innerWidth - m.offsetWidth - 8) + 'px'; m.style.top = Math.min(y, innerHeight - m.offsetHeight - 8) + 'px';
      openList = { owner: m, menu: m, close: function () { m.remove(); } };
    }
    function wire(th) {
      var i = +th.dataset.i;
      drag(th, cols[i].h, i, function (g) { move(i, g > i ? g - 1 : g); draw(); }, function () { selected = i; draw(i); });
      th.addEventListener('contextmenu', function (e) { e.preventDefault(); menu(th, i, e.clientX, e.clientY); });
      th.addEventListener('keydown', function (e) {
        if (e.ctrlKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) { var to = i + (e.key === 'ArrowLeft' ? -1 : 1); if (to >= 0 && to < cols.length) { move(i, to); draw(to); } }
        else if (e.key === 'ArrowLeft' && th.previousElementSibling) th.previousElementSibling.focus();
        else if (e.key === 'ArrowRight' && th.nextElementSibling) th.nextElementSibling.focus();
        else if (e.key === 'Enter' || e.key === ' ') { selected = i; draw(i); }
        else if (e.key === 'ContextMenu' || (e.shiftKey && e.key === 'F10')) { var r = th.getBoundingClientRect(); menu(th, i, r.left, r.bottom); }
        else return;
        e.preventDefault();
      });
    }
    $('[data-field]', box.parentElement).forEach(function (f) { drag(f, f.dataset.field, -1, function (g) { insert(g, f.dataset.field); draw(); }); });
    draw();
  });

  // the date field (DateField): typed as 2026-10-24 or 24.10.2026, or picked in the month the icon opens
  function iso(d) { return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2); }
  function readDay(t) {
    var m = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(t.trim()) || /^(\d{1,2})\. ?(\d{1,2})\. ?(\d{4})$/.exec(t.trim());
    if (!m) return null;
    var y = +(m[1].length === 4 ? m[1] : m[3]), mo = +m[2], d = +(m[1].length === 4 ? m[3] : m[1]), day = new Date(y, mo - 1, d);
    return day.getMonth() === mo - 1 && day.getDate() === d ? day : null;
  }
  $('.s-date').forEach(function (box) {
    var input = box.querySelector('input'), value = readDay(input.value), cursor, card = document.createElement('div');
    card.className = 's-month' + (box.dataset.weekends ? ' ' + box.dataset.weekends : ''); card.tabIndex = -1; card.hidden = true; box.appendChild(card);
    var names = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'], keys = false;
    function same(a, b) { return a && b && a.toDateString() === b.toDateString(); }
    function draw() {
      var first = new Date(cursor.getFullYear(), cursor.getMonth(), 1), start = new Date(first), today = new Date();
      start.setDate(1 - (first.getDay() + 6) % 7);
      var h = '<div class="top"><b>' + first.toLocaleString('en', { month: 'long' }) + ' ' + first.getFullYear() + '</b>' +
        '<button type="button" class="prev" aria-label="Previous month">' + icon('chevron-left') + '</button><button type="button" class="next" aria-label="Next month">' + icon('chevron-left') + '</button></div>';
      h += '<div class="band" style="grid-column:6"></div><div class="band" style="grid-column:7"></div>';
      names.forEach(function (n, c) { h += '<div class="wd' + (c >= 5 ? ' we' : '') + '" style="grid-row:2;grid-column:' + (c + 1) + '">' + n + '</div>'; });
      for (var i = 0; i < 42; i++) {
        var d = new Date(start); d.setDate(start.getDate() + i);
        var cls = 'day' + (d.getMonth() !== first.getMonth() ? ' out' : '') + ((d.getDay() + 6) % 7 >= 5 ? ' we' : '') + (same(d, today) ? ' today' : '') + (same(d, value) ? ' picked' : '') + (keys && same(d, cursor) ? ' cursor' : '');
        h += '<button type="button" class="' + cls + '" style="grid-row:' + (3 + Math.floor(i / 7)) + ';grid-column:' + (i % 7 + 1) + '" data-day="' + iso(d) + '">' + d.getDate() + '</button>';
      }
      card.innerHTML = h + '<div class="foot"><button type="button" class="today">Today</button><button type="button" class="clear">Clear</button></div>';
    }
    function icon(name) { return '<svg class="s-i" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }
    function open(byKeys) { cursor = value ? new Date(value) : readDay(input.value) || new Date(); keys = byKeys; card.hidden = false; draw(); if (byKeys) card.focus(); }
    function close() { card.hidden = true; }
    function pick(d) { value = d; input.value = d ? iso(d) : ''; box.classList.remove('invalid'); close(); input.focus(); }
    function move(d) { cursor = d; keys = true; draw(); }
    box.querySelector('.cal').addEventListener('click', function () { if (card.hidden) open(false); else close(); });
    card.addEventListener('mousedown', function (e) { e.preventDefault(); });
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
      else if (e.key === 'Enter' || e.key === ' ') { pick(cursor); e.preventDefault(); return; }
      else if (e.key === 'Escape') { close(); input.focus(); return; }
      else return;
      e.preventDefault(); move(d);
    });
    input.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); open(true); }
      else if (e.key === 'Enter') settle();
    });
    input.addEventListener('input', function () { var d = readDay(input.value); if (d) value = d; else if (!input.value.trim()) value = null; });
    function settle() { var t = input.value.trim(), d = readDay(t); if (d) { value = d; input.value = iso(d); } box.classList.toggle('invalid', !!t && !d); }
    input.addEventListener('blur', function () { if (card.hidden) settle(); });
    document.addEventListener('pointerdown', function (e) { if (!card.hidden && !box.contains(e.target)) close(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !card.hidden) close(); });
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
