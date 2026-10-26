// Map tools, drawn inside the map page (HTML over the map canvas, so they also work in the detached map window):
//  * a "go to coordinates" box (latitude, longitude; decimal or degrees-minutes-seconds) that flies there and drops a temporary pin
//  * "get coordinates" mode and right-click: latitude/longitude of any point, copy, add to "my points", measure from here
//  * "measure distance" mode like Google Maps: click points, live total, a label at every vertex, undo / finish / clear
//  * one chip per Explorer/Monitoring result set (zoom to it, remove it; "remove all" when there are several)
//  * a small card for a selected "my points" point (copy, delete)
// The host sends the texts ("toolsInit") and the result sets ("toolResults"); the page answers with "toolAddPoint",
// "toolDeletePoint", "toolZoomResult" and "toolClearResult" (with the id of one set, or without an id for all). Distances are
// great-circle (haversine, WGS84 mean radius).
//
// Right-to-left text: every card is right-to-left, and every number is wrapped in an isolated left-to-right span, so the order is
// always "number, then unit" (e.g. "300 متر") whatever the surrounding words are.
(function () {
  'use strict';

  const host = window.chrome && window.chrome.webview;
  const post = (m) => host && host.postMessage(m);
  const FONT = ['NotoSans'];
  const SVG_NS = 'http://www.w3.org/2000/svg';

  // English fallbacks; the host replaces them with the Persian texts.
  const T = {
    placeholder: 'Latitude, longitude', example: 'Example: 35.7219, 51.3347', go: 'Go to coordinates', pick: 'Get coordinates from the map',
    measure: 'Measure distance', clear: 'Clear', copy: 'Copy', copied: 'Copied', copyBoth: 'Copy coordinates',
    lat: 'Latitude', lng: 'Longitude', pointTitle: 'Point coordinates', addPoint: 'Add to my points',
    namePlaceholder: 'Name (optional)', measureFrom: 'Measure from here', measureTitle: 'Measure distance',
    total: 'Total distance', points: 'points', lastSegment: 'Last segment',
    hintStart: 'Click the map to place the first point.',
    hintMore: 'Click to add points. Double-click, Enter or right-click to finish.',
    hintDone: 'Done. Press Clear to start again.', undo: 'Undo last point', finish: 'Finish', close: 'Close',
    invalid: 'Not a valid coordinate. Example: 35.7219, 51.3347', added: 'Point added to my points',
    zoom: 'Show on map', remove: 'Remove from map', removeAll: 'Remove all results', deletePoint: 'Delete this point',
    manualPoint: 'My point', km: 'km', m: 'm'
  };

  const PIN_SRC = 'tools|pin-src', LINE_SRC = 'tools|line-src', RUBBER_SRC = 'tools|rubber-src', PTS_SRC = 'tools|pts-src';
  const TOOL_LAYERS = ['tools|line-case', 'tools|line', 'tools|rubber', 'tools|pts', 'tools|lbl', 'tools|pin-halo', 'tools|pin-dot'];
  const PIN_COLOR = '#F43F5E';
  const EARTH_RADIUS = 6371008.8;

  let map = null, ready = false, dark = true, bound = false;
  let mode = 'none';          // 'none' | 'pick' | 'measure'
  let temp = null;            // temporary pin { lng, lat }
  let line = [];              // measure vertices [[lng, lat], ...]
  let measuring = false;      // true while points can still be added
  let cursor = null;          // last mouse position { lng, lat }
  let selectedManual = null;  // selected point of the "my points" layer
  let results = [];           // Explorer/Monitoring result sets [{ id, title, count, kind, color }]
  let ui = null;
  let toastTimer = 0, errorTimer = 0;

  const empty = () => ({ type: 'FeatureCollection', features: [] });
  const pointFeature = (lng, lat, props) => ({ type: 'Feature', geometry: { type: 'Point', coordinates: [lng, lat] }, properties: props || {} });

  // ---- geometry ---------------------------------------------------------------------------------------------

  function dist(a, b) {
    const rad = Math.PI / 180;
    const dLat = (b[1] - a[1]) * rad, dLng = (b[0] - a[0]) * rad;
    const h = Math.sin(dLat / 2) ** 2 + Math.cos(a[1] * rad) * Math.cos(b[1] * rad) * Math.sin(dLng / 2) ** 2;
    return 2 * EARTH_RADIUS * Math.asin(Math.min(1, Math.sqrt(h)));
  }

  function total() {
    let sum = 0;
    for (let i = 1; i < line.length; i++) sum += dist(line[i - 1], line[i]);
    return sum;
  }

  // [value, unit]; long = full unit words for the cards, short = compact labels on the map
  function distParts(m, long) {
    if (m < 1000) return [m < 10 ? m.toFixed(1) : String(Math.round(m)), long ? T.m : 'm'];
    const km = m / 1000;
    return [km < 100 ? km.toFixed(2) : km.toFixed(1), long ? T.km : 'km'];
  }

  const fmtDist = (m, long) => distParts(m, long).join(' ');
  const fmtCoord = (v) => v.toFixed(6);

  // ---- parsing "35.72, 51.33" / "35.72 51.33" / "35.72N 51.33E" / 35°43'19"N 51°20'05"E (Persian digits accepted) ----------

  function normalize(text) {
    let s = String(text);
    s = s.replace(/[\u06F0-\u06F9]/g, (d) => String(d.charCodeAt(0) - 0x06F0));
    s = s.replace(/[\u0660-\u0669]/g, (d) => String(d.charCodeAt(0) - 0x0660));
    return s.replace(/\u066B/g, '.').replace(/[\u060C;]/g, ',').trim();
  }

  const hemisphere = (h) => (/[SW]/i.test(h || '') ? -1 : 1);

  function parseCoords(input) {
    const s = normalize(input);
    if (!s) return null;
    let a, b, h1 = '', h2 = '';
    if (s.includes('\u00B0')) {
      const parts = [...s.matchAll(/([+-]?\d+(?:\.\d+)?)\s*\u00B0\s*(?:(\d+(?:\.\d+)?)\s*['\u2032]\s*)?(?:(\d+(?:\.\d+)?)\s*(?:"|\u2033|'')\s*)?([NSEW])?/gi)];
      if (parts.length !== 2) return null;
      const deg = (m) => {
        const v = Math.abs(parseFloat(m[1])) + (m[2] ? parseFloat(m[2]) / 60 : 0) + (m[3] ? parseFloat(m[3]) / 3600 : 0);
        return (/^-/.test(m[1]) ? -1 : 1) * hemisphere(m[4]) * v;
      };
      a = deg(parts[0]); b = deg(parts[1]);
      h1 = parts[0][4] || ''; h2 = parts[1][4] || '';
    } else {
      const m = s.match(/^([NSEW])?\s*([+-]?\d+(?:\.\d+)?)\s*([NSEW])?\s*[,\s/]+\s*([NSEW])?\s*([+-]?\d+(?:\.\d+)?)\s*([NSEW])?$/i);
      if (!m) return null;
      h1 = m[1] || m[3] || ''; h2 = m[4] || m[6] || '';
      a = parseFloat(m[2]) * hemisphere(h1); b = parseFloat(m[5]) * hemisphere(h2);
    }
    if (/[EW]/i.test(h1) && /[NS]/i.test(h2)) [a, b] = [b, a]; // longitude typed first, with letters
    let lat = a, lng = b;
    if (Math.abs(lat) > 90 && Math.abs(lng) <= 90) [lat, lng] = [lng, lat]; // obviously swapped
    if (!(Math.abs(lat) <= 90 && Math.abs(lng) <= 180)) return null;
    return { lat, lng };
  }

  // ---- DOM helpers ---------------------------------------------------------------------------------------------

  function el(tag, cls, text) {
    const e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  // Fills a right-to-left element from strings and { n: '123' } numbers; each number is an isolated left-to-right run,
  // so "2 نقطه" and "32.22 کیلومتر" read correctly (number first, then the word).
  function fill(container, ...parts) {
    container.textContent = '';
    for (const p of parts) {
      if (typeof p === 'string') container.appendChild(document.createTextNode(p));
      else container.appendChild(el('span', 'mt-num', p.n));
    }
  }

  const ICON = {
    search: `<circle cx='10.5' cy='10.5' r='6'/><path d='M15 15l6 6'/>`,
    target: `<circle cx='12' cy='12' r='6.5'/><circle cx='12' cy='12' r='1.5' fill='currentColor' stroke='none'/><path d='M12 2.5v4M12 17.5v4M2.5 12h4M17.5 12h4'/>`,
    ruler: `<path d='M3 17.5 17.5 3 21 6.5 6.5 21z'/><path d='M7.5 14.5l2 2M10.5 11.5l2 2M13.5 8.5l2 2'/>`,
    close: `<path d='M6 6l12 12M18 6L6 18'/>`,
    zoom: `<circle cx='11' cy='11' r='6'/><path d='M15.5 15.5 21 21M11 8.5v5M8.5 11h5'/>`
  };

  function icon(name, size) {
    const s = document.createElementNS(SVG_NS, 'svg');
    s.setAttribute('viewBox', '0 0 24 24');
    s.setAttribute('width', String(size || 18)); s.setAttribute('height', String(size || 18));
    s.setAttribute('fill', 'none'); s.setAttribute('stroke', 'currentColor'); s.setAttribute('stroke-width', '1.9');
    s.setAttribute('stroke-linecap', 'round'); s.setAttribute('stroke-linejoin', 'round');
    s.innerHTML = ICON[name];
    return s;
  }

  function iconButton(name, title, onClick, cls) {
    const b = el('button', cls || 'mt-btn');
    b.type = 'button';
    b.title = title; b.setAttribute('aria-label', title);
    b.appendChild(icon(name));
    b.addEventListener('click', onClick);
    return b;
  }

  function textButton(label, cls, onClick, disabled) {
    const b = el('button', cls, label);
    b.type = 'button';
    if (disabled) b.disabled = true;
    b.addEventListener('click', onClick);
    return b;
  }

  function fallbackCopy(text) {
    const t = document.createElement('textarea');
    t.value = text; t.style.position = 'fixed'; t.style.opacity = '0';
    document.body.appendChild(t); t.select();
    try { document.execCommand('copy'); } catch (_) { /* nothing more to try */ }
    t.remove();
  }

  function copyText(text, button, restore) {
    const done = () => {
      button.textContent = T.copied; button.classList.add('ok');
      setTimeout(() => { button.textContent = restore; button.classList.remove('ok'); }, 1200);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).then(done, () => { fallbackCopy(text); done(); });
    else { fallbackCopy(text); done(); }
  }

  // ---- styles ---------------------------------------------------------------------------------------------------

  const CSS = `
#mt-root{position:absolute;inset:0;z-index:6;pointer-events:none;font:13px 'Segoe UI',Tahoma,sans-serif;color:var(--fg);
  --bg:#FFFFFF;--fg:#151B2C;--mut:#5B6478;--line:#E0E5EE;--acc:#4F46E5;--onacc:#FFFFFF;--soft:rgba(79,70,229,.12);--sh:0 6px 22px rgba(15,23,42,.2);--danger:#DC2626}
#mt-root.dark{--bg:#1A2038;--fg:#E8EBF5;--mut:#A3ACC6;--line:#2E3558;--acc:#8A90FF;--onacc:#0E1220;--soft:rgba(138,144,255,.18);--sh:0 6px 22px rgba(0,0,0,.5);--danger:#F87171}
#mt-root *{box-sizing:border-box}
.mt-top{position:absolute;top:12px;left:12px;right:68px;display:flex;flex-direction:column;align-items:flex-start;gap:8px}
.mt-card{pointer-events:auto;background:var(--bg);border:1px solid var(--line);border-radius:14px;box-shadow:var(--sh)}
.mt-bar{display:flex;align-items:center;gap:2px;padding:3px;width:min(470px,100%)}
.mt-input{flex:1;min-width:0;height:34px;border:0;outline:0;background:transparent;color:var(--fg);font:inherit;padding:0 10px;text-align:start}
.mt-input::placeholder{color:var(--mut)}
.mt-btn{display:inline-flex;align-items:center;justify-content:center;width:34px;height:34px;border:0;border-radius:10px;background:transparent;color:var(--mut);cursor:pointer}
.mt-btn:hover{background:var(--soft);color:var(--acc)}
.mt-btn.on{background:var(--acc);color:var(--onacc)}
.mt-btn:focus-visible,.mt-input:focus-visible,.mt-pbtn:focus-visible,.mt-gbtn:focus-visible,.mt-x:focus-visible,.mt-mini:focus-visible,.mt-link:focus-visible{outline:2px solid var(--acc);outline-offset:1px}
.mt-sep{width:1px;height:20px;background:var(--line);margin:0 4px}
.mt-err{display:none;pointer-events:none;color:#fff;background:var(--danger);border-radius:10px;padding:6px 10px;font-size:12px;direction:rtl}
.mt-chips{display:flex;flex-wrap:wrap;gap:6px}
.mt-chip{display:flex;align-items:center;gap:6px;padding:4px 12px 4px 6px;max-width:min(470px,100%);direction:rtl}
.mt-chip-text{font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:320px}
.mt-chip .mt-gbtn{height:26px;padding:0 10px;font-size:12px}
.mt-x{display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border:0;border-radius:8px;background:transparent;color:var(--mut);cursor:pointer}
.mt-x:hover{background:var(--soft);color:var(--acc)}
.mt-pop{position:absolute;left:50%;bottom:34px;transform:translateX(-50%);width:min(430px,calc(100% - 24px));padding:12px 14px 14px;direction:rtl;text-align:right}
.mt-head{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:6px}
.mt-title{font-weight:700;font-size:14px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.mt-tag{display:inline-block;margin-bottom:6px;padding:2px 8px;border-radius:8px;background:var(--soft);color:var(--acc);font-size:11px;font-weight:600}
.mt-num{direction:ltr;unicode-bidi:isolate;display:inline-block}
.mt-kv{display:flex;align-items:center;gap:10px;padding:6px 0;border-bottom:1px dashed var(--line)}
.mt-key{color:var(--mut);font-size:12px;flex:0 0 auto;min-width:88px}
.mt-val{flex:1;direction:ltr;text-align:left;unicode-bidi:isolate;font:600 14px Consolas,'Segoe UI',monospace;user-select:all}
.mt-mini{border:1px solid var(--line);background:transparent;color:var(--fg);border-radius:8px;padding:3px 10px;font:inherit;font-size:12px;cursor:pointer}
.mt-mini:hover{background:var(--soft)}
.mt-mini.ok,.mt-pbtn.ok,.mt-gbtn.ok{background:#16A34A;border-color:#16A34A;color:#fff}
.mt-row{display:flex;gap:8px;margin-top:10px}
.mt-row>*{flex:1}
.mt-pbtn,.mt-gbtn{height:34px;border-radius:10px;font:inherit;font-weight:600;cursor:pointer;padding:0 12px;border:1px solid var(--acc)}
.mt-pbtn{background:var(--acc);color:var(--onacc)}
.mt-gbtn{background:transparent;color:var(--fg);border-color:var(--line)}
.mt-gbtn:hover:not(:disabled){background:var(--soft);border-color:var(--acc)}
.mt-gbtn.danger{color:var(--danger)}
.mt-pbtn:disabled,.mt-gbtn:disabled{opacity:.45;cursor:default}
.mt-add{align-items:stretch}
.mt-name{flex:1.2;min-width:0;height:34px;border:1px solid var(--line);border-radius:10px;background:transparent;color:var(--fg);font:inherit;padding:0 10px;outline:0}
.mt-name:focus{border-color:var(--acc)}
.mt-link{margin-top:10px;border:0;background:transparent;color:var(--acc);font:inherit;font-weight:600;cursor:pointer;padding:2px 0}
.mt-big{font:700 28px 'Segoe UI',Tahoma,sans-serif;direction:rtl;text-align:right;margin:2px 0}
.mt-sub{color:var(--mut);font-size:12px;direction:rtl;text-align:right}
.mt-hint{color:var(--mut);font-size:12px;line-height:1.7;margin-top:8px}
.mt-toast{position:absolute;left:50%;bottom:34px;transform:translate(-50%,10px);background:var(--fg);color:var(--bg);padding:8px 14px;border-radius:10px;opacity:0;transition:opacity .2s,transform .2s;pointer-events:none;direction:rtl;font-weight:600}
.mt-toast.show{opacity:1;transform:translate(-50%,0)}
`;

  // ---- UI ---------------------------------------------------------------------------------------------------------

  function ensureUi() {
    if (ui) return;
    const style = el('style');
    style.textContent = CSS;
    document.head.appendChild(style);

    const root = el('div');
    root.id = 'mt-root';
    root.style.display = 'none';

    const top = el('div', 'mt-top');
    const bar = el('div', 'mt-card mt-bar');
    const input = el('input', 'mt-input');
    input.type = 'text'; input.spellcheck = false; input.autocomplete = 'off';
    input.dir = 'rtl'; // empty: the Persian hint reads and aligns right-to-left
    input.addEventListener('input', () => { input.dir = input.value ? 'ltr' : 'rtl'; }); // typed digits: left-to-right
    input.addEventListener('keydown', (e) => {
      e.stopPropagation(); // typing must not trigger the map shortcuts
      if (e.key === 'Enter') { e.preventDefault(); goTo(input.value); }
    });
    const goBtn = iconButton('search', T.go, () => goTo(input.value));
    const pickBtn = iconButton('target', T.pick, () => setMode('pick'));
    const measureBtn = iconButton('ruler', T.measure, () => setMode('measure'));
    bar.append(input, goBtn, el('span', 'mt-sep'), pickBtn, measureBtn);

    const err = el('div', 'mt-err');
    const chips = el('div', 'mt-chips');
    top.append(bar, err, chips);

    const card = el('div', 'mt-card mt-pop');
    card.style.display = 'none';
    const toastEl = el('div', 'mt-toast');

    root.append(top, card, toastEl);
    document.body.appendChild(root);
    ui = { root, input, goBtn, pickBtn, measureBtn, err, chips, card, toast: toastEl };
    applyLabels();
  }

  function applyLabels() {
    if (!ui) return;
    ui.input.placeholder = T.placeholder;
    ui.input.title = T.example;
    ui.input.setAttribute('aria-label', T.go + ' — ' + T.example);
    for (const [b, text] of [[ui.goBtn, T.go], [ui.pickBtn, T.pick], [ui.measureBtn, T.measure]]) {
      b.title = text; b.setAttribute('aria-label', text);
    }
  }

  function syncButtons() {
    ui.pickBtn.classList.toggle('on', mode === 'pick');
    ui.measureBtn.classList.toggle('on', mode === 'measure');
    ui.pickBtn.setAttribute('aria-pressed', String(mode === 'pick'));
    ui.measureBtn.setAttribute('aria-pressed', String(mode === 'measure'));
  }

  function showError(message) {
    ui.err.textContent = message;
    ui.err.style.display = 'block';
    clearTimeout(errorTimer);
    errorTimer = setTimeout(() => { ui.err.style.display = 'none'; }, 4000);
  }

  function toast(message) {
    ui.toast.textContent = message;
    ui.toast.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => ui.toast.classList.remove('show'), 2300);
  }

  // One chip per result set (Explorer = amber diamond, Monitoring = rose square), plus "remove all" when there are several.
  function renderChips() {
    if (!ui) return;
    ui.chips.textContent = '';
    for (const r of results) {
      const color = /^#[0-9A-Fa-f]{6}$/.test(r.color || '') ? r.color : '#F59E0B';
      const chip = el('div', 'mt-card mt-chip');
      const mark = document.createElementNS(SVG_NS, 'svg');
      mark.setAttribute('viewBox', '0 0 24 24'); mark.setAttribute('width', '16'); mark.setAttribute('height', '16');
      mark.innerHTML = r.kind === 'monitoring'
        ? `<rect x='4' y='4' width='16' height='16' rx='4' fill='${color}'/>`
        : `<path d='M12 2.5l9.5 9.5-9.5 9.5L2.5 12z' fill='${color}'/>`;
      const label = el('span', 'mt-chip-text');
      label.dir = 'auto'; label.title = r.title;
      fill(label, r.title + ' · ', { n: String(r.count) }, ' ' + T.points);
      chip.append(mark, label,
        iconButton('zoom', T.zoom, () => post({ type: 'toolZoomResult', id: r.id }), 'mt-x'),
        iconButton('close', T.remove, () => post({ type: 'toolClearResult', id: r.id }), 'mt-x'));
      ui.chips.appendChild(chip);
    }
    if (results.length >= 2) {
      const all = el('div', 'mt-card mt-chip');
      all.appendChild(textButton(T.removeAll, 'mt-gbtn danger', () => post({ type: 'toolClearResult' })));
      ui.chips.appendChild(all);
    }
  }

  // ---- cards --------------------------------------------------------------------------------------------------------

  function header(title, onClose) {
    const h = el('div', 'mt-head');
    const t = el('div', 'mt-title', title);
    t.dir = 'auto';
    h.append(t, iconButton('close', T.close, onClose, 'mt-x'));
    return h;
  }

  function kv(label, value) {
    const r = el('div', 'mt-kv');
    const b = el('button', 'mt-mini', T.copy);
    b.type = 'button';
    b.addEventListener('click', () => copyText(value, b, T.copy));
    r.append(el('span', 'mt-key', label), el('span', 'mt-val', value), b);
    return r;
  }

  function renderCard() {
    const c = ui.card;
    c.textContent = '';
    c._big = null; c._sub = null;
    if (mode === 'measure') return cardMeasure(c);
    if (temp) return cardPoint(c);
    if (selectedManual) return cardManual(c);
    c.style.display = 'none';
  }

  function cardPoint(c) {
    c.style.display = 'block';
    c.appendChild(header(T.pointTitle, () => { temp = null; redraw(); }));
    const lat = fmtCoord(temp.lat), lng = fmtCoord(temp.lng);
    c.append(kv(T.lat, lat), kv(T.lng, lng));

    const copyAll = textButton(T.copyBoth, 'mt-pbtn', () => copyText(lat + ', ' + lng, copyAll, T.copyBoth));
    const row = el('div', 'mt-row');
    row.appendChild(copyAll);
    c.appendChild(row);

    const name = el('input', 'mt-name');
    name.type = 'text'; name.maxLength = 100; name.dir = 'auto'; name.placeholder = T.namePlaceholder;
    const add = () => {
      post({ type: 'toolAddPoint', lat: temp.lat, lng: temp.lng, name: name.value.trim() });
      temp = null;
      toast(T.added);
      redraw();
    };
    name.addEventListener('keydown', (e) => { e.stopPropagation(); if (e.key === 'Enter') { e.preventDefault(); add(); } });
    const addRow = el('div', 'mt-row mt-add');
    addRow.append(name, textButton(T.addPoint, 'mt-gbtn', add));
    c.appendChild(addRow);

    c.appendChild(textButton(T.measureFrom, 'mt-link', () => { const p = [temp.lng, temp.lat]; temp = null; startMeasure(p); }));
  }

  function cardManual(c) {
    const s = selectedManual;
    c.style.display = 'block';
    c.appendChild(header(s.name, () => { selectedManual = null; redraw(); }));
    c.appendChild(el('span', 'mt-tag', T.manualPoint));
    const lat = fmtCoord(s.lat), lng = fmtCoord(s.lng);
    c.append(kv(T.lat, lat), kv(T.lng, lng));
    const copyAll = textButton(T.copyBoth, 'mt-gbtn', () => copyText(lat + ', ' + lng, copyAll, T.copyBoth));
    const del = textButton(T.deletePoint, 'mt-gbtn danger', () => {
      post({ type: 'toolDeletePoint', layerId: s.layerId, pointId: s.pointId });
      selectedManual = null;
      redraw();
    });
    const row = el('div', 'mt-row');
    row.append(copyAll, del);
    c.appendChild(row);
  }

  function cardMeasure(c) {
    c.style.display = 'block';
    c.appendChild(header(T.measureTitle, () => setMode('none')));
    c.appendChild(el('div', 'mt-key', T.total));
    c._big = el('div', 'mt-big');
    c._sub = el('div', 'mt-sub');
    c.append(c._big, c._sub);
    c.appendChild(el('div', 'mt-hint', line.length === 0 ? T.hintStart : (measuring ? T.hintMore : T.hintDone)));
    const row = el('div', 'mt-row');
    row.appendChild(textButton(T.undo, 'mt-gbtn', undo, line.length === 0));
    if (measuring && line.length >= 2) row.appendChild(textButton(T.finish, 'mt-pbtn', finish));
    row.appendChild(textButton(T.clear, 'mt-gbtn', clearMeasure, line.length === 0));
    c.appendChild(row);
    updateLive();
  }

  // Updates only the numbers (called on every mouse move while measuring): "32.22 کیلومتر", "2 نقطه · آخرین قطعه: 32.22 کیلومتر".
  function updateLive() {
    const c = ui && ui.card;
    if (!c || !c._big) return;
    const live = (measuring && cursor && line.length) ? dist(line[line.length - 1], [cursor.lng, cursor.lat]) : 0;
    const [value, unit] = distParts(total() + live, true);
    fill(c._big, { n: value }, ' ' + unit);
    if (line.length > 1) {
      const [lv, lu] = distParts(dist(line[line.length - 2], line[line.length - 1]), true);
      fill(c._sub, { n: String(line.length) }, ' ' + T.points + '  ·  ' + T.lastSegment + ': ', { n: lv }, ' ' + lu);
    } else {
      fill(c._sub, { n: String(line.length) }, ' ' + T.points);
    }
  }

  // ---- map layers -----------------------------------------------------------------------------------------------

  function ensureSources() {
    if (map.getSource(PIN_SRC)) return;
    const blue = dark ? '#60A5FA' : '#2563EB';
    const ink = dark ? '#E8ECF8' : '#1B2033';
    const halo = dark ? '#0E1220' : '#FFFFFF';
    for (const id of [PIN_SRC, LINE_SRC, RUBBER_SRC, PTS_SRC]) map.addSource(id, { type: 'geojson', data: empty() });
    const round = { 'line-cap': 'round', 'line-join': 'round' };
    map.addLayer({ id: 'tools|line-case', type: 'line', source: LINE_SRC, layout: round, paint: { 'line-color': '#FFFFFF', 'line-width': 7, 'line-opacity': 0.9 } });
    map.addLayer({ id: 'tools|line', type: 'line', source: LINE_SRC, layout: round, paint: { 'line-color': blue, 'line-width': 3.5 } });
    map.addLayer({ id: 'tools|rubber', type: 'line', source: RUBBER_SRC, paint: { 'line-color': blue, 'line-width': 3, 'line-dasharray': [2, 2], 'line-opacity': 0.85 } });
    map.addLayer({ id: 'tools|pts', type: 'circle', source: PTS_SRC, paint: { 'circle-radius': 5.5, 'circle-color': '#FFFFFF', 'circle-stroke-color': blue, 'circle-stroke-width': 3 } });
    map.addLayer({ id: 'tools|lbl', type: 'symbol', source: PTS_SRC,
      layout: { 'text-field': ['get', 'label'], 'text-font': FONT, 'text-size': 12, 'text-anchor': 'bottom', 'text-offset': [0, -0.9], 'text-allow-overlap': true },
      paint: { 'text-color': ink, 'text-halo-color': halo, 'text-halo-width': 2 } });
    map.addLayer({ id: 'tools|pin-halo', type: 'circle', source: PIN_SRC, paint: { 'circle-radius': 18, 'circle-color': PIN_COLOR, 'circle-opacity': 0.22 } });
    map.addLayer({ id: 'tools|pin-dot', type: 'circle', source: PIN_SRC,
      paint: { 'circle-radius': 7, 'circle-color': PIN_COLOR, 'circle-stroke-color': '#FFFFFF', 'circle-stroke-width': 3 } });
  }

  function vertexFeatures() {
    let sum = 0;
    return {
      type: 'FeatureCollection',
      features: line.map((p, i) => {
        if (i > 0) sum += dist(line[i - 1], p);
        return pointFeature(p[0], p[1], { label: i === 0 ? '' : fmtDist(sum, false) });
      })
    };
  }

  function updateRubber() {
    const src = map && map.getSource(RUBBER_SRC);
    if (!src) return;
    const show = mode === 'measure' && measuring && cursor && line.length > 0;
    src.setData(show
      ? { type: 'Feature', geometry: { type: 'LineString', coordinates: [line[line.length - 1], [cursor.lng, cursor.lat]] }, properties: {} }
      : empty());
  }

  function drawMap() {
    if (!map || !ready) return;
    ensureSources();
    map.getSource(PIN_SRC).setData(temp ? pointFeature(temp.lng, temp.lat) : empty());
    map.getSource(LINE_SRC).setData(line.length > 1
      ? { type: 'Feature', geometry: { type: 'LineString', coordinates: line }, properties: {} }
      : empty());
    map.getSource(PTS_SRC).setData(vertexFeatures());
    updateRubber();
    for (const id of TOOL_LAYERS) if (map.getLayer(id)) map.moveLayer(id); // keep the tools above the point layers
  }

  function redraw() {
    if (!ui) return;
    syncButtons();
    drawMap();
    renderCard();
  }

  function applyCursor() {
    if (map) map.getCanvas().style.cursor = mode === 'none' ? '' : 'crosshair';
  }

  // ---- modes ------------------------------------------------------------------------------------------------------

  function leaveMode() {
    if (mode === 'measure') {
      line = []; measuring = false; cursor = null;
      if (map) map.doubleClickZoom.enable();
    }
  }

  function enterMode() {
    if (mode === 'measure') {
      temp = null; selectedManual = null; line = []; measuring = true;
      if (map) map.doubleClickZoom.disable();
    } else if (mode === 'pick') {
      selectedManual = null;
    }
    applyCursor();
  }

  function setMode(next) {
    if (next === mode) next = 'none'; // pressing the active tool again turns it off
    leaveMode();
    mode = next;
    enterMode();
    redraw();
  }

  function startMeasure(first) {
    leaveMode();
    mode = 'measure';
    enterMode();
    line = [first];
    redraw();
  }

  function addVertex(ll) {
    const p = [ll.lng, ll.lat];
    if (line.length && dist(line[line.length - 1], p) < 0.5) return; // a double-click must not add the same point twice
    line.push(p);
    redraw();
  }

  function undo() {
    if (!line.length) return;
    line.pop();
    measuring = true;
    redraw();
  }

  function finish() {
    measuring = false;
    cursor = null;
    redraw();
  }

  function clearMeasure() {
    line = [];
    measuring = true;
    redraw();
  }

  function setTemp(ll) {
    temp = { lng: ll.lng, lat: ll.lat };
    selectedManual = null;
    redraw();
  }

  function goTo(text) {
    const c = parseCoords(text);
    if (!c) { showError(T.invalid); return; }
    ui.err.style.display = 'none';
    if (!map) return;
    leaveMode(); mode = 'none'; enterMode();
    temp = { lng: c.lng, lat: c.lat };
    selectedManual = null;
    map.flyTo({ center: [c.lng, c.lat], zoom: Math.max(map.getZoom(), 13), speed: 1.6 });
    redraw();
  }

  // ---- map events -------------------------------------------------------------------------------------------------

  function onClick(e) {
    if (mode === 'pick') setTemp(e.lngLat);
    else if (mode === 'measure' && measuring) addVertex(e.lngLat);
  }

  function onDblClick(e) {
    if (mode === 'measure' && measuring && line.length >= 2) { e.preventDefault(); finish(); }
  }

  function onContext(e) {
    e.originalEvent.preventDefault(); // right-click = coordinates of this point (finishes a measurement while measuring)
    if (mode === 'measure') { if (measuring && line.length >= 2) finish(); return; }
    setTemp(e.lngLat);
  }

  function onMove(e) {
    cursor = { lng: e.lngLat.lng, lat: e.lngLat.lat };
    if (mode !== 'none') map.getCanvas().style.cursor = 'crosshair';
    if (mode === 'measure' && measuring && line.length) { updateRubber(); updateLive(); }
  }

  function onKey(e) {
    if (!ui || ui.root.style.display === 'none') return;
    const t = e.target;
    if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) return;
    if (e.key === 'Escape') {
      if (temp || selectedManual) { temp = null; selectedManual = null; redraw(); }
      else if (mode !== 'none') setMode('none');
    } else if (e.key === 'Enter' && mode === 'measure' && measuring && line.length >= 2) {
      finish();
    } else if ((e.key === 'Backspace' || e.key === 'Delete') && mode === 'measure') {
      e.preventDefault();
      undo();
    }
  }

  // ---- entry points for map.html ----------------------------------------------------------------------------------

  // Called by map.html every time it creates a map (the old one is already removed).
  window.toolsAttach = function (m, isDark) {
    map = m; ready = false; dark = isDark;
    mode = 'none'; temp = null; line = []; measuring = false; cursor = null; selectedManual = null;
    ensureUi();
    ui.root.classList.toggle('dark', dark);
    ui.root.style.display = 'block';
    if (!bound) {
      bound = true;
      window.addEventListener('keydown', onKey);
      // coordinate-layers.js announces selections; only a selected "my points" point gets a card here.
      window.addEventListener('coordselect', (e) => {
        const d = e.detail;
        if (mode !== 'none' || !d) return;
        if (d.kind === 'manual') { selectedManual = d; temp = null; } else { selectedManual = null; }
        if (ui) redraw();
      });
      window.addEventListener('coordselectclear', () => { selectedManual = null; if (ui) redraw(); });
    }
    map.on('load', () => { ready = true; drawMap(); });
    map.on('click', onClick);
    map.on('dblclick', onDblClick);
    map.on('contextmenu', onContext);
    map.on('mousemove', onMove);
    redraw();
  };

  window.toolsHandle = function (m) {
    switch (m.type) {
      case 'toolsInit':
        Object.assign(T, m.labels || {});
        ensureUi();
        applyLabels();
        renderChips();
        redraw();
        break;
      case 'toolResults':
        results = Array.isArray(m.items) ? m.items : [];
        ensureUi();
        renderChips();
        break;
    }
  };
})();
