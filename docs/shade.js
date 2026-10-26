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
    var terms = (q || '').trim().split(/\s+/).filter(Boolean), normalized = '', positions = [];
    // Keep original UTF-16 positions when folding accents, including decomposed characters.
    for (var at = 0; at < text.length;) {
      var end = at + String.fromCodePoint(text.codePointAt(at)).length;
      while (end < text.length && /\p{M}/u.test(String.fromCodePoint(text.codePointAt(end)))) end += String.fromCodePoint(text.codePointAt(end)).length;
      var folded = text.slice(at, end).normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase();
      for (var j = 0; j < folded.length; j++) positions.push({ start: at, end: end });
      normalized += folded; at = end;
    }
    var hits = new Array(text.length).fill(false);
    terms.forEach(function (term) {
      term = term.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase();
      if (!term) return;
      for (var at = normalized.indexOf(term); at >= 0; at = normalized.indexOf(term, at + 1))
        hits.fill(true, positions[at].start, positions[at + term.length - 1].end);
    });
    var out = '';
    for (var at = 0; at < text.length;) {
      var end = at + 1; while (end < text.length && hits[end] === hits[at]) end++;
      var part = esc(text.slice(at, end)); out += hits[at] ? '<mark class="s-hit">' + part + '</mark>' : part; at = end;
    }
    return out;
  }

  function closeOpen() { if (openList) { var o = openList; openList = null; o.close(); } }
  document.addEventListener('pointerdown', function (e) { if (openList && !openList.owner.contains(e.target) && !(openList.menu && openList.menu.contains(e.target))) closeOpen(); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeOpen(); });

  /** A list of rows under (attached) or at a point; keyboard: arrows, Enter. pick(index) on a click or Enter. */
  function listMenu(items, opt) {
    var menu = document.createElement('div');
    menu.className = 's-menu' + (opt.attached ? ' attached' : ' floating');
    menu.setAttribute('role', opt.tick ? 'group' : 'listbox');
    var rows = [], lit = opt.current == null ? -1 : opt.current;
    items.forEach(function (it, i) {
      if (it === '-') { menu.appendChild(document.createElement('hr')); return; }
      var row = document.createElement('button'); row.type = 'button';
      row.className = 'row' + (i === opt.current ? ' current' : '') + (opt.danger && opt.danger(i) ? ' danger' : '');
      row.setAttribute('role', opt.tick ? 'checkbox' : 'option');
      row.tabIndex = -1; if (opt.tick) row.setAttribute('aria-checked', !!opt.tick(i));
      if (opt.color && opt.color(i)) row.style.color = opt.color(i);
      row.innerHTML = (opt.tick ? '<span class="s-tick" aria-checked="' + !!opt.tick(i) + '">' + icon('check', 'check') + '</span>' : '') + '<span></span>';
      if (opt.mark) row.lastChild.innerHTML = marked(it, opt.mark); else row.lastChild.textContent = it;
      row.addEventListener('pointerenter', function () { if (!opt.tick) light(rows.indexOf(row)); });
      row.addEventListener('click', function () { row.focus({ preventScroll: true }); opt.pick(i); });
      if (opt.tick) { row.firstChild.setAttribute('aria-hidden', 'true'); row.addEventListener('focus', function () { lit = rows.indexOf(row); rows.forEach(function (r) { r.classList.toggle('lit', r === row); }); }); }
      row.dataset.index = i;
      rows.push(row); menu.appendChild(row);
    });
    function light(k) { lit = k; rows.forEach(function (r, j) { r.classList.toggle('lit', j === k); }); if (rows[k]) { var row = rows[k]; if (row.offsetTop < menu.scrollTop) menu.scrollTop = row.offsetTop; else if (row.offsetTop + row.offsetHeight > menu.scrollTop + menu.clientHeight) menu.scrollTop = row.offsetTop + row.offsetHeight - menu.clientHeight; if (!opt.mark) row.focus({ preventScroll: true }); } }
    light(rows.findIndex(function (r) { return +r.dataset.index === opt.current; }));
    menu.reveal = function () { light(lit); };
    var prefix = '', typedAt = 0;
    menu.key = function (e) {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); var n = rows.length; light(lit < 0 ? (e.key === 'ArrowDown' ? 0 : n - 1) : opt.tick ? Math.max(0, Math.min(n - 1, lit + (e.key === 'ArrowDown' ? 1 : -1))) : (lit + (e.key === 'ArrowDown' ? 1 : n - 1)) % n); return true; }
      if (e.key === 'Home' || e.key === 'End') { e.preventDefault(); light(e.key === 'Home' ? 0 : rows.length - 1); return true; }
      if ((e.key === 'Enter' || e.key === ' ') && lit >= 0 && !e.repeat) { e.preventDefault(); opt.pick(+rows[lit].dataset.index); return true; }
      if (!opt.mark && e.key.length === 1 && e.key !== ' ' && !e.ctrlKey && !e.metaKey && !e.altKey) {
        var now = Date.now(); prefix = now - typedAt > 700 ? e.key : prefix + e.key; typedAt = now;
        var order = rows.slice(lit + 1).concat(rows.slice(0, lit + 1));
        var hit = order.find(function (row) { return items[+row.dataset.index].toLocaleLowerCase().startsWith(prefix.toLocaleLowerCase()); });
        if (!hit) { prefix = e.key; hit = order.find(function (row) { return items[+row.dataset.index].toLocaleLowerCase().startsWith(prefix.toLocaleLowerCase()); }); }
        if (hit) light(rows.indexOf(hit)); e.preventDefault(); return true;
      }
      return false;
    };
    return menu;
  }

  // dropdown (ChoiceButton) and several-from-a-list (MultiChoice): data-items "A|B|C", data-colors "role|role" (CSS vars)
  // box.set(items, current) swaps the items; a pick fires 'change' with detail { index, value }
  function select(box) {
    var items = box.dataset.items ? box.dataset.items.split('|') : [], colors = box.dataset.colors ? box.dataset.colors.split('|') : null;
    var floating = box.hasAttribute('data-floating');
    var multi = box.hasAttribute('data-multi'), chosen = new Set(), current = Math.max(0, items.indexOf(box.dataset.current));
    var button = document.createElement('button'); button.type = 'button';
    button.setAttribute('aria-haspopup', multi ? 'dialog' : 'listbox');
    button.setAttribute('aria-expanded', 'false');
    button.innerHTML = '<span></span>' + icon('chevron-down');
    box.appendChild(button);
    function values() { return items.length ? items.filter(function (item) { return chosen.has(item); }) : Array.from(chosen).sort(); }
    function show() {
      var label = button.firstChild;
      if (multi) { var picked = values(); label.textContent = box.dataset.label || (picked.length > 2 ? picked.length + ' selected' : picked.length ? picked.join(', ') : box.dataset.empty === undefined ? 'All' : box.dataset.empty); }
      else { label.textContent = items[current] || ''; label.style.color = colors ? 'var(--' + colors[current] + ')' : ''; }
    }
    function open(last) {
      if (button.disabled || !items.length) return;
      if (openList && openList.owner === box) { closeOpen(); button.focus(); return; }
      closeOpen();
      var menu = listMenu(items, {
        attached: true, current: multi ? null : current,
        color: colors && function (i) { return 'var(--' + colors[i] + ')'; },
        tick: multi && function (i) { return chosen.has(items[i]); },
        pick: function (i) {
          if (button.disabled) return;
          if (multi) {
            if (chosen.has(items[i])) chosen.delete(items[i]); else chosen.add(items[i]);
            show(); sync(menu);
            box.dispatchEvent(new CustomEvent('change', { detail: { values: values() } })); return;
          }
          current = i; show(); closeOpen(); button.focus();
          box.dispatchEvent(new CustomEvent('change', { detail: { index: i, value: items[i] } }));
        },
      });
      if (multi) { menu.setAttribute('role', 'dialog'); menu.setAttribute('aria-label', box.dataset.label || box.dataset.empty || 'Choose options'); }
      if (floating) {
        menu.classList.add('floating'); document.body.appendChild(menu);
        var anchor = button.getBoundingClientRect(), below = innerHeight - anchor.bottom - 8, above = anchor.top - 8;
        var upwards = below < Math.min(290, menu.scrollHeight) && above > below;
        menu.classList.toggle('above', upwards); box.classList.toggle('above', upwards);
        menu.style.width = anchor.width + 'px'; menu.style.maxHeight = Math.max(28, Math.min(290, upwards ? above : below)) + 'px';
        menu.style.left = anchor.left + 'px'; menu.style.top = (upwards ? anchor.top - menu.getBoundingClientRect().height : anchor.bottom) + 'px';
      } else box.appendChild(menu);
      box.classList.add('open'); button.setAttribute('aria-expanded', 'true');
      var moved = function (event) { if (!menu.contains(event.target)) closeOpen(); };
      if (floating) { document.addEventListener('scroll', moved, true); window.addEventListener('resize', closeOpen); }
      openList = { owner: box, menu: menu, close: function () {
        menu.remove(); box.classList.remove('open', 'above'); button.setAttribute('aria-expanded', 'false');
        if (floating) { document.removeEventListener('scroll', moved, true); window.removeEventListener('resize', closeOpen); }
      } };
      {
        menu.addEventListener('keydown', function (e) {
          if (e.key === 'Escape' || e.key === 'Tab') { closeOpen(); button.focus(); if (e.key === 'Escape') e.preventDefault(); }
          else { menu.key(e); e.stopPropagation(); }
        });
        var at = multi ? (last ? items.length - 1 : items.findIndex(function (item) { return chosen.has(item); })) : current;
        var rows = $('.row', menu); menu.reveal(); rows[Math.max(0, at)].focus({ preventScroll: true });
      }
    }
    function sync(menu) {
      $('.row', menu).forEach(function (row) {
        var checked = chosen.has(items[+row.dataset.index]);
        row.setAttribute('aria-checked', checked); row.querySelector('.s-tick').setAttribute('aria-checked', checked);
      });
    }
    button.addEventListener('click', function () { open(false); });
    button.addEventListener('keydown', function (e) {
      if (openList && openList.owner === box && openList.menu.key(e)) return;
      if (e.key === 'ArrowDown' || multi && (e.key === 'ArrowUp' || e.key === 'F4')) { e.preventDefault(); open(e.key === 'ArrowUp'); }
    });
    new MutationObserver(function () { if (button.disabled && openList && openList.owner === box) closeOpen(); }).observe(button, { attributes: true, attributeFilter: ['disabled'] });
    box.setSelected = function (selected) {
      chosen = new Set(Array.from(selected).filter(function (item) { return !items.length || items.includes(item); })); show();
      if (multi && openList && openList.owner === box) sync(openList.menu);
    };
    box.set = function (list, cur) {
      if (openList && openList.owner === box) closeOpen();
      items = Array.from(new Set(list));
      if (multi) box.setSelected(cur === undefined ? chosen : cur); else { current = Math.max(0, items.indexOf(cur)); show(); }
    };
    box.value = function () { return multi ? values() : items[current]; };
    show();
  }
  $('.s-select').forEach(select);

  // SearchChoice: loaded objects, stable IDs and independent committed selection.
  var searchChoiceSequence = 0;
  $('.s-search-choice').forEach(function (box) {
    var items = JSON.parse(box.dataset.items || '[]'), selected = null, popup = null, input = null, viewport = null, matches = [], active = -1;
    var prefix = 'search-choice-' + (++searchChoiceSequence) + '-';
    var trigger = document.createElement('button'); trigger.type = 'button'; trigger.setAttribute('aria-haspopup', 'dialog'); trigger.setAttribute('aria-expanded', 'false');
    trigger.innerHTML = '<span></span>' + icon('chevron-down'); box.appendChild(trigger);
    function normalize(text) { return String(text || '').normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase(); }
    var indexed;
    function index() { indexed = items.map(function (item) { return { item: item, search: normalize(item.search == null ? item.label : item.search) }; }); }
    function caption() { trigger.firstChild.textContent = selected ? selected.label : box.dataset.placeholder || 'Choose an option'; trigger.classList.toggle('placeholder', !selected); }
    function close(focus) { if (openList && openList.owner === box) closeOpen(); if (focus) trigger.focus(); }
    function commit(item) {
      if (trigger.disabled || !popup || !matches.some(function (entry) { return entry.item === item; })) return;
      selected = item; caption(); close(true); box.dispatchEvent(new CustomEvent('change', { detail: { value: item, id: item.id } }));
    }
    function render() {
      if (!viewport) return;
      var offset = viewport.scrollTop;
      viewport.replaceChildren(); viewport.style.height = Math.min(280, Math.max(28, matches.length * 28)) + 'px';
      if (!matches.length) { var empty = document.createElement('div'); empty.className = 's-search-empty'; empty.setAttribute('role', 'status'); empty.textContent = box.dataset.noResults || 'No matches'; viewport.appendChild(empty); input.removeAttribute('aria-activedescendant'); return; }
      var space = document.createElement('div'); space.style.height = matches.length * 28 + 'px'; space.className = 's-search-space'; viewport.appendChild(space); viewport.scrollTop = offset;
      var start = Math.max(0, Math.floor(viewport.scrollTop / 28) - 2), end = Math.min(matches.length, start + Math.ceil(viewport.clientHeight / 28) + 4);
      for (var i = start; i < end; i++) (function (at) {
        var entry = matches[at];
        var row = document.createElement('button'); row.type = 'button'; row.className = 's-search-option'; row.id = prefix + at; row.tabIndex = -1;
        row.style.top = at * 28 + 'px'; row.innerHTML = marked(matches[at].item.label, input.value); row.setAttribute('role', 'option'); row.setAttribute('aria-selected', String(at === active));
        row.addEventListener('pointerdown', function (event) { if (event.button === 0) event.preventDefault(); });
        row.addEventListener('click', function () { commit(entry.item); }); space.appendChild(row);
      })(i);
      if (active >= start && active < end) input.setAttribute('aria-activedescendant', prefix + active); else input.removeAttribute('aria-activedescendant');
    }
    function filter() {
      var words = normalize(input.value).trim().split(/\s+/).filter(Boolean);
      matches = words.length ? indexed.filter(function (entry) { return words.every(function (word) { return entry.search.includes(word); }); }) : indexed;
      active = words.length ? (matches.length ? 0 : -1) : matches.findIndex(function (entry) { return entry.item === selected; });
      render(); viewport.scrollTop = Math.max(0, active) * 28; render();
    }
    function open() {
      if (trigger.disabled) return;
      if (popup) { close(true); return; }
      closeOpen(); index(); popup = document.createElement('div'); popup.className = 's-search-popup'; popup.setAttribute('role', 'dialog'); popup.setAttribute('aria-label', box.dataset.placeholder || 'Choose an option');
      var header = document.createElement('div'); header.className = 's-search-header'; var field = document.createElement('span'); field.className = 's-field s-search'; field.innerHTML = icon('search');
      input = document.createElement('input'); input.type = 'text'; input.placeholder = box.dataset.searchPlaceholder || 'Search'; input.setAttribute('aria-label', input.placeholder); input.setAttribute('aria-controls', prefix + 'options'); field.appendChild(input);
      header.appendChild(field); viewport = document.createElement('div'); viewport.className = 's-search-results'; viewport.id = prefix + 'options'; viewport.setAttribute('role', 'listbox'); viewport.setAttribute('aria-label', 'Options');
      popup.append(header, viewport); box.appendChild(popup); box.classList.add('open'); trigger.setAttribute('aria-expanded', 'true');
      openList = { owner: box, menu: popup, close: function () { popup.remove(); popup = input = viewport = null; box.classList.remove('open'); trigger.setAttribute('aria-expanded', 'false'); } };
      input.addEventListener('input', filter); input.addEventListener('change', function (event) { event.stopPropagation(); }); viewport.addEventListener('scroll', render);
      popup.addEventListener('keydown', function (event) {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
          event.preventDefault(); active = !matches.length ? -1 : active < 0 ? (event.key === 'ArrowDown' ? 0 : matches.length - 1) : Math.max(0, Math.min(matches.length - 1, active + (event.key === 'ArrowDown' ? 1 : -1)));
          if (active >= 0) { var top = active * 28; if (top < viewport.scrollTop) viewport.scrollTop = top; else if (top + 28 > viewport.scrollTop + viewport.clientHeight) viewport.scrollTop = top + 28 - viewport.clientHeight; } render();
        } else if (event.key === 'Enter') { event.preventDefault(); if (!event.repeat && active >= 0) commit(matches[active].item); }
        else if (event.key === 'Escape') { event.preventDefault(); close(true); }
        else if (event.key === 'Tab') close(true);
        event.stopPropagation();
      });
      filter(); input.focus();
    }
    trigger.addEventListener('click', open);
    trigger.addEventListener('keydown', function (event) { if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'F4') { event.preventDefault(); open(); } });
    new MutationObserver(function () { if (trigger.disabled) close(false); }).observe(trigger, { attributes: true, attributeFilter: ['disabled'] });
    box.value = function () { return selected; };
    box.setValue = function (id) { selected = items.find(function (item) { return item.id === id; }) || null; caption(); if (popup) filter(); };
    box.setItems = function (value) { items = Array.from(value); if (!items.includes(selected)) selected = null; caption(); index(); if (popup) filter(); };
    caption(); index();
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
  function textTabs(tabs) {
    var line = document.createElement('i'); line.className = 'line'; tabs.appendChild(line);
    var buttons = $('button', tabs);
    function place() {
      var active = tabs.querySelector('[aria-selected="true"]'); if (!active) return;
      var strip = tabs.getBoundingClientRect(), rect = active.getBoundingClientRect();
      line.style.left = (rect.left - strip.left - tabs.clientLeft) + 'px'; line.style.width = rect.width + 'px';
    }
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
  }
  $('.s-tabs').forEach(textTabs);

  // PairTable: preserve draft rows; export only when duplicate validation succeeds.
  $('.s-pair-table').forEach(function (table) {
    var body = table.querySelector('[data-pair-rows]');
    function validate() {
      var rows = $('.s-pair-row', body), counts = new Map();
      function key(input) { var value = input.value.trim(); return table.dataset.caseSensitive === 'true' ? value : value.toLowerCase(); }
      rows.forEach(function (row) { var name = key(row.querySelector('[data-name]')); if (name) counts.set(name, (counts.get(name) || 0) + 1); });
      var invalid = false;
      rows.forEach(function (row) {
        var input = row.querySelector('[data-name]'), duplicate = (counts.get(key(input)) || 0) > 1;
        input.classList.toggle('invalid', duplicate); input.setAttribute('aria-invalid', String(duplicate));
        var error = row.querySelector('[data-error]'); error.hidden = !duplicate; error.textContent = duplicate ? 'Duplicate name: ' + input.value.trim() : ''; invalid = invalid || duplicate;
      });
      table.dataset.invalid = String(invalid); return !invalid;
    }
    table.draft = function () { return $('.s-pair-row', body).map(function (row) { return { name: row.querySelector('[data-name]').value, value: row.querySelector('[data-value]').value }; }); };
    table.pairs = function () { return validate() ? table.draft().filter(function (row) { return row.name.trim(); }).map(function (row) { return { name: row.name.trim(), value: row.value }; }) : null; };
    body.addEventListener('input', function () { validate(); table.dispatchEvent(new Event('change')); });
    body.addEventListener('click', function (event) { var remove = event.target.closest('[data-remove-pair]'); if (!remove) return; remove.closest('.s-pair-row').remove(); validate(); table.dispatchEvent(new Event('change')); });
    table.querySelector('[data-add-pair]').addEventListener('click', function () {
      var row = table.querySelector('template').content.firstElementChild.cloneNode(true); body.appendChild(row); validate(); row.querySelector('input').focus(); table.dispatchEvent(new Event('change'));
    });
    validate();
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
      var q = input.value.trim().toLowerCase(); clear.hidden = !q; clear.disabled = input.readOnly || input.disabled;
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
    new MutationObserver(show).observe(input, { attributes: true, attributeFilter: ['readonly', 'disabled'] });
    input.addEventListener('input', show);
    input.addEventListener('keydown', function (e) { if (e.key === 'Escape' && input.value && !input.readOnly && !input.disabled) { e.stopPropagation(); input.value = ''; show(); } });
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
      menu.classList.remove('floating'); icon.parentElement.appendChild(menu); menu.reveal(); icon.classList.add('open');
      menu.addEventListener('keydown', function (event) { if (event.key === 'Escape' || event.key === 'Tab') { closeOpen(); icon.focus({ preventScroll: true }); if (event.key === 'Escape') event.preventDefault(); } else menu.key(event); });
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
      hexOut.textContent = cur.toUpperCase(); what.textContent = accent ? accent.name || accent.label || 'Custom' : 'Violet';
      $('.cur', pick).forEach(function (button) { button.classList.toggle('current', button.dataset.name === (accent ? accent.name : 'Violet')); });
      $('.palette button', pick).forEach(function (button) { button.classList.toggle('current', !!accent && !accent.name && button.dataset.c.toUpperCase() === cur.toUpperCase()); });
      ratio.textContent = c.toFixed(1) + ' : 1 ' + (c >= 4.5 ? 'readable' : c >= 3 ? 'borderline' : dark() ? 'too dark' : 'too light');
      ratio.classList.toggle('low', c < 4.5);
    }
    function choose(el, pair, label) {
      pair.label = label; accent = pair; applyAccent(); show(label);
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
    mix(); pick.show = show; show();
    document.addEventListener('shade-accent', function () { show(); });
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
  // are the table designer: Ctrl+drag one to a gap, right-click to insert or remove, Ctrl+arrows to move.
  var OPS = { '+': function (a, b) { return a + b; }, '−': function (a, b) { return a - b; }, '×': function (a, b) { return a * b; }, '÷': function (a, b) { return b ? a / b : NaN; } };
  var FNS = { none: 'None', sum: 'Sum', average: 'Average', min: 'Min', max: 'Max', count: 'Count' };
  var ink = document.createElement('canvas').getContext('2d');
  function textWidth(t, px, weight, italic) { ink.font = (italic ? 'italic ' : '') + (weight || 400) + ' ' + px + 'px Inter, sans-serif'; return ink.measureText(t || '').width; }
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
  $('.s-grid').forEach(function (box, gridIndex) {
    if (!box.id) box.id = 'shade-grid-' + gridIndex;
    var cfg = JSON.parse(box.querySelector('script').textContent), seq = 0;
    // sizing: fit (its content, never cut), fill (the room the others leave) or fixed (width); min and max hold any of them
    var cols = cfg.columns.map(function (c) { return Object.assign({ type: 'text', footer: 'none', width: null, min: null, max: null, sizing: c.grow ? 'fill' : c.width ? 'fixed' : 'fit' }, c); });
    var rows = cfg.rows.map(function (r) { return { id: ++seq, v: Object.assign({}, r) }; });
    var opt = { head: 12, sub: 11, cell: 13, sorting: true, design: false, paper: 'Screen', orientation: 'Portrait', paperWidth: 210, paperHeight: 297 }, sort = { key: null, dir: 0 };
    var selected = {}, anchor = null, current = cols[0].key, line = document.createElement('div');
    function field(label, key, items, scope, extra) {
      var attr = ' data-grid="' + box.id + '" data-' + (scope || 'col') + '="' + key + '"';
      return '<label class="s-label grid-field-' + key + '">' + label + (items ? '<span class="s-select"' + attr + ' data-items="' + items + '"></span>'
        : '<input class="s-field"' + attr + (extra || '') + '>') + '</label>';
    }
    box.innerHTML = '<div class="grid-toolbar" role="toolbar" aria-label="Table columns"><button type="button" class="s-btn ghost edit-columns" data-opt="design" aria-pressed="false" aria-expanded="false" aria-controls="' + box.id + '-ribbon">Edit columns</button>' +
      '<button type="button" class="s-btn ghost square move-left" aria-label="Move column left" data-tip="Move column left" disabled>' + icon('chevron-left') + '</button>' +
      '<button type="button" class="s-btn ghost square move-right" aria-label="Move column right" data-tip="Move column right" disabled>' + icon('chevron-right') + '</button>' +
      '<button type="button" class="s-btn ghost remove-column" disabled>Remove column</button><span class="grid-toolbar-spacer"></span><button type="button" class="s-btn ghost column" disabled>' + icon('plus') + 'Add column</button>' +
      '<button type="button" class="s-btn ghost computed-column" disabled>ƒ Calculated column</button></div>' +
      '<div class="grid-ribbon" id="' + box.id + '-ribbon" hidden><div class="s-tabs grid-ribbon-tabs" role="tablist" aria-label="Column settings">' +
      ['Column', 'Text', 'Width', 'Table'].map(function (name, i) {
        return '<button type="button" role="tab" id="' + box.id + '-tab-' + i + '" aria-controls="' + box.id + '-panel-' + i + '" aria-selected="' + (i === 0) + '" tabindex="' + (i ? -1 : 0) + '">' + name + '</button>';
      }).join('') + '</div>' +
      '<div class="grid-ribbon-panel" role="tabpanel" id="' + box.id + '-panel-0" aria-labelledby="' + box.id + '-tab-0">' +
        field('Heading', 'title') + field('Type', 'type', 'Text|Number|Value with unit|Computed|General') + field('Footer', 'footer', 'None|Sum|Average|Min|Max|Count') + field('Alignment', 'alignment', 'Auto|Left|Center|Right') +
        '<div class="s-label grid-formula" data-when="computed"><div class="grid-formula-head"><span>Formula</span><button type="button" class="s-btn ghost add-term">' + icon('plus') + 'Add term</button></div><div class="terms" data-grid="' + box.id + '" data-col="terms"></div></div><div class="grid-units" data-when="unit">' + field('Units (comma-separated)', 'units') + '<span class="grid-units-hint">First unit is the default. Existing row units are kept.</span><span class="grid-units-error" role="status" hidden></span></div></div>' +
      '<div class="grid-ribbon-panel" role="tabpanel" id="' + box.id + '-panel-1" aria-labelledby="' + box.id + '-tab-1" hidden>' +
        field('Subheading', 'subtitle') + field('Heading size', 'head', '12|13|14') + field('Subheading size', 'sub', '11|12') + field('Values', 'style', 'Normal|Bold|Italic|Bold italic') + '</div>' +
      '<div class="grid-ribbon-panel" role="tabpanel" id="' + box.id + '-panel-2" aria-labelledby="' + box.id + '-tab-2" hidden>' +
        field('Width', 'sizing', 'Fit|Fill|Fixed') + '<div data-when="fixed">' + field('Fixed width', 'width', null, null, ' inputmode="numeric" placeholder="auto"') + '</div>' +
        field('Min width', 'min', null, null, ' inputmode="numeric" placeholder="auto"') + field('Max width', 'max', null, null, ' inputmode="numeric" placeholder="none"') + '<span class="grid-width-error" role="status" hidden></span></div>' +
      '<div class="grid-ribbon-panel" role="tabpanel" id="' + box.id + '-panel-3" aria-labelledby="' + box.id + '-tab-3" hidden>' +
        field('Heading size', 'head', '12|13|14', 'opt') + field('Subheading size', 'sub', '11|12', 'opt') + field('Value size', 'cell', '12|13|14', 'opt') + field('Sorting', 'sorting', 'On|Off', 'opt') +
        '<div class="grid-paper-controls">' + field('Paper format', 'paper', 'Screen|A5|A4|A3|Letter|Legal|Custom', 'opt') +
        '<div data-paper-settings>' + field('Orientation', 'orientation', 'Portrait|Landscape', 'opt') + '</div>' +
        '<div data-custom-paper>' + field('Width (mm)', 'paperWidth', null, 'opt', ' type="number" min="1" max="2000" step="0.1"') + '</div>' +
        '<div data-custom-paper>' + field('Height (mm)', 'paperHeight', null, 'opt', ' type="number" min="1" max="2000" step="0.1"') + '</div>' +
        '<span class="grid-paper-note"></span></div></div></div>' +
      '<div class="scroll"><table></table></div><div class="add"><button type="button" class="s-btn ghost rows">' + icon('plus') + 'Add</button>' +
      '<input class="s-field count" value="1" inputmode="numeric" aria-label="Rows to add"><span>rows</span></div>';
    $('.s-select', box).forEach(function (el) { el.setAttribute('data-floating', ''); select(el); el.querySelector('button').setAttribute('aria-label', el.parentElement.firstChild.textContent); });
    line.className = 'line'; line.hidden = true; box.appendChild(line);
    var scroll = box.querySelector('.scroll'), table = scroll.firstChild, countField = box.querySelector('.count');
    var ribbon = box.querySelector('.grid-ribbon'), edit = box.querySelector('.edit-columns'), tabs = $('[role="tab"]', ribbon);
    function showTab(index) {
      $('[role="tabpanel"]', ribbon).forEach(function (panel, i) { panel.hidden = i !== index; });
    }
    textTabs(box.querySelector('.grid-ribbon-tabs'));
    tabs.forEach(function (tab, i) { tab.addEventListener('click', function () { closeOpen(); showTab(i); }); });
    function setDesign(on) {
      var invalid = table.querySelector('.v.invalid'); if (invalid) { invalid.focus(); return; }
      closeOpen(); opt.design = on;
      if (on) sort = { key: null, dir: 0 };
      render();
      if (!on) edit.focus();
    }
    edit.addEventListener('click', function () { setDesign(!opt.design); });
    box.querySelector('.move-left').addEventListener('click', function () { if (moveCol(cols.indexOf(col(current)), cols.indexOf(col(current)) - 1)) render(); });
    box.querySelector('.move-right').addEventListener('click', function () { if (moveCol(cols.indexOf(col(current)), cols.indexOf(col(current)) + 1)) render(); });
    box.querySelector('.remove-column').addEventListener('click', function () { if (col(current)) removeCol(current); });
    var paperSizes = { A5: [148, 210], A4: [210, 297], A3: [297, 420], Letter: [215.9, 279.4], Legal: [215.9, 355.6] };
    var paperStyle = document.createElement('style'); paperStyle.media = 'print'; box.appendChild(paperStyle);
    function paperLayout() {
      var size = opt.paper === 'Custom' ? [opt.paperWidth, opt.paperHeight] : paperSizes[opt.paper];
      if (size && opt.orientation === 'Landscape') size = [size[1], size[0]];
      $('[data-paper-settings]', ribbon).forEach(function (el) { el.hidden = !size; });
      $('[data-custom-paper]', ribbon).forEach(function (el) { el.hidden = opt.paper !== 'Custom'; });
      scroll.style.width = size ? size[0] + 'mm' : '';
      scroll.style.maxWidth = '100%';
      box.querySelector('.grid-paper-note').hidden = !size;
      box.querySelector('.grid-paper-note').textContent = size ? size[0] + ' × ' + size[1] + ' mm · preview and print size' : 'Use the available table width';
      box.style.page = size ? 'shade-table-' + gridIndex : '';
      paperStyle.textContent = size ? '@page shade-table-' + gridIndex + ' { size: ' + size[0] + 'mm ' + size[1] + 'mm; margin: 0; }' : '';
    }
    function col(k) { return cols.find(function (c) { return c.key === k; }); }
    function row(id) { return rows.find(function (r) { return r.id === +id; }); }
    function numeric(c) { return c.type !== 'text'; }
    function num(t) { t = String(t == null ? '' : t).trim().replace(/\s/g, '').replace(',', '.'); return /^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:e[+-]?\d+)?$/i.test(t) && Number.isFinite(Number(t)) ? Number(t) : NaN; }
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
    function alignment(r, c) { var value = r.v.alignments && r.v.alignments[c.key] || c.alignment; return ['left', 'center', 'right'].indexOf(value) >= 0 ? value : right(c, r.v[c.key]) ? 'right' : 'left'; }
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
      var w = Math.max(textWidth(c.title, c.head || opt.head, 600), textWidth(c.subtitle, c.sub || opt.sub)) + (sort.key === c.key ? 22 : 0);
      var extra = c.type === 'unit' ? Math.max.apply(null, c.units.concat(rows.map(function (r) { return unitOf(r, c); })).map(function (u) { return textWidth(u, opt.cell); })) + 14 : c.suffix ? textWidth(c.suffix, 11) + 6 : 0;
      rows.forEach(function (r) { w = Math.max(w, textWidth(shown(r, c), opt.cell, c.bold ? 600 : 400, c.italic) + extra); });
      w = Math.max(w, textWidth(footer(c), opt.cell, 600) + (c.footer !== 'none' ? textWidth(FNS[c.footer], 11, 500) + 6 : 0));
      return Math.ceil(Math.max(40, w + 18));
    }
    // a fill column gives way down to its longest word (its text wraps)
    function least(c) {
      var w = 0;
      String(c.title).split(/\s+/).forEach(function (word) { w = Math.max(w, textWidth(word, c.head || opt.head, 600)); });
      rows.forEach(function (r) { String(shown(r, c)).split(/\s+/).forEach(function (word) { w = Math.max(w, textWidth(word, opt.cell, c.bold ? 600 : 400, c.italic)); }); });
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
      cols.filter(function (c) { return c.type === 'unit'; }).forEach(function (c) {
        var after = Math.ceil(Math.max.apply(null, c.units.concat(rows.map(function (r) { return unitOf(r, c); })).map(function (u) { return textWidth(u, 12, 500); }))) + 16;
        $('tbody td[data-k="' + c.key + '"]', table).forEach(function (td) { td.style.setProperty('--after', after + 'px'); });
      });
    }
    function render(focus) {
      var list = order(), h = '<colgroup><col style="width:36px">' + cols.map(function () { return '<col class="c">'; }).join('') + '<col style="width:32px"></colgroup><thead><tr>';
      var all = list.length && list.every(function (r) { return selected[r.id]; });
      h += '<th class="gut"><button type="button" class="pick" aria-label="' + (all ? 'Select none' : 'Select all rows') + '" data-tip="' + (all ? 'Select none' : 'Select all') + '">#</button></th>';
      cols.forEach(function (c) {
        var dir = sort.key === c.key ? sort.dir : 0;
        h += '<th data-k="' + c.key + '" class="' + (numeric(c) && c.type !== 'general' ? 'num' : '') + (c.key === current ? ' current' : '') + '"' + (dir ? ' aria-sort="' + (dir > 0 ? 'ascending' : 'descending') + '"' : '') +
          (opt.design ? ' tabindex="0"' : '') + (c.type === 'computed' ? ' data-tip="= ' + esc(formula(c)) + '"' : '') + '>' +
          '<div class="lbl"><span class="h"' + (c.head ? ' style="font-size:' + Number(c.head) + 'px"' : '') + '>' + esc(c.title) + '</span>' + (c.subtitle ? '<span class="subt"' + (c.sub ? ' style="font-size:' + Number(c.sub) + 'px"' : '') + '>' + esc(c.subtitle) + '</span>' : '') + '</div>' +
          (dir ? icon(dir > 0 ? 'chevron-up' : 'chevron-down', 'sort') : '') + '<span class="rz"></span></th>';
      });
      h += '<th class="act"></th></tr></thead><tbody>';
      list.forEach(function (r, i) {
        h += '<tr data-id="' + r.id + '"' + (selected[r.id] ? ' class="selected"' : '') + '><td class="gut"><button type="button" class="pick" tabindex="-1" aria-label="Select row ' + (i + 1) + '">' + (i + 1) + '</button></td>';
        cols.forEach(function (c) {
          var cls = (right(c, r.v[c.key]) ? 'num' : '') + (c.type === 'computed' ? ' ro' : '') + (c.bold ? ' bold' : '') + (c.italic ? ' italic' : '');
          h += '<td data-k="' + c.key + '" class="' + cls + '">';
          h += c.type === 'computed' ? '<div class="v" style="text-align:' + alignment(r, c) + '" tabindex="0">' + esc(shown(r, c)) + '</div>'  // read only, still focusable: its column's controls follow
            : '<div class="v" style="text-align:' + alignment(r, c) + '" contenteditable="plaintext-only" spellcheck="false" role="textbox" aria-label="' + esc(c.title) + '">' + esc(shown(r, c)) + '</div>';
          if (c.type === 'unit') h += '<button type="button" class="unit" data-tip="Click to change the unit" aria-label="Unit: ' + esc(unitOf(r, c)) + '">' + esc(unitOf(r, c)) + '</button>';
          if (c.suffix) h += '<span class="suf">' + esc(c.suffix) + '</span>';
          h += '</td>';
        });
        h += '<td class="act"><button type="button" class="more" data-tip="Row actions" aria-label="Row actions">' + icon('ellipsis') + '</button></td></tr>';
      });
      h += '</tbody><tfoot><tr><td class="gut"></td>' + cols.map(function (c) {
        return '<td data-k="' + c.key + '" class="' + (numeric(c) ? 'num' : '') + '"><button type="button" class="fn' + (c.footer === 'none' ? ' none' : '') + '" aria-label="Calculate · Choose what the footer shows" data-tip="Choose what the footer shows">' +
          (c.footer === 'none' ? '<span aria-hidden="true">ƒ</span>' : '<span class="fl">' + FNS[c.footer] + '</span><b>' + esc(footer(c)) + '</b>') + '</button></td>';
      }).join('') + '<td class="act"></td></tr></tfoot>';
      table.innerHTML = h;
      box.classList.toggle('sorting', opt.sorting && !opt.design); box.classList.toggle('design', opt.design);
      box.style.setProperty('--head', opt.head + 'px'); box.style.setProperty('--sub', opt.sub + 'px'); box.style.setProperty('--cell', opt.cell + 'px');
      ribbon.hidden = !opt.design;
      edit.classList.toggle('ghost', !opt.design);
      edit.textContent = opt.design ? 'Done' : 'Edit columns'; edit.setAttribute('aria-pressed', String(opt.design)); edit.setAttribute('aria-expanded', String(opt.design));
      $('.grid-toolbar .column, .computed-column', box).forEach(function (button) { button.disabled = !opt.design; button.title = opt.design ? '' : 'Choose Edit columns first'; });
      $('[data-opt]', ribbon).forEach(function (el) {
        if (el.matches('input')) { if (el !== document.activeElement) el.value = opt[el.dataset.opt]; }
        else el.set(el.dataset.items.split('|'), el.dataset.opt === 'sorting' ? (opt.sorting ? 'On' : 'Off') : String(opt[el.dataset.opt]));
      });
      paperLayout();
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
    function insertCol(i, computed) {
      var sources = cols.filter(numeric);
      var c = { key: 'c' + (++seq), title: computed ? 'Calculated column' : 'New column', type: computed ? 'computed' : 'text', footer: computed ? 'sum' : 'none', sizing: 'fit', width: null };
      if (computed) { c.decimals = 2; c.terms = sources.slice(0, 2).map(function (x, j) { return j ? { op: '+', k: x.key } : { k: x.key }; }); }
      tabs[0].click();
      cols.splice(i, 0, c); current = c.key; render({ head: c.key });
      var f = document.querySelector('[data-grid="' + box.id + '"][data-col="title"]'); if (f) { f.focus(); f.select(); }
    }
    function removeCol(k) {
      var c = col(k); if (cols.length < 2) return;
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
    var validation = document.createElement('p'); validation.className = 's-grid-error'; validation.setAttribute('role', 'alert'); validation.hidden = true; box.insertBefore(validation, box.querySelector('.add'));
    function validateCell(v) {
      var c = col(v.parentElement.dataset.k), text = v.textContent;
      var invalid = (c.type === 'unit' || c.type === 'number') && text.trim() !== '' && !Number.isFinite(num(text));
      v.classList.toggle('invalid', invalid); v.setAttribute('aria-invalid', String(invalid));
      if (invalid) { v.setAttribute('aria-description', 'Enter a number. Press Escape to cancel.'); v.title = 'Enter a number'; }
      else { v.removeAttribute('aria-description'); v.removeAttribute('title'); }
      var errors = $('.v.invalid', table);
      validation.hidden = !errors.length; validation.textContent = errors.length ? 'Enter a number. Press Escape in the cell to cancel.' : '';
      return !invalid;
    }
    table.addEventListener('input', function (e) {
      var v = e.target.closest('.v'), tr = v && v.closest('tbody tr'); if (!tr || !validateCell(v)) return;
      var td = v.parentElement, c = col(td.dataset.k);
      row(tr.dataset.id).v[c.key] = v.textContent; td.classList.toggle('num', right(c, v.textContent)); v.style.textAlign = alignment(row(tr.dataset.id), c); refresh(tr);
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
      var v = e.target.closest('.v'); if (!v) return;
      if (e.key === 'Escape' && v.classList.contains('invalid')) { e.preventDefault(); v.textContent = shown(row(v.closest('tr').dataset.id), col(v.parentElement.dataset.k)); validateCell(v); selectAll(v); return; }
      if ((e.key === 'Enter' || e.key === 'Tab') && !validateCell(v)) { e.preventDefault(); return; }
      if (e.key !== 'Enter') return;
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
      else if (th && opt.design && !e.target.closest('.rz')) { current = th.dataset.k; $('th[data-k]', table).forEach(function (h) { h.classList.toggle('current', h === th); }); box.dispatchEvent(new CustomEvent('column', { detail: col(current) })); }
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
        var mv = function (ev) { var v = Math.max(40, Math.round(w + ev.clientX - start)); if (c.sizing === 'fill') c.min = v; else { c.width = Math.max(c.min || 40, Math.min(v, c.max || Infinity)); c.sizing = 'fixed'; } layout(); };
        var up = function () { rz.removeEventListener('pointermove', mv); rz.removeEventListener('pointerup', up); box.classList.remove('resizing'); rz.classList.remove('on'); box.dispatchEvent(new CustomEvent('column', { detail: col(current) })); };
        rz.addEventListener('pointermove', mv); rz.addEventListener('pointerup', up);
        return;
      }
      if (!th || !opt.design || !e.ctrlKey) return;
      e.preventDefault();
      // Ctrl+drag moves a column; a plain header click only selects it.
      var k = th.dataset.k, from = cols.indexOf(col(k)), sx = e.clientX, sy = e.clientY, moving = false, ghost = null;
      th.setPointerCapture(e.pointerId);
      var drag = function (ev) {
        if (!ev.ctrlKey) { finish(ev, false); return; }
        if (!moving && Math.abs(ev.clientX - sx) + Math.abs(ev.clientY - sy) < 4) return;
        if (!moving) {
          moving = true; ghost = document.createElement('div'); ghost.className = 's-ghost'; ghost.innerHTML = icon('grip-vertical') + '<span></span>'; ghost.lastChild.textContent = col(k).title; document.body.appendChild(ghost);
          $('[data-k="' + k + '"]', table).forEach(function (el) { el.classList.add('dim'); });
        }
        ghost.style.left = (ev.clientX - 12) + 'px'; ghost.style.top = (ev.clientY - 14) + 'px'; showGap(gapAt(ev.clientX, ev.clientY), from);
      };
      var finish = function (ev, commit) {
        th.removeEventListener('pointermove', drag); th.removeEventListener('pointerup', drop);
        th.removeEventListener('pointercancel', cancel); th.removeEventListener('lostpointercapture', cancel);
        document.removeEventListener('keyup', keys); document.removeEventListener('keydown', keys);
        if (th.hasPointerCapture(e.pointerId)) th.releasePointerCapture(e.pointerId);
        if (ghost) ghost.remove(); line.hidden = true;
        $('.dim', table).forEach(function (el) { el.classList.remove('dim'); });
        if (commit && moving && ev.ctrlKey) { var g = gapAt(ev.clientX, ev.clientY); moveCol(from, g > from ? g - 1 : g); render({ head: k }); }
      };
      var drop = function (ev) { finish(ev, true); }, cancel = function (ev) { finish(ev, false); };
      var keys = function (ev) { if (ev.key === 'Escape' || ev.key === 'Control' && ev.type === 'keyup') finish(ev, false); };
      th.addEventListener('pointermove', drag); th.addEventListener('pointerup', drop);
      th.addEventListener('pointercancel', cancel); th.addEventListener('lostpointercapture', cancel);
      document.addEventListener('keyup', keys); document.addEventListener('keydown', keys);
    });
    function reorderCursor(event) { box.classList.toggle('reorder-ready', opt.design && event.ctrlKey); }
    table.addEventListener('pointermove', reorderCursor);
    document.addEventListener('keydown', reorderCursor); document.addEventListener('keyup', reorderCursor);
    box.addEventListener('keydown', function (e) { if (e.key === 'Escape' && opt.design && !e.defaultPrevented && !e.ctrlKey && !openList) { e.preventDefault(); setDesign(false); return; } if (e.key === 'Escape' && Object.keys(selected).length && !openList) { selected = {}; render(); } });
    box.querySelector('.add .rows').addEventListener('click', function () {
      var n = Math.max(1, Math.min(100, parseInt(countField.value, 10) || 1)), first = null;
      for (var i = 0; i < n; i++) { var b = blank(); rows.push(b); if (!first) first = b; }
      countField.value = n; if (sort.dir) sort = { key: null, dir: 0 };  // new rows show at the end, where they were added
      render({ id: first.id, key: cols[0].key });
    });
    box.querySelector('.grid-toolbar .column').addEventListener('click', function () { insertCol(cols.length); });
    box.querySelector('.computed-column').addEventListener('click', function () { insertCol(cols.length, true); });
    countField.addEventListener('keydown', function (e) { if (e.key === 'Enter') box.querySelector('.add .rows').click(); });
    // the controls for this table elsewhere on the page: data-grid="its id", data-opt (head, sub, cell, sorting, design) or
    // data-col (title, subtitle, type, footer, sizing, min, max; terms is the formula's editor)
    function bind(el, apply, read) { el.addEventListener(el.matches('input') && el.dataset.col !== 'units' ? 'input' : 'change', function () { apply(read(el)); }); }
    $('[data-grid="' + box.id + '"]').forEach(function (el) {
      var o = el.dataset.opt, k = el.dataset.col;
      if (k === 'terms' || el === edit) return;
      var read = el.classList.contains('s-switch') ? function () { return el.getAttribute('aria-checked') === 'true'; } : el.matches('input') ? function () { return el.value; } : function () { return el.value(); };
      if (o) bind(el, function (v) {
        if (o === 'paperWidth' || o === 'paperHeight') {
          var dimension = Number(v); el.setCustomValidity(dimension >= 1 && dimension <= 2000 ? '' : 'Enter a dimension from 1 to 2000 mm.');
          if (!el.checkValidity()) return;
          opt[o] = dimension;
        } else opt[o] = o === 'sorting' ? v === 'On' : o === 'paper' || o === 'orientation' ? v : parseInt(v, 10);
        if (o === 'sorting' && !opt.sorting) sort = { key: null, dir: 0 };  // back to the original order
        render();
      }, read);
      if (k) bind(el, function (v) {
        var c = col(current); if (!c) return;
        if (k === 'units') {
          var units = Array.from(new Set(v.split(',').map(function (u) { return u.trim(); }).filter(Boolean)));
          var message = units.length ? '' : 'Define at least one unit.';
          el.setCustomValidity(message); el.setAttribute('aria-invalid', String(!units.length));
          var error = box.querySelector('.grid-units-error'); error.hidden = !!units.length; error.textContent = message;
          if (!units.length) return;
          c.units = units; el.value = units.join(', ');
        }
        else if (k === 'title' || k === 'subtitle') c[k] = v;
        else if (k === 'sizing') { c.sizing = Object.keys(SIZES).find(function (z) { return SIZES[z] === v; }); if (c.sizing === 'fixed' && !c.width) c.width = Math.round($('col.c', table)[cols.indexOf(c)].getBoundingClientRect().width); }
        else if (k === 'alignment') c.alignment = v === 'Auto' ? null : v.toLowerCase();
        else if (k === 'head' || k === 'sub') c[k] = parseInt(v, 10);
        else if (k === 'style') { c.bold = v.indexOf('Bold') >= 0; c.italic = v.indexOf('italic') >= 0 || v === 'Italic'; }
        else if (k === 'width' || k === 'min' || k === 'max') {
          var n = parseInt(v, 10), low = Math.max(40, c.min || 0), high = Math.max(low, c.max || Infinity);
          if (k === 'width' && n > 0 && (n < low || n > high)) {
            el.setCustomValidity('Width must be at least ' + low + (isFinite(high) ? ' and at most ' + high : '') + ' points.');
            el.setAttribute('aria-invalid', 'true');
            var error = box.querySelector('.grid-width-error'); error.hidden = false; error.textContent = el.validationMessage; return;
          }
          c[k] = n > 0 ? n : null;
          if (c.max) c.max = Math.max(40, c.min || 0, c.max);
          if (c.width) c.width = Math.max(40, c.min || 0, Math.min(c.width, c.max || Infinity));
          var width = box.querySelector('[data-col="width"]'); width.setCustomValidity(''); width.setAttribute('aria-invalid', 'false'); box.querySelector('.grid-width-error').hidden = true;
          $('[data-col="width"], [data-col="min"], [data-col="max"]', ribbon).forEach(function (input) { if (input !== document.activeElement) input.value = c[input.dataset.col] || ''; });
          layout(); return;
        }
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
    $('[data-col="width"], [data-col="min"], [data-col="max"]', ribbon).forEach(function (input) {
      input.addEventListener('change', function () {
        var c = col(current), n = parseInt(input.value, 10);
        if (input.dataset.col === 'width' && n > 0) c.width = Math.max(40, c.min || 0, Math.min(n, c.max || Infinity));
        input.value = c[input.dataset.col] || ''; input.setCustomValidity(''); input.setAttribute('aria-invalid', 'false');
        box.querySelector('.grid-width-error').hidden = true; layout();
      });
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
          sel.setAttribute('data-floating', ''); select(sel);
          sel.addEventListener('change', function (e) { if (sel.classList.contains('op')) t.op = e.detail.value; else t.k = sources[e.detail.index].key; render(); });
        });
        var rm = line.querySelector('.rm'); if (rm) rm.addEventListener('click', function () { c.terms.splice(i, 1); if (c.terms[0]) delete c.terms[0].op; render(); });
      });
      var add = box.querySelector('.add-term');
      add.onclick = function () { var used = c.terms.map(function (t) { return t.k; }), next = sources.find(function (x) { return used.indexOf(x.key) < 0; }) || sources[0]; if (!next) return; c.terms.push({ op: '+', k: next.key }); render(); host.scrollTop = host.scrollHeight; };
      add.disabled = !sources.length;
    }
    box.addEventListener('column', function (e) {
      var c = e.detail; if (!c) return;
      $('[data-grid="' + box.id + '"][data-col]').forEach(function (el) {
        var k = el.dataset.col, wrap = el.closest('[data-when]');
        if (wrap) wrap.hidden = wrap.dataset.when === 'fixed' ? c.sizing !== 'fixed' : c.type !== wrap.dataset.when;
        if (k === 'units') { if (document.activeElement !== el) { el.value = (c.units || []).join(', '); el.setCustomValidity(''); el.removeAttribute('aria-invalid'); box.querySelector('.grid-units-error').hidden = true; } }
        else if (k === 'title' || k === 'subtitle' || k === 'width' || k === 'min' || k === 'max') { if (document.activeElement !== el) el.value = c[k] || ''; }
        else if (k === 'alignment') el.set(['Auto', 'Left', 'Center', 'Right'], c.alignment ? c.alignment[0].toUpperCase() + c.alignment.slice(1) : 'Auto');
        else if (k === 'head' || k === 'sub') el.set(el.dataset.items.split('|'), String(c[k] || opt[k]));
        else if (k === 'style') el.set(['Normal', 'Bold', 'Italic', 'Bold italic'], c.bold ? (c.italic ? 'Bold italic' : 'Bold') : c.italic ? 'Italic' : 'Normal');
        else if (k === 'sizing') el.set(Object.keys(SIZES).map(function (z) { return SIZES[z]; }), SIZES[c.sizing]);
        else if (k === 'footer') el.set(numeric(c) ? Object.keys(FNS).map(function (f) { return FNS[f]; }) : ['None', 'Count'], FNS[c.footer]);
        else if (k === 'type') el.set(Object.keys(TYPES).map(function (t) { return TYPES[t]; }), TYPES[c.type]);
        else if (k === 'terms' && c.type === 'computed') termsEditor(el, c);
      });
      box.querySelector('.move-left').disabled = !opt.design || cols.indexOf(c) === 0; box.querySelector('.move-right').disabled = !opt.design || cols.indexOf(c) === cols.length - 1; box.querySelector('.remove-column').disabled = !opt.design || cols.length < 2;
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
    function set(pair, typing) { accent = pair; applyAccent(); show(typing); document.dispatchEvent(new CustomEvent('shade-accent', { detail: typing ? row : null })); }
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
    document.addEventListener('shade-accent', function (event) { if (event.detail !== row) show(); });
    row.show = show; show();
  });
  // a new mode makes the accent's shades again, as SetMode does
  new MutationObserver(function () { applyAccent(); $('.s-picker, .s-accent').forEach(function (p) { p.show(); }); })
    .observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
})();
