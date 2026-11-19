// patches: TASK-1. The spoiler view (fork only, see PATCHES.md).
//
// With reveal_all on, the server sends the whole generated world plus the locations the game
// has planned but not built yet (Patches/Spoilers.cs). Whether a viewer sees it is up to them:
// the Spoilers switch in the left bar's Settings group sets a cookie and reloads the page.
//  - cookie set: the server sends the all-clear fog image and nothing here filters anything;
//  - cookie not set: the server sends the real explored mask as data/fog.png (the page draws it
//    as the fog of war, in 2D and 3D, as always), and this module drops, before the rest of the
//    page sees them, what would otherwise show past the fog: markers on unexplored ground and
//    planned locations, and chunks of buildings, ruins, trees and objects on unexplored ground.
//    The rule is the server's own (World/Fog.cs IsExplored, AnyExplored), so the map looks as it
//    does with reveal_all off.
// With reveal_all off none of this filters anything: the server already did.
//
// No imports from net.js (it imports this): plain fetch only.

import { addSetting, settingRow, settingIcon } from './patches-settings.js';

const COOKIE = 'munin_spoilers';
const WORLD_HALF = 10752, CHUNK = 256;   // TileMath.WORLD_HALF, TileMath.CHUNK_SIZE

// the marker sets the server limits to explored ground (World/Markers.cs); custom sets from the
// admin's markers.json, the spawn and raids are shown wherever they are, so they are left alone
const FOGGED_SETS = new Set(['bosses', 'minibosses', 'dungeons', 'landmarks', 'camps', 'traders', 'portals', 'tablepins',
  'tombstones', 'wards', 'runestones', 'peaks', 'signs', 'vegvisirs', 'beds', 'bases', 'vehicles']);
const CHUNK_INDEXES = new Set(['data/structures/index.json', 'data/ruins/index.json', 'data/objects/index.json']);

export const spoilersOn = (() => { try { return document.cookie.split(/;\s*/).includes(`${COOKIE}=1`); } catch { return false; } })();

let config = null;          // {revealAll, size, px}, once known
let configLoad = null;
let mask = null, maskAt = 0, maskLoad = null;   // the explored mask: size x size bytes, row 0 = north

function loadConfig() {
  if (!configLoad) {
    configLoad = fetch('config', { cache: 'no-store' }).then((r) => r.json())
      .then((c) => { config = { revealAll: !!c.reveal_all, size: c.fog_size || 2048, px: c.fog_pixel || 12 }; })
      .catch(() => { config = { revealAll: false, size: 2048, px: 12 }; });
  }
  return configLoad;
}

// whether this page filters: reveal_all is on and the viewer has not asked for spoilers
async function filtering() {
  if (spoilersOn) return false;
  if (!config) await loadConfig();
  return config.revealAll;
}

// the explored mask, fetched again when older than 20 s (exploring goes on)
async function loadMask() {
  if (mask && Date.now() - maskAt < 20000) return;
  if (!maskLoad) {
    maskLoad = (async () => {
      const r = await fetch('data/fog.png', { cache: 'no-cache' });
      if (!r.ok) throw new Error('fog ' + r.status);
      const bmp = await createImageBitmap(await r.blob());
      const c = document.createElement('canvas');
      c.width = bmp.width; c.height = bmp.height;
      const g = c.getContext('2d', { willReadFrequently: true });
      g.drawImage(bmp, 0, 0);
      const px = g.getImageData(0, 0, c.width, c.height).data;
      const m = new Uint8Array(c.width * c.height);
      for (let i = 0; i < m.length; i++) m[i] = px[i * 4];
      mask = m; maskAt = Date.now();
    })().catch((e) => { console.warn('spoilers: fog mask', e); }).finally(() => { maskLoad = null; });
  }
  await maskLoad;
}

// World/Fog.cs IsExplored, on the north-up PNG
function explored(x, z) {
  if (!mask) return false;
  const n = config.size, half = n / 2;
  const ix = Math.round(x / config.px + half), iz = Math.round(z / config.px + half);
  if (ix < 0 || iz < 0 || ix >= n || iz >= n) return false;
  return mask[(n - 1 - iz) * n + ix] !== 0;
}

// World/Fog.cs AnyExplored over a 256 m chunk
function chunkExplored(cx, cz) {
  if (!mask) return false;
  const n = config.size, half = n / 2, p = config.px;
  const minX = -WORLD_HALF + cx * CHUNK, minZ = -WORLD_HALF + cz * CHUNK;
  const clamp = (v) => Math.max(0, Math.min(n - 1, v));
  const x0 = clamp(Math.floor(minX / p + half)), x1 = clamp(Math.ceil((minX + CHUNK) / p + half));
  const z0 = clamp(Math.floor(minZ / p + half)), z1 = clamp(Math.ceil((minZ + CHUNK) / p + half));
  for (let z = z0; z <= z1; z++) {
    const row = (n - 1 - z) * n;
    for (let x = x0; x <= x1; x++) if (mask[row + x]) return true;
  }
  return false;
}

// ---- hooks (each called from one marked line in an upstream file)

