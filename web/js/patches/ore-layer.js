// patches: TASK-4 (TASK-4.4). The fork's ore layer: all ore, real and predicted, in one layer and
// one style (fork only, see PATCHES.md).
//
// Real deposits come from data/ore.json (Patches/OreLayer.cs), predicted ones from
// data/predicted-ore.json when the server has predictions on (Patches/PredictedOre.cs). Two styles
// for both, chosen in the Ore row and kept per browser:
//   classic (default): as upstream's ore, a small filled dot in each ore's colour; predicted ones the
//            same dot, hollow
//   shapes:  a shape per ore (copper circle, tin square, silver diamond, obsidian triangle, muddy
//            scrap pentagon), filled for real and hollow for predicted, big enough to see zoomed out
// On the WebGL map this layer draws the real ore too, and upstream's own ore dots are hidden while it
// does (vegpack.js vegShowMask, on layerState.vegForkOre); the Ore row's switch and checklist drive
// both, with "Include predicted" and the style in its checklist. The 3D view keeps upstream's ore.
// The plain (non-WebGL) map has no ore row (real ore is part of upstream's Trees & rocks there), so it
// gets no predictions either, for consistency (with the user): this layer isn't used there.
//
// Also: rows in the stats panel's Server details whenever predictions are in use (TASK-4.6, 4.7):
// the predicted deposits, how far an in-place run is while it goes, and the game version in-place
// was verified on when the server runs another.
// ui.js calls oreRows (the layer panel) and predictedOreLine (Server details) from one marked line each.

import { on } from '../net.js';
import { TILE, WORLD_HALF, metersPerPixel } from '../crs.js';
import { VEG, VEG_GROUPS } from '../vegpack.js';
import { layerState as S } from '../layerstate.js';
import { escape } from '../layers/markers.js';

const KEY_STYLE = 'munin-fork-ore-style';                 // classic | shapes, kept per browser
const KEY_PREDICTED = 'munin-fork-ore-predicted';         // include predicted ore (WebGL map)
const ORE = VEG_GROUPS.find((g) => g.key === 'vegOre');
const CHUNK = 256;
// [sides, rotation in turns]; 0 sides = a circle
const SHAPES = { 34: [0, 0], 35: [4, 0.125], 36: [4, 0], 37: [3, -0.25], 38: [5, -0.25], 8: [6, 0] };
const SIZE = (k) => (k === 35 || k === 38 ? 0.4 : 1.2);   // Vegetation.ClassifyName: tin and scrap are small

const read = (k, d) => { try { const v = localStorage.getItem(k); return v === null ? d : v; } catch { return d; } };
const write = (k, v) => { try { localStorage.setItem(k, v); } catch { /* this visit only */ } };

let style = read(KEY_STYLE, 'classic') === 'shapes' ? 'shapes' : 'classic';
let includePredicted = read(KEY_PREDICTED, '1') === '1';

let drawReal = false, predictOn = false;
let real = null, predicted = null;           // the last responses
let realIndex = new Map(), insideIndex = new Map(), predIndex = new Map();
let layer = null;

const chunkKey = (x, z) => Math.floor((x + WORLD_HALF) / CHUNK) * 4096 + Math.floor((z + WORLD_HALF) / CHUNK);
function indexOf(a) {
  const m = new Map();
  for (let i = 0; i + 2 < a.length; i += 3) {
    const k = chunkKey(a[i + 1], a[i + 2]);
    let l = m.get(k);
    if (!l) m.set(k, (l = []));
    l.push(a[i], a[i + 1], a[i + 2]);
  }
  return m;
}

const busy = {};
async function fetchJson(url, have, apply) {
  if (busy[url]) return;
  busy[url] = true;
  try {
    const d = await (await fetch(url, { cache: 'no-store' })).json();
    if (have && d.rev === have.rev && (d.d || []).length === (have.d || []).length && (d.i || []).length === (have.i || []).length) return;
    apply(d);
    if (layer) layer.refresh();
  } catch (e) { console.warn('ore layer', url, e); }
  finally { busy[url] = false; }
}
const loadReal = () => drawReal && fetchJson('data/ore.json', real, (d) => { real = d; realIndex = indexOf(d.d || []); insideIndex = indexOf(d.i || []); });
const loadPredicted = () => predictOn && fetchJson('data/predicted-ore.json', predicted, (d) => {
  predicted = d; predIndex = indexOf(d.d || []);
  refreshLine();
});
const loadAll = () => { loadReal(); loadPredicted(); };
on('world', loadAll);
on('reconnected', loadAll);

// ---- drawing

