// patches: TASK-20. Claimed players on the page (fork only, see PATCHES.md).
//
// With the server's require_auth on, pins, pings and base edits need a claimed player
// (src/Patches/Access.cs, Claims.cs). A browser claims one of the players Munin knows with
// the user key the admin shares; the claim belongs to this browser's ID (upstream's "Pin key", shown
// here as Browser ID), so entering that ID in another browser makes it the same player. Here:
// - the pin form (right click) says who you are, or offers the claim, and Add pin and Ping wait for one;
// - the base popup does the same for rename and hide, and names who renamed a base;
// - both show the Browser ID, and a claimed browser can only switch to another claimed ID;
// - middle click pings as the claimed player, or says to claim first;
// - with the admin key stored, the players panel lists the claims, each with Unclaim.
// With require_auth off none of this changes anything, except "renamed by" on a renamed base.
import { getJSON } from '../net.js';
import { clientId, setClientId, escape } from '../layers/markers.js';

const authOn = () => window.app?.config?.require_auth === true;
const toast = (msg) => window.app?.toast?.(msg);
const adminKey = () => { try { return localStorage.getItem('munin-admin-key') || ''; } catch (e) { return ''; } };

// this browser's claim, {requireAuth, open, name}, for the key it has now
let mine = null, mineFor = null;
async function claim(fresh) {
  const key = clientId();
  if (fresh || !mine || mineFor !== key) {
    mine = await getJSON('data/claim.json?client=' + encodeURIComponent(key)).catch(() => ({}));
    mineFor = key;
  }
  return mine;
}