// net.js getJSON: what the page gets from the server, filtered when the fog applies
export async function spoilerJSON(path, d) {
  const p = path.split('?')[0];
  const markers = p === 'data/markers.json', index = CHUNK_INDEXES.has(p);
  if ((!markers && !index) || !d) return d;
  if (!(await filtering())) {
    // planned locations only ever show in the spoiler view: a server just switched off reveal_all
    // still serves its last markers (kept on disk) until its first sweep
    if (markers && !spoilersOn && Array.isArray(d.sets))
      d.sets = d.sets.map((s) => (Array.isArray(s.markers) && s.markers.some((m) => m.placed === false) ? Object.assign({}, s, { markers: s.markers.filter((m) => m.placed !== false) }) : s));
    return d;
  }
  await loadMask();
  if (markers && Array.isArray(d.sets)) {
    d.sets = d.sets.map((s) => {
      if (!FOGGED_SETS.has(s.id) || !Array.isArray(s.markers)) return s;
      let ms = s.markers.filter((m) => m.placed !== false && explored(m.x, m.z));
      // the server calls the highest peak it shows "Highest peak" (peaks come highest first): it
      // saw them all, so name the highest one left
      if (s.id === 'peaks') ms = ms.map((m, i) => Object.assign({}, m, { label: String(m.label || '').replace(/^(Highest peak|Peak) · /, i === 0 ? 'Highest peak · ' : 'Peak · ') }));
      return Object.assign({}, s, { markers: ms });
    });
  }
  if (index && Array.isArray(d.chunks)) d.chunks = d.chunks.filter((c) => chunkExplored(c[0], c[1]));
  return d;
}

// data.js, a vegetation chunk (2D packs and 3D points): false when the fog hides it
export async function spoilerChunk(cx, cz) {
  if (!(await filtering())) return true;
  await loadMask();
  return chunkExplored(cx, cz);
}

// biomehover.js: no biome name over the fog
export function spoilerExplored(x, z) {
  if (spoilersOn || !config || !config.revealAll) return true;
  return explored(x, z);
}

// markers.js popup: what a planned location adds to its popup
export function spoilerPopup(m, extra) {
  if (m.placed !== false) return extra;
  const lines = [];
  if (!/not yet generated/.test(extra)) lines.push('Not generated yet');
  if (m.sites > 1) lines.push(`One of ${m.sites} possible sites: the game builds only the first one a player comes near`);
  return lines.length ? (extra ? extra + '<br>' : '') + lines.map((t) => `<small>${t}</small>`).join('<br>') : extra;
}

// The Spoilers switch in the fork's Settings group (patches-settings.js), only when reveal_all is
// on. The cookie is its only state: flipping it reloads the page, which the server and this
// module then read.
addSetting((app) => {
  if (!app.config || !app.config.reveal_all) return null;
  const r = settingRow(settingIcon('<path d="M2 12s3.6-6.5 10-6.5S22 12 22 12s-3.6 6.5-10 6.5S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>'),
    'Spoilers', 'Whole world, all locations (reloads)', spoilersOn, (on, input) => {
      try { document.cookie = `${COOKIE}=${on ? 1 : 0}; path=/; max-age=${on ? 31536000 : 0}; SameSite=Lax`; } catch { /* no cookies */ }
      if (document.cookie.split(/;\s*/).includes(`${COOKIE}=1`) === spoilersOn) { input.checked = spoilersOn; return; }   // the cookie did not stick: nothing to reload into
      keepPlace(r);
      location.reload();
    });
  restorePlace(r);
  return r;
});

// The reload must not leave the sidebar scrolled back to the top, with the switch out of view:
// note where the Spoilers row sat in its scrolling panel, and put it back there after the
// reload. The panels above fill in a moment later (the marker list, for one), so it is held in
// place while the sidebar settles, until the viewer scrolls themselves.
const PLACE_KEY = 'munin-spoilers-place';
function scroller(elm) {
  for (let e = elm.parentElement; e; e = e.parentElement) {
    const o = getComputedStyle(e).overflowY;
    if ((o === 'auto' || o === 'scroll') && e.scrollHeight > e.clientHeight) return e;
  }
  return null;
}
function keepPlace(c) {
  const sc = scroller(c);
  if (!sc) return;
  try { sessionStorage.setItem(PLACE_KEY, String(c.getBoundingClientRect().top - sc.getBoundingClientRect().top)); } catch { /* not kept */ }
}
function restorePlace(c) {
  let offset = null;
  try { offset = sessionStorage.getItem(PLACE_KEY); sessionStorage.removeItem(PLACE_KEY); } catch { /* none */ }
  if (offset === null || isNaN(+offset)) return;
  offset = +offset;
  let sc = null, until = Date.now() + 4000, stopped = false;
  const stop = () => { stopped = true; };
  const place = () => {
    if (stopped || Date.now() > until) { if (sc) ['wheel', 'touchstart', 'keydown', 'mousedown'].forEach((t) => sc.removeEventListener(t, stop)); return; }
    sc = sc || scroller(c);
    if (sc) {
      if (!sc.dataset.spoilerWatch) { sc.dataset.spoilerWatch = '1'; ['wheel', 'touchstart', 'keydown', 'mousedown'].forEach((t) => sc.addEventListener(t, stop, { passive: true })); }
      const d = (c.getBoundingClientRect().top - sc.getBoundingClientRect().top) - offset;
      if (Math.abs(d) > 1) sc.scrollTop += d;
    }
    requestAnimationFrame(place);
  };
  requestAnimationFrame(place);
}

loadConfig();