function shapePath(ctx, kind, x, y, r) {
  const [sides, turn] = SHAPES[kind] || [0, 0];
  ctx.beginPath();
  if (!sides) { ctx.arc(x, y, r, 0, Math.PI * 2); return; }
  const R = r * (sides === 3 ? 1.35 : sides === 4 ? 1.2 : 1.1);   // polygons read as big as the circle
  for (let i = 0; i < sides; i++) {
    const a = (turn + i / sides) * Math.PI * 2;
    if (i) ctx.lineTo(x + Math.cos(a) * R, y + Math.sin(a) * R); else ctx.moveTo(x + Math.cos(a) * R, y + Math.sin(a) * R);
  }
  ctx.closePath();
}

function colours(kind) {
  const c = (VEG[kind] || VEG[8])[1], n = parseInt(c.slice(1), 16);
  const lum = 0.299 * (n >> 16) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255);
  return { c, halo: lum < 90 ? 'rgba(235,235,235,0.85)' : 'rgba(0,0,0,0.75)', tint: `rgba(${n >> 16},${(n >> 8) & 255},${n & 255},0.25)` };
}

const kindHidden = (k) => S.vegOre === false || S.vegHidden.has(k);

function drawOne(ctx, k, px, py, ppm, isPredicted) {
  const col = colours(k);
  if (style === 'classic') {
    // as upstream draws a deposit (vegpack.js VEG radius x Vegetation's size), never below a pixel
    // a predicted one as big as a real one, so neither outweighs the other zoomed out: too small for a
    // ring (under 2 px), a see-through filled dot; with room, a hollow ring
    const r = Math.max(0.9, VEG[k][0] * SIZE(k) * ppm);
    ctx.beginPath(); ctx.arc(px, py, r, 0, Math.PI * 2);
    if (isPredicted) {
      if (r < 2) { ctx.globalAlpha = 0.45; ctx.fillStyle = col.c; ctx.fill(); ctx.globalAlpha = 1; }
      else { ctx.lineWidth = Math.max(1, Math.min(2.5, r * 0.3)); ctx.strokeStyle = col.c; ctx.stroke(); }
    } else {
      ctx.fillStyle = col.c; ctx.fill();
      if (r > 2.5) { ctx.lineWidth = 0.8; ctx.strokeStyle = 'rgba(0,0,0,0.45)'; ctx.stroke(); }
    }
    return;
  }
  const r = Math.max(5, 3 * ppm);
  shapePath(ctx, k, px, py, r);
  ctx.lineJoin = 'round';
  if (isPredicted) {
    ctx.fillStyle = col.tint; ctx.fill();   // see-through: hollow
    ctx.lineWidth = 5; ctx.strokeStyle = col.halo; ctx.stroke();
    ctx.lineWidth = 2.6; ctx.strokeStyle = col.c; ctx.stroke();
  } else {
    ctx.lineWidth = 3; ctx.strokeStyle = col.halo; ctx.stroke();
    ctx.fillStyle = col.c; ctx.fill();      // filled: real
  }
}

class OreLayer extends L.GridLayer {
  constructor(pane) { super({ tileSize: TILE, updateWhenIdle: false, keepBuffer: 2, className: 'fork-ore-tile', pane }); }

  // The page's glide (smoothzoom.js glideTo, a marker clicked in a list) moves the view by moving the
  // pixel origin, not the map pane. A GridLayer places its tiles again only on 'zoom', so a glide at
  // one zoom left the ore where it was. Placed again on every 'move' too (TASK-35).
  // (the same function every time: Leaflet unbinds what getEvents returns when the layer is removed)
  getEvents() {
    const events = super.getEvents(), onMove = events.move;
    this._placeOnMove = this._placeOnMove || function (e) {
      if (this._map) this._setZoomTransforms(this._map.getCenter(), this._map.getZoom());
      if (onMove) onMove.call(this, e);
    };
    events.move = this._placeOnMove;
    return events;
  }

  // draw every tile again where it stands (Leaflet's redraw() rebuilds them, which blinks)
  refresh() {
    const dpr = Math.min(devicePixelRatio || 1, 3);
    for (const key in this._tiles || {}) {
      const t = this._tiles[key], canvas = t && t.el;
      if (!canvas || !canvas.getContext) continue;
      const ctx = canvas.getContext('2d');
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      try { this.draw(canvas, t.coords, dpr); } catch (e) { console.warn('ore tile', e); }
    }
  }

  createTile(coords) {
    const canvas = document.createElement('canvas');
    const dpr = Math.min(devicePixelRatio || 1, 3);
    canvas.width = TILE * dpr; canvas.height = TILE * dpr;
    canvas.style.width = canvas.style.height = TILE + 'px';
    try { this.draw(canvas, coords, dpr); } catch (e) { console.warn('ore tile', e); }
    return canvas;
  }

