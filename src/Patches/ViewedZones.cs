// patches: TASK-4 (TASK-4.6). Which zones a browser is looking at (fork only, see PATCHES.md).
//
// Every tile a browser asks for passes through TileStore.Get, which calls Tile (one marked line). For a
// close zoom (a tile at most MaxSpan metres across) each zone under the tile is passed to whoever listens:
// in-place prediction works those zones out first (InPlaceOre); a server extract could do the same
// (DRAFT-9). A zoomed-out view promotes nothing, so looking at the whole map doesn't reorder the world.
using System;
using MuninMap.Tiles;

namespace MuninMap.Patches
{
    internal static class ViewedZones
    {
        public const float MaxSpan = 1024f;   // zoom 5 and closer

        // HTTP threads: (zone x, zone y) for each zone under a viewed tile; listeners must be quick
        public static event Action<int, int> Zone;

        // HTTP threads, from TileStore.Get: cheap, and nothing at all when no one listens
        public static void Tile(int zoom, int x, int y)
        {
            var listeners = Zone;
            if (listeners == null) return;
            try
            {
                TileMath.TileBounds(zoom, x, y, out float minX, out float minZ, out float maxX, out float maxZ);
                if (maxX - minX > MaxSpan) return;
                int zx0 = TileMath.ZoneCoord(minX), zx1 = TileMath.ZoneCoord(maxX);
                int zz0 = TileMath.ZoneCoord(minZ), zz1 = TileMath.ZoneCoord(maxZ);
                for (int zz = zz0; zz <= zz1; zz++)
                    for (int zx = zx0; zx <= zx1; zx++)
                        listeners(zx, zz);
            }
            catch { }
        }
    }
}
