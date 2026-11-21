---
id: doc-1
title: 'Cartography table pins: how the game reads and writes them'
type: specification
created_date: '2026-11-21T07:52:00Z'
updated_date: '2026-11-21T07:53:00Z'
---
How Valheim's cartography tables (`MapTable`) store, read and write map pins, worked out for a feature that would put Munin's web pins on the tables. Source: the decompiled game code (`assembly_valheim`, Valheim 1.0.21), read on 2026-11-21. Each point says whether it was read directly in the code, inferred from the code, or derived from other points. Nothing here has been tested in game yet.

## Storage (read in the code)

- Each table keeps its own map in its own ZDO, `ZDOVars.s_data`. `MapTable.cs` (131 lines) reads and writes only that. No global key, guild or world-level store is involved, and nothing else in the game calls `Minimap.GetSharedMapData` / `AddSharedMapData` or touches a table's `s_data`. **Tables are not synced with each other.** They only look synced because players carry data from one table to the next.
- Format: compressed with `Utils.Compress`. Inside:
  - int version (3);
  - int count of explored-grid cells, then one bool per cell;
  - int pin count, then for each pin: long owner, string name, Vector3 pos, int type, bool checked, string author.
- `ReadExploredArray` (`Minimap.cs:2724`) fails the whole read if the grid size doesn't match the player's `m_explored.Length`. **A writer must keep or carry over a valid explored grid, not just the pins.** Munin already parses this format (`src/World/Markers.cs` `ReadTablePins`, `src/World/Fog.cs` `MergeMapTable`).

## Who a pin belongs to (read in the code)

- A table pin has an **owner**: the player ID of the character who wrote it. It's the same per-character number building pieces carry as their creator, which Munin turns into "Built by".
- It also has an **author**: the writer's platform ID (for example a Steam ID). The author is set only when it was empty.
- On a player's own map, their own pins have owner 0. Pins they got from a table keep the original player's ID.
- Ownership is per character, not per account. To a player's second character, the first character's pins are someone else's.
- **Open question:** what the game shows from the author field (a name on hover, or nothing). Munin's reader skips that field today.

## Read (`MapTable.OnRead`, `MapTable.cs:54-80`; `Minimap.AddSharedMapData`, `Minimap.cs:2745-2836`) (read in the code)

1. Every pin on the player's map whose owner is neither 0 nor the local player (other players' pins) is marked for deletion.
2. Each table pin is matched to the player's map **by position only**: `HavePinInRange(pos, 1f)` (`Minimap.cs:2228`) looks for any saved pin within 1 m in XZ, whatever its owner, name or type.
   - **Found:** the closest pin within 1 m is unmarked, and nothing is added. Name, type and checked state are not updated.
   - **Not found, and the owner isn't the local player:** the pin is added, keeping its owner and author.
   - **The owner is the local player:** the pin is skipped. The game assumes the player has it, or deleted it on purpose.
3. Other players' pins still marked are removed. **The last table read decides which of other players' pins a player has.**

Read doesn't check ward (PrivateArea) access; write does.

## Write (`MapTable.OnWrite`, `MapTable.cs:82-105`; `GetSharedMapData`, `Minimap.cs:2676-2721`) (read in the code)

1. A silent read of the same table first (all of the above).
2. The explored grid is OR-merged: the player's explored and explored-by-others grids, and the table's old grid.
3. Pins are **not** merged. The table's new pin list is every saved pin on the writer's map, except Death pins. Own pins (owner 0) go in under the writer's player ID, with the writer's platform ID as author if that was empty.
4. Sent with `m_nview.InvokeRPC("MapData", pkg)` to the ZDO's owner. `RPC_MapData` (`MapTable.cs:107-114`) sets `s_data` only on the owner.

## What follows (derived from the points above)

- **A player's own pins travel:** they go into every table that player writes to.
- **Other players' pins don't spread between tables through a writer.** The silent read leaves the writer with exactly this table's other-player pins, so the write puts back what the table already had. A player's pins get into a table only when that player writes there. This needs checking in game with two characters.
- **Deleting someone else's pin doesn't last.** It comes back on the next read of any table that has it, and the silent read before a write means a player can't remove someone else's pin from a table. Only the owner can: delete it, then write at each table.
- **A table pin under your own ID that you don't have never reaches you** (you're skipped on read). Other players get it as yours, and your next write at that table removes it.

## For Munin's web pins (derived)

- **Every table, not one.** A web pin on only some tables appears when a player reads one of them and disappears when they read another.
- **One fixed owner ID that no character has,** for every web pin, including ones placed by a claimed player. Under the claimed player's real ID the pin would never reach them, and their next write would remove it. Who placed a pin has to go in its name (short) or the author field (if the game shows it).
- **Merge into each table's existing data:** keep its explored grid and its pins, and add or remove only Munin's own.
- **Never edit a pin in place.** A pin within 1 m of any existing pin is never added, and an existing pin's name and type are never updated. A changed web pin has to be a new pin, more than 1 m away or after the old one has been removed everywhere.
- **Removal reaches players:** take the pin off every table, and each player loses it on their next read of any table. In game, a player can't remove a web pin for good, the same as for any pin they don't own.
- **Not live:** a pin reaches a player only when they next read a table. Live in-game marks stay what the web ping already does.
- Munin's own map lists table pins, so it has to recognise its own pins by the fixed owner ID and not show them twice.

## Writing to a table someone else controls (inferred from `ZDO.cs` and `ZDOMan.cs`, not tested)

- `ZDO.Set(hash, byte[])` (`ZDO.cs:454`) bumps the data revision (`IncreaseDataRevision`, `ZDO.cs:578`).
- `ZDOMan.RPC_ZDOData` (about lines 1165-1190) accepts an incoming copy only when its revision is higher than the local one, and then replaces all of the ZDO's data.
- A server write normally reaches the owner and sticks. But if the owner changes the same ZDO at the same time (a player writing the table, or damage changing its health), both sides reject each other's copy, and the next change wins outright.
- Safer: send the new data through the table's own RPC to its owner, so the owner sets it: `ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), zdo.m_uid, "MapData", new ZPackage(bytes))`. If nobody owns the table, the server takes ownership (`zdo.SetOwner(<server session id>)`) and sets it directly.