  draw(canvas, coords, dpr) {
    const showPred = predictOn && includePredicted;
    if (!(drawReal && (realIndex.size || insideIndex.size)) && !(showPred && predIndex.size)) return;
    const mpp = metersPerPixel(coords.z), ppm = 1 / mpp, span = TILE * mpp;
    const minX = -WORLD_HALF + coords.x * span, maxZ = WORLD_HALF - coords.y * span;
    const maxX = minX + span, minZ = maxZ - span;
    const margin = (Math.max(8, 4 * ppm) + 4) * mpp;
    const ctx = canvas.getContext('2d');
    ctx.scale(dpr, dpr);
    const c0 = Math.floor((minX - margin + WORLD_HALF) / CHUNK), c1 = Math.floor((maxX + margin + WORLD_HALF) / CHUNK);
    const z0 = Math.floor((minZ - margin + WORLD_HALF) / CHUNK), z1 = Math.floor((maxZ + margin + WORLD_HALF) / CHUNK);
    const pass = (idx, isPredicted) => {
      for (let cx = c0; cx <= c1; cx++) {
        for (let cz = z0; cz <= z1; cz++) {
          const l = idx.get(cx * 4096 + cz);
          if (!l) continue;
          for (let i = 0; i < l.length; i += 3) {
            const k = l[i], x = l[i + 1], z = l[i + 2];
            if (kindHidden(k) || x < minX - margin || x > maxX + margin || z < minZ - margin || z > maxZ + margin) continue;
            drawOne(ctx, k, (x - minX) * ppm, (maxZ - z) * ppm, ppm, isPredicted);
          }
        }
      }
    };
    if (showPred) pass(predIndex, true);       // under the real ones
    if (drawReal) pass(realIndex, false);
    // ore inside dungeons (crypt scrap): classic only. Never predicted, not on the ground the map
    // shows, and found by its crypt anyway, so the shapes (for spotting ore) leave it out
    if (drawReal && style === 'classic') pass(insideIndex, false);
  }
}

// ---- the layer panel

function swatch(kind, hollow) {
  const [sides, turn] = SHAPES[kind] || [0, 0], c = (VEG[kind] || VEG[8])[1];
  let d;
  if (!sides) d = '<circle cx="5" cy="5" r="3.4"/>';
  else {
    const R = 3.4 * (sides === 3 ? 1.3 : sides === 4 ? 1.2 : 1.1), pts = [];
    for (let i = 0; i < sides; i++) { const a = (turn + i / sides) * Math.PI * 2; pts.push(`${(5 + Math.cos(a) * R).toFixed(1)},${(5 + Math.sin(a) * R).toFixed(1)}`); }
    d = `<polygon points="${pts.join(' ')}"/>`;
  }
  return `<svg class="fork-ore-swatch" width="9" height="9" viewBox="0 0 10 10" style="flex:none;overflow:visible" fill="${c}" fill-opacity="${hollow ? 0.25 : 1}" stroke="${c}" stroke-width="${hollow ? 1.6 : 0.8}">${d}</svg>`;
}
const classicSwatch = (kind, hollow) => `<i class="fork-ore-swatch" style="background:${hollow ? 'transparent' : VEG[kind][1]};${hollow ? `box-sizing:border-box;border:2px solid ${VEG[kind][1]};` : ''}border-radius:50%"></i>`;

function ensureLayer(app) {
  if (layer) return layer;
  // its own pane over the map: the WebGL map draws the ground on a canvas in shapesPane (z 245),
  // above Leaflet's tile pane; under the world edge (450) and the markers
  const pane = app.map.getPane('forkOrePane') || app.map.createPane('forkOrePane');
  pane.style.zIndex = 250;
  pane.style.pointerEvents = 'none';
  layer = new OreLayer('forkOrePane').addTo(app.map);
  return layer;
}

// the style's look in the Ore row's checklist: upstream's own dots (classic) or the shapes
function restyleChecklist(list) {
  if (!list) return;
  for (const lab of list.querySelectorAll('label.vkind')) {
    const k = +lab.dataset.forkKind;
    if (!k) continue;
    const own = lab.querySelector('i:not(.fork-ore-swatch)');
    lab.querySelectorAll('.fork-ore-swatch').forEach((e) => e.remove());
    if (style === 'shapes') {
      if (own) own.style.display = 'none';
      (own || lab.querySelector('input')).insertAdjacentHTML('afterend', swatch(k, lab.dataset.forkPredicted === '1'));
    } else {
      if (own) own.style.display = '';
      if (!own) lab.querySelector('input').insertAdjacentHTML('afterend', classicSwatch(k, lab.dataset.forkPredicted === '1'));
    }
  }
}

