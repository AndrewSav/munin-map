// patches: the fork's Settings group in the left bar, between "3D map" and "Map key" (fork
// only, see PATCHES.md). Each fork feature registers its rows here; ui.js calls buildSettings
// from one marked line.

const makers = [];

// make(app) -> a row element, or null when the feature has nothing to offer on this server
export function addSetting(make) { makers.push(make); }

export function buildSettings(app, card) {
  const c = card('Settings');
  for (const make of makers) {
    try { const r = make(app); if (r) c.append(r); } catch (e) { console.warn('settings', e); }
  }
  return c;
}

// A row in the left bar's style (ui.js .lrow), with a switch. Not ui.js's row(): that one
// remembers the switch and replays it on load, which each setting here does its own way.
export function settingRow(icon, label, desc, checked, onToggle) {
  const r = document.createElement('div');
  r.className = 'lrow';
  r.innerHTML = `<span class="lico">${icon}</span><span class="ltext"><b>${label}</b>${desc ? `<small>${desc}</small>` : ''}</span><label class="switch"><input type="checkbox"${checked ? ' checked' : ''}><i></i></label>`;
  r.querySelector('input').addEventListener('change', (e) => onToggle(e.target.checked, e.target));
  return r;
}

// drawn like ui.js's SVG() icons
export const settingIcon = (d) => `<svg viewBox="0 0 24 24" style="fill:none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${d}</svg>`;