// the claim block: who to claim, the user key, Claim. done(name) once claimed.
function claimBlock(done) {
  const box = document.createElement('div');
  box.className = 'fork-claim';
  box.innerHTML = '<small>Loading…</small>';
  (async () => {
    const c = await claim(true);
    if (c.name) { done(c.name); return; }
    if (!c.open) { box.innerHTML = '<small>To place pins, ping or edit bases, claim your player. Claiming is closed on this server: ask the admin.</small>'; return; }
    const list = await getJSON('data/claimable.json').catch(() => []);
    const names = new Map(); for (const p of list) names.set(p.name, (names.get(p.name) || 0) + 1);
    const label = (p) => names.get(p.name) > 1 && p.firstSeen ? `${p.name} (first seen ${new Date(p.firstSeen).toLocaleDateString()})` : p.name;
    box.innerHTML = `<small>To place pins, ping or edit bases, claim your player.</small>
      <div class="row"><select data-f="who">${list.map((p) => `<option value="${escape(p.id)}">${escape(label(p))}</option>`).join('')}</select></div>
      <div class="row"><input data-f="ukey" type="text" placeholder="User key (from the admin)" autocomplete="off" spellcheck="false"><button class="btn small" type="button" data-act="claim">Claim</button></div>
      <small>Not listed? Join the game server first: the map only knows players it has seen online.</small>
      <small class="err" hidden></small>`;
    const err = box.querySelector('.err');
    box.querySelector('[data-f=ukey]').addEventListener('keydown', (ev) => { if (ev.key === 'Enter') { ev.preventDefault(); box.querySelector('[data-act=claim]').click(); } });
    box.querySelector('[data-act=claim]').addEventListener('click', async () => {
      const id = box.querySelector('[data-f=who]').value, token = box.querySelector('[data-f=ukey]').value.trim();
      if (!id || !token) return;
      try {
        const r = await fetch('api/claim', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Munin-Client': clientId() }, body: JSON.stringify({ id, token }) });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.error || r.status);
        mine = { ...mine, name: j.name }; mineFor = clientId();
        done(j.name);
      } catch (e) { err.textContent = e.message === 'wrong user key' ? 'That key was not accepted.' : 'Could not claim: ' + e.message; err.hidden = false; }   // (as the Admin key's message)
    });
  })();
  return box;
}

// Switching to another browser ID ("Use ID"): a browser whose ID holds a claim may only switch to
// an ID that holds one too (the same player's ID from another browser), so a claimed browser can't
// start over with a fresh ID. A browser with no claim may set any ID. null when fine, else why not.
async function switchRefused(id) {
  const clean = String(id || '').replace(/[^A-Za-z0-9_-]/g, '').slice(0, 40);
  if (!clean) return "That ID can't be used here.";
  if (clean === clientId()) return null;
  const now = await claim(true);
  if (!now.name) return null;
  const other = await getJSON('data/claim.json?client=' + encodeURIComponent(clean)).catch(() => ({}));
  return other.name ? null : 'That ID was not accepted.';
}

const ID_HELP = "This browser's ID: your claim belongs to it. To act as the same player in another browser, copy it there and press Use ID.";

// upstream's "Pin key" box in the pin form, as the Browser ID, with the switch check in front of its button
function browserIdBox(el) {
  const box = el.querySelector('details.pin-key');
  if (!box) return;
  box.querySelector('summary').textContent = 'Browser ID';
  const help = box.querySelector('small'); if (help) help.textContent = ID_HELP;
  const btn = box.querySelector('[data-act=key]'), input = box.querySelector('[name=key]'), err = el.querySelector('.err');
  if (!btn || !input) return;
  btn.textContent = 'Use ID'; btn.style.whiteSpace = 'nowrap';
  btn.addEventListener('click', async (ev) => {
    if (btn.dataset.ok) { delete btn.dataset.ok; return; }   // (checked: upstream's handler takes it from here)
    ev.stopImmediatePropagation();
    if (err) { err.textContent = ''; err.hidden = true; }   // (a message from an earlier try; .pin-form .err is display:block, so hidden alone leaves it showing)
    const why = await switchRefused(input.value.trim());
    if (why) { if (err) { err.textContent = why; err.hidden = false; } return; }
    btn.dataset.ok = '1'; btn.click();
  }, true);
}

// the same box for the base popup, which has none upstream
function baseIdBox(el) {
  const box = document.createElement('details');
  box.className = 'pin-key';
  box.innerHTML = `<summary>Browser ID</summary><small>${ID_HELP}</small>
    <div class="row"><input data-f="bid" value="${escape(clientId())}" autocomplete="off" spellcheck="false"><button class="btn small" type="button" style="white-space:nowrap">Use ID</button></div>
    <small class="err" hidden></small>`;
  const input = box.querySelector('[data-f=bid]'), err = box.querySelector('.err');
  box.querySelector('button').addEventListener('click', async () => {
    const v = input.value.trim();
    err.textContent = ''; err.hidden = true;   // (a message from an earlier try; .pin-form .err is display:block, so hidden alone leaves it showing)
    if (v === clientId()) return;
    const why = await switchRefused(v);
    if (why || !setClientId(v)) { err.textContent = why || "That ID can't be used here."; err.hidden = false; return; }
    window.app?.layers?.markers?.loadPins?.();
    window.app?.map?.closePopup();
  });
  const wrap = document.createElement('div');
  wrap.className = 'pin-form';   // (the pin form's styles: one row for the ID and its button)
  wrap.append(box);
  el.append(wrap);
}

// the pin form (layers/markers.js openPinEditor): the name field gives way to the claimed name
export function claimPinForm(el) {
  if (!authOn()) return;
  browserIdBox(el);
  const name = el.querySelector('[name=name]');
  const buttons = [el.querySelector('button[type=submit]'), el.querySelector('[data-act=ping]')].filter(Boolean);
  if (!name) return;
  const hidden = document.createElement('input');
  hidden.type = 'hidden'; hidden.name = 'name';
  const who = document.createElement('small');
  name.replaceWith(hidden, who);
  for (const b of buttons) b.disabled = true;
  const done = (n) => { hidden.value = n; who.innerHTML = `as <b>${escape(n)}</b>`; for (const b of buttons) b.disabled = false; block.remove(); };
  const block = claimBlock(done);
  el.querySelector('.err')?.before(block);
}

// the base popup (layers/markers.js basePopup): rename, hide and auto name wait for a claim
export function claimBaseForm(el) {
  if (!authOn()) return;
  const form = el.querySelector('form');
  if (!form) return;
  const buttons = [...form.querySelectorAll('button')];
  for (const b of buttons) b.disabled = true;
  const done = () => { for (const b of buttons) b.disabled = false; block.remove(); };
  const block = claimBlock(done);
  form.querySelector('.err')?.before(block);
  baseIdBox(el);
}

// a marker popup's extra lines (layers/markers.js popupHtml): who renamed a base
export function renamedBy(m, extra) {
  if (m.cat !== 'base' || !m.renamedBy || m.renamedBy === 'web') return extra;
  return `${extra}<br><small>Renamed by ${escape(m.renamedBy)}</small>`;
}

// middle click (app.js webPing): true when handled here (require_auth on)
export async function claimPing(latlng, x, z) {
  if (!authOn()) return false;
  const c = await claim(false);
  if (!c.name) { toast('Right-click the map to claim your player first'); return true; }
  try {
    const r = await fetch('api/ping', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Munin-Client': clientId() }, body: JSON.stringify({ x: Math.round(x * 10) / 10, z: Math.round(z * 10) / 10, name: c.name }) });
    if (r.status === 429) { const j = await r.json().catch(() => ({})); toast(`Wait ${j.wait || 10} s to ping again`); }
    else if (r.status === 403) { mine = null; toast('Right-click the map to claim your player first'); }
    else if (!r.ok) toast('Could not ping');
  } catch (e) { toast('Could not ping'); }
  return true;
}

// the players panel (ui.js playersPanel), with the admin key stored: the claims, each with Unclaim
let claims = null, claimsAt = 0, claimsGen = 0;   // (gen: an answer asked for before an unclaim is stale)
export function adminClaims(panel) {
  if (!authOn() || !adminKey()) return;
  const box = document.createElement('div');
  panel.append(box);
  const draw = (list) => {
    box.replaceChildren();
    if (!list || !list.length) return;
    const h = document.createElement('h3');
    h.innerHTML = `Claimed players <span class="count">${list.length}</span>`;
    const rows = document.createElement('div');
    rows.className = 'mk-list';
    for (const c of list) {
      const r = document.createElement('div');
      r.className = 'mrow';
      r.innerHTML = `<div class="grow"><div class="name">${escape(c.name)}</div><div class="meta">claimed ${escape(new Date(c.at).toLocaleString())}</div></div><button class="btn small" type="button">Unclaim</button>`;
      r.querySelector('button').addEventListener('click', async () => {
        if (!confirm(`Unclaim ${c.name}? They can then be claimed again from any browser with the user key.`)) return;
        const res = await fetch('api/unclaim', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Munin-Token': adminKey() }, body: JSON.stringify({ id: c.id }) }).catch(() => null);
        if (!res || (!res.ok && res.status !== 404)) { toast('Could not unclaim'); return; }   // (404: already unclaimed)
        claimsGen++; claims = (claims || list).filter((x) => x.id !== c.id); claimsAt = Date.now(); mine = null;
        draw(claims);
      });
      rows.append(r);
    }
    box.append(h, rows);
  };
  if (claims) draw(claims);   // (what we have, at once: refreshed below when it's older than 30 s)
  if (claims && Date.now() - claimsAt < 30000) return;
  const gen = claimsGen;
  fetch('api/claims', { headers: { 'X-Munin-Token': adminKey() }, cache: 'no-store' })
    .then((r) => (r.ok ? r.json() : null)).then((list) => { if (gen !== claimsGen) { draw(claims); return; } claims = list || []; claimsAt = Date.now(); draw(claims); }).catch(() => {});
}