// ui.js, after the vegetation rows (one marked line): "Include predicted" and the style go into the
// Ore row's checklist; nothing new is returned. Nothing at all on the plain (non-WebGL) map.
export function oreRows(app, groupRow, icon, world) {
  if (!app || !app.map || !app.gl) return [];
  predictOn = !!(app.config && app.config.predict_ore);
  drawReal = true;
  ensureLayer(app);
  S.set('vegForkOre', true);   // upstream's own ore dots off: this layer draws them
  // the Ore row's checklist (ui.js groupRow: the row, then its list)
  let list = null;
  for (const r of (world || document).querySelectorAll('.lrow')) {
    const b = r.querySelector('.ltext b');
    if (b && b.textContent.trim() === ORE.label && r.nextElementSibling && r.nextElementSibling.classList.contains('vkinds')) { list = r.nextElementSibling; break; }
  }
  if (list) {
    const byName = new Map(ORE.kinds.map(([k, n]) => [n, k]));
    for (const lab of list.querySelectorAll('label.vkind')) {
      const k = byName.get(lab.querySelector('span')?.textContent?.trim());
      if (k) lab.dataset.forkKind = String(k);
    }
    const extra = document.createElement('div');
    extra.className = 'fork-ore-extra';
    extra.style.cssText = 'grid-column:1/-1;display:flex;flex-wrap:wrap;gap:4px 12px;align-items:center;padding:4px 2px 0;border-top:1px solid var(--line, rgba(127,127,127,.25));margin-top:3px';
    extra.innerHTML = `<label class="vkind" title="Shapes are easier to tell apart and to see when zoomed out">Style <select style="margin-left:4px">`
      + `<option value="classic"${style === 'classic' ? ' selected' : ''}>Classic</option><option value="shapes"${style === 'shapes' ? ' selected' : ''}>Shapes</option></select></label>`
      + (predictOn ? `<label class="vkind" data-fork-kind="34" data-fork-predicted="1" title="Where the game will place ore, in zones not generated yet">`
        + `<input type="checkbox"${includePredicted ? ' checked' : ''}><span>Predicted ore</span></label>` : '');
    extra.querySelector('select').addEventListener('change', (e) => { style = e.target.value; write(KEY_STYLE, style); restyleChecklist(list); layer.refresh(); });
    const pc = extra.querySelector('input[type=checkbox]');
    if (pc) pc.addEventListener('change', (e) => { includePredicted = e.target.checked; write(KEY_PREDICTED, includePredicted ? '1' : '0'); layer.refresh(); });
    list.append(extra);
    restyleChecklist(list);
  }
  loadAll();
  return [];
}

S.onChange((key) => {
  if ((key === 'vegOre' || key === 'vegHidden') && layer) layer.refresh();
});

// ---- Server details and the version note

export function predictedOreLine(section) {
  const dl = section && section.querySelector('dl.kv');
  if (!dl || !predictOn || !predicted || !predicted.on) return;
  // rows of the card's own name / value list (display: contents keeps them in its grid), then a short
  // note for a warning or a failure; refreshed in place while the server makes a prediction
  dl.insertAdjacentHTML('beforeend', `<div class="fork-po" style="display:contents">${rowsHtml()}</div>`);
}

function rowsHtml() {
  const row = (k, v) => `<dt>${k}</dt><dd>${v}</dd>`;
  const notes = [];
  const ok = predicted.status === 'ok';
  const ip = predicted.inplace || null;   // the in-place run (TASK-4.6)
  const ipFailed = ip && /^failed/.test(ip.state || '');
  let h = '';
  if (predicted.game) h += row('Server version', escape(predicted.game));
  h += row('Predicted ore', ok ? `${(predicted.total || 0).toLocaleString()} deposits` : 'none');
  if (ip) {
    // the update row only while a run is in progress
    if (ip.state === 'running' && ip.total) h += row('Prediction update', `${(ip.done || 0).toLocaleString()} of ${ip.total.toLocaleString()} (${Math.floor((100 * (ip.done || 0)) / ip.total)}%)`);
    else if (ip.state && ip.state.startsWith('waiting')) h += row('Prediction update', 'waiting for the world');
    if (ipFailed) notes.push(`Prediction ${escape(ip.state)}.`);
    if (ip.checked && predicted.game && ip.checked !== predicted.game) {
      h += row('Verified on', escape(ip.checked));
      notes.push('Predicted ore uses placement rules verified on a different version, locations may be wrong.');
    }
  }
  if (notes.length) h += `<div style="grid-column:1/-1;font-size:12px;line-height:1.35;color:var(--muted)">${notes.join(' ')}</div>`;
  return h;
}

function refreshLine() {
  if (!predicted || !predicted.on) return;
  const html = rowsHtml();
  for (const el of document.querySelectorAll('.fork-po')) if (el.innerHTML !== html) el.innerHTML = html;
}

