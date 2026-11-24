// patches: TASK-2. The in-game day and time as a chip at the left end of the bottom-left HUD row
// (fork only, see PATCHES.md). Shown only when the viewer turns on Clock in the fork's Settings
// group (off by default: the game itself shows no clock, and some find one breaks immersion);
// the choice is kept in this browser.
//
// The time comes with each players update (Patches/WorldClock.cs: "clock" in the players frame).
// Between updates the clock runs on from the last one at the rate the game's time moved between
// the last two: 1 normally (one game second a second), 0 while the server has nobody on (the game
// pauses its clock then), faster while everyone sleeps and the game fast-forwards to morning.
// Every update puts it right again.

import { on, state } from '../net.js';
import { addSetting, settingRow, settingIcon } from './patches-settings.js';

const KEY = 'munin-fork-clock';   // this fork's own key, apart from upstream's remembered switches
let enabled = false;
try { enabled = localStorage.getItem(KEY) === '1'; } catch { /* not kept: off */ }

let chip = null, last = null, lastAt = 0, rate = 0, connected = !!state.connected, timer = 0;

// EnvMan.RescaleDayFraction: the daylight part of the day (0.15 to 0.85 of it) runs from 0.25 to 0.75
const rescale = (f) => (f >= 0.15 && f <= 0.85 ? 0.25 + (f - 0.15) / 0.7 * 0.5 : f < 0.5 ? f / 0.15 * 0.25 : 0.75 + (f - 0.85) / 0.15 * 0.25);

function ensureChip() {
  if (chip) return chip;
  const hud = document.querySelector('.hud');
  if (!hud) return null;
  chip = document.createElement('div');
  chip.className = 'fork-clock';
  chip.title = 'The time in the game world';
  chip.hidden = true;
  hud.insertBefore(chip, hud.firstChild);   // the left end of the row
  return chip;
}

function render() {
  const c = ensureChip();
  if (!c) return;
  if (!enabled || !connected || !last) { c.hidden = true; c.textContent = ''; return; }
  const t = last.worldTime + rate * Math.min(Date.now() - lastAt, 3000) / 1000;   // on at the game's own pace, never far past an update
  const len = last.dayLength > 0 ? last.dayLength : 1800;
  const day = Math.floor(t / len);
  const frac = rescale((((t % len) + len) % len) / len);
  const mins = Math.floor(frac * 24 * 60) % (24 * 60);
  const hh = String(Math.floor(mins / 60)).padStart(2, '0'), mm = String(mins % 60).padStart(2, '0');
  const night = frac <= 0.25 || frac >= 0.75;
  c.textContent = `${night ? '☾ ' : ''}Day ${day} · ${hh}:${mm}`;
  c.hidden = false;
}

function tick() {
  clearInterval(timer);
  if (enabled) timer = setInterval(render, 1000);
  render();
}

on('players', (f) => {
  const k = f && f.data && f.data.clock;
  if (k && typeof k.worldTime === 'number') {
    const now = Date.now();
    // game seconds per second since the last update: 0 when paused, 1 normally, more while sleeping
    rate = last && now > lastAt ? Math.max(0, Math.min(200, (k.worldTime - last.worldTime) / ((now - lastAt) / 1000))) : 0;
    last = k; lastAt = now;
  } else { last = null; rate = 0; }   // an older plugin sends none
  render();
});
on('connection', (up) => { connected = !!up; if (!up) last = null; render(); });

addSetting(() => settingRow(
  settingIcon('<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>'),
  'Clock', 'The time of day in the game', enabled, (on_) => {
    enabled = on_;
    try { localStorage.setItem(KEY, on_ ? '1' : '0'); } catch { /* this visit only */ }
    tick();
  }));

tick();
