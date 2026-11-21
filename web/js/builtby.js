// patches: TASK-17. Who built a portal or a base, in its popup (fork only, see PATCHES.md).
//
// The server sends `by` on those markers: the builder's character name from the game's own record
// (Live/Stats.cs NameOfPlayerId), only for a player the map has seen and not hidden. Upstream uses it
// on the player card only.
import { escape } from './layers/markers.js';

// a marker popup's extra lines (layers/markers.js popupHtml)
export function builtBy(m, extra) {
  if ((m.cat !== 'portal' && m.cat !== 'base') || !m.by) return extra;
  const line = `<small>Built by ${escape(m.by)}</small>`;
  return extra ? `${extra}<br>${line}` : line;
}
