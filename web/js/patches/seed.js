// patches: TASK-15. The world's seed as the last line of the stats panel's Server details (fork
// only, see PATCHES.md). The server sends it in the config only when the admin turned show_seed on.
import { escape } from '../layers/markers.js';

export function seedLine(section, config) {
  const seed = config && config.seed;
  const dl = section && section.querySelector('dl.kv');
  if (!seed || !dl) return;
  dl.insertAdjacentHTML('beforeend', `<dt>Seed</dt><dd><code>${escape(seed)}</code></dd>`);
}
