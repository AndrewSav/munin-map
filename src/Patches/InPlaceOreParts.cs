// patches: TASK-4 (TASK-4.6). Parts of the in-place ore prediction that run off the game thread (fork
// only, see PATCHES.md): a copy of Unity's random number generator, a zone's terrain worked out per
// vertex with HeightmapBuilder's own formula, and blocking by ray tests against copies of the
// vegetation prefabs' colliders. InPlaceOre checks the first two against the game's own code at start.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuninMap.Patches
{
    // Lowers the calling thread's CPU priority. On Linux, Mono ignores Thread.Priority, so the thread
    // sets its own nice value (setpriority on its thread id: on Linux that is one thread, not the
    // process); elsewhere Thread.Priority = Lowest does it.
    internal static class ThreadNice
    {
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] private static extern int setpriority(int which, int who, int prio);
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)] private static extern long syscall(long number);
        private const long SYS_gettid_x64 = 186;

        // the result, for the log ("nice 19", "failed (errno n)", or "thread priority")
        public static string Lower()
        {
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Lowest;
            if (Environment.OSVersion.Platform != PlatformID.Unix) return "thread priority lowest";
            try
            {
                int tid = (int)syscall(SYS_gettid_x64);
                if (tid <= 0) return "nice not set (no thread id)";
                return setpriority(0 /* PRIO_PROCESS: on Linux, a thread id names that thread */, tid, 19) == 0
                    ? "nice 19" : "nice not set (errno " + System.Runtime.InteropServices.Marshal.GetLastWin32Error() + ")";
            }
            catch (Exception e) { return "nice not set (" + e.GetType().Name + ")"; }
        }
    }

    // Unity's UnityEngine.Random (Xorshift128), which Unity allows on its main thread only. InPlaceOre
    // checks every function used here against the real one before it trusts it (SelfTest).
    internal sealed class URandom
    {
        private uint s0, s1, s2, s3;

        // which of the possible formulas match Unity's (set by InPlaceOre.CheckRandom)
        public static int RangeFloatForm = 0;
        public static int CircleForm = 0;

        public void InitState(int seed)
        {
            s0 = (uint)seed;
            s1 = s0 * 1812433253u + 1u;
            s2 = s1 * 1812433253u + 1u;
            s3 = s2 * 1812433253u + 1u;
        }

        public uint NextUInt()
        {
            uint t = s0 ^ (s0 << 11);
            s0 = s1; s1 = s2; s2 = s3;
            s3 = s3 ^ (s3 >> 19) ^ t ^ (t >> 8);
            return s3;
        }

        public float value => (NextUInt() & 0x7FFFFFu) * (1f / 8388607f);

        // Unity's native code rounds every float operation; Mono may keep intermediates at higher
        // precision, so each step is rounded explicitly (the (float) casts are conv.r4, not no-ops)
        public float Range(float min, float max)
        {
            float t = value;
            switch (RangeFloatForm)
            {
                case 0: return (float)((float)(t * max) + (float)((float)(1f - t) * min));
                case 1: return (float)(min + (float)((float)(max - min) * t));
                default: return (float)((float)(t * min) + (float)((float)(1f - t) * max));
            }
        }

        public int Range(int min, int max)
        {
            if (min < max) return min + (int)(NextUInt() % (uint)(max - min));
            if (min > max) return min - (int)(NextUInt() % (uint)(min - max));
            return min;
        }

        public Vector2 insideUnitCircle
        {
            get
            {
                switch (CircleForm)
                {
                    case 0: { float a = Range(0f, Mathf.PI * 2f); float r = Mathf.Sqrt(Range(0f, 1f)); return new Vector2((float)(r * Mathf.Cos(a)), (float)(r * Mathf.Sin(a))); }
                    case 1: { float r = Mathf.Sqrt(Range(0f, 1f)); float a = Range(0f, Mathf.PI * 2f); return new Vector2((float)(r * Mathf.Cos(a)), (float)(r * Mathf.Sin(a))); }
                    case 2: { float a = (float)((float)(value * Mathf.PI) * 2f); float r = Mathf.Sqrt(value); return new Vector2((float)(r * Mathf.Cos(a)), (float)(r * Mathf.Sin(a))); }
                    default: { float r = Mathf.Sqrt(value); float a = (float)((float)(value * Mathf.PI) * 2f); return new Vector2((float)(r * Mathf.Cos(a)), (float)(r * Mathf.Sin(a))); }
                }
            }
        }
    }

    // One zone's terrain, as Heightmap's collision mesh has it, each vertex worked out when first read,
    // with the same arithmetic as HeightmapBuilder.Build (InPlaceOre.CheckTerrain compares them).
    internal sealed class ZoneTerrain
    {
        public Vector2s zone;
        public Vector3 center;
        public BiomeSector[] corners;        // Build's m_cornerBiomes
        public bool active;                  // false: no ore biome at a corner (nothing to do)
        private Heightmap.Biome b0, b1, b2, b3;
        private bool same;
        private Vector3 origin;
        private float[] heights, masks;
        private readonly int width; private readonly float scale;
        private WorldGenerator wg;

        public ZoneTerrain(Vector2s z, int width, float scale)
        {
            zone = z; this.width = width; this.scale = scale;
            center = ZoneSystem.GetZonePos(z);
        }

        // corners only (cheap): enough to tell whether the zone needs anything
        public void Corners(WorldGenerator wg, Heightmap.Biome need)
        {
            this.wg = wg;
            origin = center + new Vector3((float)width * scale * -0.5f, 0f, (float)width * scale * -0.5f);
            double w = (double)width * (double)scale;
            corners = new BiomeSector[4];
            corners[0] = wg.GetBiomeSector(origin.x, origin.z);
            corners[1] = wg.GetBiomeSector((float)((double)origin.x + w), origin.z);
            corners[2] = wg.GetBiomeSector(origin.x, (float)((double)origin.z + w));
            corners[3] = wg.GetBiomeSector((float)((double)origin.x + w), (float)((double)origin.z + w));
            b0 = wg.GetBiome(origin.x, origin.z);
            b1 = wg.GetBiome((float)((double)origin.x + w), origin.z);
            b2 = wg.GetBiome(origin.x, (float)((double)origin.z + w));
            b3 = wg.GetBiome((float)((double)origin.x + w), (float)((double)origin.z + w));
            same = b2 == b0 && b1 == b0 && b3 == b0;
            active = ((corners[0].Biome | corners[1].Biome | corners[2].Biome | corners[3].Biome) & need) != 0;
            if (active)
            {
                int n = (width + 1) * (width + 1);
                heights = new float[n]; masks = new float[n];
                for (int i = 0; i < n; i++) heights[i] = float.NaN;
            }
        }

        public int Touched { get { int c = 0; if (heights != null) foreach (var h in heights) if (!float.IsNaN(h)) c++; return c; } }

        public float H(int x, int y) { int i = y * (width + 1) + x; if (float.IsNaN(heights[i])) Build(x, y, i); return heights[i]; }
        public float MaskA(int x, int y) { int i = y * (width + 1) + x; if (float.IsNaN(heights[i])) Build(x, y, i); return masks[i]; }

        // HeightmapBuilder.Build's loop body for vertex (l, k)
        private void Build(int l, int k, int i)
        {
            float wy = (float)((double)origin.z + (double)k * (double)scale);
            float t = DUtils.SmoothStep(0f, 1f, (float)((double)k / (double)width));
            float wx = (float)((double)origin.x + (double)l * (double)scale);
            float t2 = DUtils.SmoothStep(0f, 1f, (float)((double)l / (double)width));
            float h; Color mask;
            if (same) h = wg.GetBiomeHeight(b0, wx, wy, out mask);
            else
            {
                float h0 = wg.GetBiomeHeight(b0, wx, wy, out Color m0);
                float h1 = wg.GetBiomeHeight(b1, wx, wy, out Color m1);
                float h2 = wg.GetBiomeHeight(b2, wx, wy, out Color m2);
                float h3 = wg.GetBiomeHeight(b3, wx, wy, out Color m3);
                float a = DUtils.Lerp(h0, h1, t2);
                float b = DUtils.Lerp(h2, h3, t2);
                h = DUtils.Lerp(a, b, t);
                Color ma = Color.Lerp(m0, m1, t2);
                Color mb = Color.Lerp(m2, m3, t2);
                mask = Color.Lerp(ma, mb, t);
            }
            masks[i] = mask.a;
            heights[i] = h;
        }
    }

    // A placed object's colliders, for the block check (ZoneSystem.IsBlocked: a ray straight down from
    // 2000 m above, 10 km long, against the Default, static_solid, Default_small and piece layers) and
    // static-solid snapping (GetStaticSolidHeight). Shapes are read from the prefab once (game thread).
    internal sealed class ShapeDef
    {
        public int kind;                 // 0 box, 1 sphere, 2 capsule, 3 mesh
        public int layer;
        public bool trigger, rigidbody;
        public Matrix4x4 local;          // the collider's object relative to the prefab's root (scale included)
        public Vector3 center, size;     // box; sphere and capsule centre
        public float radius, height;
        public int direction;
        public Vector3[] verts; public int[] tris;
        public bool convex;              // a convex MeshCollider: PhysX tests its convex hull, not its triangles
        public Bounds bounds;            // in the collider object's local space
    }

    internal sealed class ShapeSet
    {
        public ShapeDef[] shapes;
        public Vector3 prefabScale;
    }

    // the objects placed so far in one zone
    internal sealed class Blockers
    {
        private sealed class Placed
        {
            public ShapeDef def;
            public Matrix4x4 m;          // world from the collider's local space
            public float minX, maxX, minZ, maxZ;
            public Vector3[] world;      // mesh vertices in world space, made when first needed
            public Vector2[] hull;       // convex mesh: the 2D hull of those vertices seen from above
            public float top;            // convex mesh: the highest vertex
        }

        private readonly List<Placed> placed = new List<Placed>();
        public static bool hitTriggers = true, hitBackfaces = false;

        public int Count => placed.Count;
        public void Clear() => placed.Clear();

        public void Add(ShapeSet set, Vector3 p, Quaternion rot, Vector3 scale)
        {
            var root = Matrix4x4.TRS(p, rot, scale);
            foreach (var d in set.shapes) AddShape(d, root * d.local);
        }

        // one collider whose local space maps to the world by m
        public void AddShape(ShapeDef d, Matrix4x4 m)
        {
            {
                var pl = new Placed { def = d, m = m };
                // world bounds in x and z from the local box's corners (spheres and capsules: their
                // radius grows with the largest scale, so pad by it)
                var b = d.bounds;
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int c = 0; c < 8; c++)
                {
                    var v = m.MultiplyPoint3x4(new Vector3((c & 1) != 0 ? b.max.x : b.min.x, (c & 2) != 0 ? b.max.y : b.min.y, (c & 4) != 0 ? b.max.z : b.min.z));
                    if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x; if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
                }
                float pad = d.kind == 1 || d.kind == 2 ? MaxScale(m) * d.radius : 0f;
                pl.minX = minX - pad; pl.maxX = maxX + pad; pl.minZ = minZ - pad; pl.maxZ = maxZ + pad;
                placed.Add(pl);
            }
        }

        private static float MaxScale(Matrix4x4 m) =>
            Mathf.Max(((Vector3)m.GetColumn(0)).magnitude, Mathf.Max(((Vector3)m.GetColumn(1)).magnitude, ((Vector3)m.GetColumn(2)).magnitude));

        // the highest hit of a vertical line through (x, z) between yTop and yBottom on the given layers;
        // false when nothing is hit. `rb` says whether the hit collider has a rigidbody.
        public bool Highest(float x, float z, float yTop, float yBottom, int mask, out float y, out Vector3 normal, out bool rb)
        {
            y = float.MinValue; normal = Vector3.up; rb = false;
            bool any = false;
            foreach (var pl in placed)
            {
                var d = pl.def;
                if (((1 << d.layer) & mask) == 0) continue;
                if (d.trigger && !hitTriggers) continue;
                if (x < pl.minX || x > pl.maxX || z < pl.minZ || z > pl.maxZ) continue;
                if (Hit(pl, x, z, out float hy, out Vector3 hn) && hy <= yTop && hy >= yBottom && hy > y)
                { y = hy; normal = hn; rb = d.rigidbody; any = true; }
            }
            return any;
        }

        private bool Hit(Placed pl, float x, float z, out float y, out Vector3 n)
        {
            var d = pl.def;
            y = 0f; n = Vector3.up;
            switch (d.kind)
            {
                case 0: return HitBox(pl, x, z, out y, out n);
                case 1:
                    {
                        Vector3 c = pl.m.MultiplyPoint3x4(d.center);
                        float r = d.radius * MaxScale(pl.m);
                        float dx = x - c.x, dz = z - c.z, q = r * r - dx * dx - dz * dz;
                        if (q < 0f) return false;
                        y = c.y + Mathf.Sqrt(q);
                        n = new Vector3(dx, y - c.y, dz).normalized;
                        return true;
                    }
                case 2: return HitCapsule(pl, x, z, out y, out n);
                default: return HitMesh(pl, x, z, out y, out n);
            }
        }

        // the line in the box's local space: slab test, entry from above
        private static bool HitBox(Placed pl, float x, float z, out float y, out Vector3 n)
        {
            var d = pl.def;
            y = 0f; n = Vector3.up;
            var inv = pl.m.inverse;
            Vector3 o = inv.MultiplyPoint3x4(new Vector3(x, 0f, z));
            Vector3 dir = inv.MultiplyVector(Vector3.down);
            Vector3 lo = d.center - d.size * 0.5f, hi = d.center + d.size * 0.5f;
            float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity; int axis = -1; float sign = 0f;
            for (int a = 0; a < 3; a++)
            {
                if (Mathf.Abs(dir[a]) < 1e-9f) { if (o[a] < lo[a] || o[a] > hi[a]) return false; continue; }
                float t1 = (lo[a] - o[a]) / dir[a], t2 = (hi[a] - o[a]) / dir[a];
                float s = -1f;
                if (t1 > t2) { var tt = t1; t1 = t2; t2 = tt; s = 1f; }
                if (t1 > tmin) { tmin = t1; axis = a; sign = s; }
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) return false;
            }
            // t is in world metres down from y = 0 (the matrix is affine): the entry point
            y = -tmin;
            if (axis >= 0) { var ln = Vector3.zero; ln[axis] = sign; n = pl.m.inverse.transpose.MultiplyVector(ln).normalized; if (n.y < 0f) n = -n; }
            return true;
        }

        // Unity: radius by the larger of the two cross-axis scales, height by the axis scale
        private static bool HitCapsule(Placed pl, float x, float z, out float y, out Vector3 n)
        {
            var d = pl.def;
            y = 0f; n = Vector3.up;
            Vector3 axisV = pl.m.GetColumn(d.direction);
            float axisScale = axisV.magnitude;
            float rs = 0f;
            for (int a = 0; a < 3; a++) if (a != d.direction) rs = Mathf.Max(rs, ((Vector3)pl.m.GetColumn(a)).magnitude);
            float r = d.radius * rs;
            float half = Mathf.Max(d.height * axisScale * 0.5f - r, 0f);
            Vector3 c = pl.m.MultiplyPoint3x4(d.center);
            Vector3 u = axisScale > 0f ? axisV / axisScale : Vector3.up;
            Vector3 a0 = c - u * half, a1 = c + u * half;
            // the highest point of the capsule over (x, z): the best sphere along the segment
            bool hit = false; float best = float.MinValue;
            const int steps = 32;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 s = Vector3.Lerp(a0, a1, i / (float)steps);
                float dx = x - s.x, dz = z - s.z, q = r * r - dx * dx - dz * dz;
                if (q < 0f) continue;
                float top = s.y + Mathf.Sqrt(q);
                if (top > best) { best = top; hit = true; n = new Vector3(dx, top - s.y, dz).normalized; }
            }
            y = best;
            return hit;
        }

        private static bool InTriangle2D(Vector3 v0, Vector3 v1, Vector3 v2, float x, float z)
        {
            float d0 = (v1.x - v0.x) * (z - v0.z) - (v1.z - v0.z) * (x - v0.x);
            float d1 = (v2.x - v1.x) * (z - v1.z) - (v2.z - v1.z) * (x - v1.x);
            float d2 = (v0.x - v2.x) * (z - v2.z) - (v0.z - v2.z) * (x - v2.x);
            bool neg = d0 < 0f || d1 < 0f || d2 < 0f, pos = d0 > 0f || d1 > 0f || d2 > 0f;
            return !(neg && pos);
        }

        // Andrew's monotone chain over (x, z), counter-clockwise
        private static Vector2[] Hull2D(Vector3[] pts)
        {
            var p = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) p[i] = new Vector2(pts[i].x, pts[i].z);
            Array.Sort(p, (a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            if (p.Length < 3) return p;
            var h = new Vector2[p.Length * 2]; int k = 0;
            for (int i = 0; i < p.Length; i++) { while (k >= 2 && Cross(h[k - 2], h[k - 1], p[i]) <= 0f) k--; h[k++] = p[i]; }
            for (int i = p.Length - 2, t = k + 1; i >= 0; i--) { while (k >= t && Cross(h[k - 2], h[k - 1], p[i]) <= 0f) k--; h[k++] = p[i]; }
            Array.Resize(ref h, Math.Max(k - 1, 0));
            return h;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static bool InsideConvex(Vector2[] h, float x, float z)
        {
            if (h.Length < 3) return false;
            var q = new Vector2(x, z);
            for (int i = 0; i < h.Length; i++) if (Cross(h[i], h[(i + 1) % h.Length], q) < 0f) return false;
            return true;
        }

        // the vertical line against the mesh's triangles in world space; front faces only unless the
        // physics settings say otherwise (a ray downward hits a triangle facing up)
        private static bool HitMesh(Placed pl, float x, float z, out float y, out Vector3 n)
        {
            var d = pl.def;
            y = float.MinValue; n = Vector3.up;
            if (pl.world == null)
            {
                pl.world = new Vector3[d.verts.Length];
                for (int i = 0; i < d.verts.Length; i++) pl.world[i] = pl.m.MultiplyPoint3x4(d.verts[i]);
            }
            var w = pl.world; var tr = d.tris;
            bool flip = pl.m.determinant < 0f;
            bool hit = false;
            if (d.convex)
            {
                // a vertical line meets a convex solid exactly when (x, z) is inside the solid's outline
                // from above; the height is the mesh's top there, or its highest vertex
                if (pl.hull == null) { pl.hull = Hull2D(w); pl.top = float.MinValue; foreach (var v in w) if (v.y > pl.top) pl.top = v.y; }
                if (!InsideConvex(pl.hull, x, z)) return false;
                y = pl.top;
                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    Vector3 v0 = w[tr[i]], v1 = w[tr[i + 1]], v2 = w[tr[i + 2]];
                    if (!InTriangle2D(v0, v1, v2, x, z)) continue;
                    Vector3 nn = Vector3.Cross(v1 - v0, v2 - v0);
                    if (Mathf.Abs(nn.y) < 1e-12f) continue;
                    float hy = v0.y - (nn.x * (x - v0.x) + nn.z * (z - v0.z)) / nn.y;
                    if (!hit || hy > y) { y = hy; n = (nn.y < 0f ? -nn : nn).normalized; hit = true; }
                }
                return true;
            }
            for (int i = 0; i + 2 < tr.Length; i += 3)
            {
                Vector3 v0 = w[tr[i]], v1 = w[tr[i + 1]], v2 = w[tr[i + 2]];
                // 2D point in triangle (x, z)
                float d0 = (v1.x - v0.x) * (z - v0.z) - (v1.z - v0.z) * (x - v0.x);
                float d1 = (v2.x - v1.x) * (z - v1.z) - (v2.z - v1.z) * (x - v1.x);
                float d2 = (v0.x - v2.x) * (z - v2.z) - (v0.z - v2.z) * (x - v2.x);
                bool neg = d0 < 0f || d1 < 0f || d2 < 0f, pos = d0 > 0f || d1 > 0f || d2 > 0f;
                if (neg && pos) continue;
                Vector3 nn = Vector3.Cross(v1 - v0, v2 - v0);
                if (flip) nn = -nn;
                if (Mathf.Abs(nn.y) < 1e-12f) continue;
                if (nn.y < 0f && !hitBackfaces) continue;   // faces down: the ray from above meets its back
                float hy = v0.y - (nn.x * (x - v0.x) + nn.z * (z - v0.z)) / nn.y;
                if (hy > y) { y = hy; n = (nn.y < 0f ? -nn : nn).normalized; hit = true; }
            }
            return hit;
        }
    }
}
