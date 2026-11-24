// patches: TASK-4 (TASK-4.6). In-place ore prediction: a location's parts that block vegetation (fork
// only, see PATCHES.md).
//
// In Ghost mode (a dedicated server generating a zone) ZoneSystem.SpawnLocation instantiates only the
// location's networked parts before the vegetation pass, and those block it (backlog doc-2; read in the
// game's code). Which parts exist is decided by the location's RandomSpawn and
// RandomObject rolls, seeded per zone (ZoneSystem.cs:2442); where they stand also needs the location's
// facing, which the game draws from Unity's unseeded random state: for a slope-rotated location the
// terrain makes one or two facings likely, so the most likely one is used; a randomly rotated one can't
// be predicted and its parts are left out.
//
// Each location prefab that can stand in an ore biome is loaded once, asynchronously as the game does
// (SoftReference.LoadAsync), copied into a LocModel in pieces of about a millisecond a frame on the game
// thread, and released. Zones are worked out from the copies on the worker threads.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MuninMap.Patches
{
    internal sealed class LocModel
    {
        public string name;
        public int[] parent;            // node -> parent node (-1 for the root)
        public bool[] activeSelf;       // as in the prefab
        public float[] y;               // node's height with the prefab root at the origin (RandomSpawn's elevation test)

        public struct Spawn { public int node, off; public float chance; public Heightmap.Biome biome; public int minElev, maxElev; public bool netview; public int[] childNetViews; }
        public struct Choice { public int node; public int[] objects; public float[] weights; public int[][] objectNetViews; public int[] ownNetViews; public Heightmap.Biome biome; public int minElev, maxElev; public bool netview; }
        public struct Part { public int node; public Vector3 pos; public Quaternion rot; public Vector3 scale; public PartShape[] shapes; }
        public struct PartShape { public ShapeDef def; public int[] path; }   // path: the nodes between the part and the collider that must be active

        public Spawn[] spawns;
        public Choice[] choices;
        public Part[] parts;            // networked children (each instantiated with its subtree)
        public int colliders;
    }

    internal static class InPlaceOreLocations
    {
        // ---- copying a location prefab (game thread, in pieces) ----

        // the copy of `asset`, building it a piece at a time; the enumerator yields when the piece's time is
        // used up, and sets result when done
        public static IEnumerator Copy(string name, GameObject asset, int blockMask, double sliceMs, Action<LocModel> result)
        {
            var slice = System.Diagnostics.Stopwatch.StartNew();
            var nodes = new List<Transform>();
            var index = new Dictionary<Transform, int>();
            var parent = new List<int>();
            // every node, depth first (the order GetComponentsInChildren uses)
            var stack = new Stack<(Transform, int)>();
            stack.Push((asset.transform, -1));
            while (stack.Count > 0)
            {
                var (t, p) = stack.Pop();
                index[t] = nodes.Count; nodes.Add(t); parent.Add(p);
                for (int i = t.childCount - 1; i >= 0; i--) stack.Push((t.GetChild(i), index[t]));
                if (slice.Elapsed.TotalMilliseconds >= sliceMs) { yield return null; slice.Restart(); }
            }
            var root = asset.transform;
            // the prefab root at the origin, unrotated, keeping its own scale (as SpawnLocation sets it)
            var zero = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, root.localScale) * root.worldToLocalMatrix;
            var m = new LocModel { name = name, parent = parent.ToArray(), activeSelf = new bool[nodes.Count], y = new float[nodes.Count] };
            for (int i = 0; i < nodes.Count; i++)
            {
                m.activeSelf[i] = nodes[i].gameObject.activeSelf;
                m.y[i] = zero.MultiplyPoint3x4(nodes[i].position).y;
            }
            yield return null; slice.Restart();

            int Node(Component c) => c != null && index.TryGetValue(c.transform, out int k) ? k : -1;
            int[] NetViewsUnder(GameObject go, Func<ZNetView, bool> keep)
            {
                var list = new List<int>();
                foreach (var nv in go.GetComponentsInChildren<ZNetView>(true)) if (keep == null || keep(nv)) list.Add(Node(nv));
                return list.ToArray();
            }

            // RandomSpawn and RandomObject, in the order SpawnLocation rolls them
            var spawns = new List<LocModel.Spawn>();
            foreach (var rs in Utils.GetEnabledComponentsInChildren<RandomSpawn>(asset))
            {
                spawns.Add(new LocModel.Spawn
                {
                    node = Node(rs), off = rs.m_OffObject != null ? Node(rs.m_OffObject.transform) : -1, chance = rs.m_chanceToSpawn,
                    biome = rs.m_requireBiome, minElev = rs.m_minElevation, maxElev = rs.m_maxElevation, netview = rs.GetComponent<ZNetView>() != null,
                    // RandomSpawn.Prepare
                    childNetViews = NetViewsUnder(rs.gameObject, nv => Utils.IsEnabledInheirarcy(nv.gameObject, rs.gameObject)),
                });
                if (slice.Elapsed.TotalMilliseconds >= sliceMs) { yield return null; slice.Restart(); }
            }
            var choices = new List<LocModel.Choice>();
            foreach (var ro in Utils.GetEnabledComponentsInChildren<RandomObject>(asset))
            {
                var objs = new int[ro.m_objects.Count]; var weights = new float[objs.Length]; var under = new int[objs.Length][];
                for (int i = 0; i < objs.Length; i++)
                {
                    var o = ro.m_objects[i].m_object;
                    objs[i] = o != null ? Node(o.transform) : -1; weights[i] = ro.m_objects[i].m_weight;
                    under[i] = o != null ? NetViewsUnder(o, null) : new int[0];
                }
                choices.Add(new LocModel.Choice
                {
                    node = Node(ro), objects = objs, weights = weights, objectNetViews = under, ownNetViews = NetViewsUnder(ro.gameObject, null),
                    biome = ro.m_requireBiome, minElev = ro.m_minElevation, maxElev = ro.m_maxElevation, netview = ro.GetComponent<ZNetView>() != null,
                });
                if (slice.Elapsed.TotalMilliseconds >= sliceMs) { yield return null; slice.Restart(); }
            }
            m.spawns = spawns.ToArray(); m.choices = choices.ToArray();

            // the networked children SpawnLocation instantiates, with the colliders under each that the block
            // check (or static-solid snapping) sees
            var parts = new List<LocModel.Part>();
            int solid = LayerMask.GetMask("static_solid");
            foreach (var nv in Utils.GetEnabledComponentsInChildren<ZNetView>(asset))
            {
                var t = nv.transform;
                int pn = Node(nv);
                var shapes = new List<LocModel.PartShape>();
                foreach (var c in nv.GetComponentsInChildren<Collider>(true))
                {
                    if (!c.enabled || ((1 << c.gameObject.layer) & (blockMask | solid)) == 0) continue;
                    var d = InPlaceOre.ShapeOf(c, t.worldToLocalMatrix * c.transform.localToWorldMatrix);
                    if (d == null) continue;
                    // the nodes from the collider up to (not including) the part must be active
                    var path = new List<int>();
                    for (int k = Node(c); k >= 0 && k != pn; k = m.parent[k]) path.Add(k);
                    shapes.Add(new LocModel.PartShape { def = d, path = path.ToArray() });
                    m.colliders++;
                    if (slice.Elapsed.TotalMilliseconds >= sliceMs) { yield return null; slice.Restart(); }
                }
                if (shapes.Count == 0) continue;
                var w = zero * t.localToWorldMatrix;
                parts.Add(new LocModel.Part
                {
                    node = pn, pos = zero.MultiplyPoint3x4(t.position), rot = Quaternion.Inverse(root.rotation) * t.rotation,
                    scale = t.localScale, shapes = shapes.ToArray(),
                });
            }
            m.parts = parts.ToArray();
            result(m);
        }

        // ---- a zone's location parts (worker threads) ----

        // Adds the parts SpawnLocation would instantiate for `m` at `p` facing `rot`, as Ghost mode does,
        // to `blockers`. `seed` is the location's seed (world seed + zone x * 4271 + zone y * 9187).
        public static int Place(LocModel m, int seed, Vector3 p, Quaternion rot, URandom r, Blockers blockers, Func<Vector3, Heightmap.Biome> biomeAt)
        {
            var active = (bool[])m.activeSelf.Clone();
            r.InitState(seed);
            Heightmap.Biome locBiome = Heightmap.Biome.None;   // Location.m_biome, set by the first roll that needs it
            Heightmap.Biome LocBiome(Vector3 pos) { if (locBiome == Heightmap.Biome.None) locBiome = biomeAt(pos); return locBiome; }
            foreach (var s in m.spawns)
            {
                // RandomSpawn.Randomize: the roll is always drawn
                bool spawned = r.Range(0f, 100f) <= s.chance;
                float y = p.y + m.y[s.node];
                if (s.biome != Heightmap.Biome.None && (s.biome & LocBiome(p)) == 0) spawned = false;
                if (y < s.minElev || y > s.maxElev) spawned = false;
                // SetSpawned
                if (!spawned) { active[s.node] = false; foreach (int k in s.childNetViews) if (k >= 0) active[k] = false; }
                else if (!s.netview) active[s.node] = true;
                if (s.off >= 0) active[s.off] = !spawned;
            }
            foreach (var c in m.choices)
            {
                // RandomObject.GetWeightedObject: the draw comes first, always
                float total = 0f; foreach (var w in c.weights) total += w;
                float pick = r.Range(0f, total), acc = 0f; int chosen = -1;
                for (int i = 0; i < c.weights.Length; i++) { acc += c.weights[i]; if (pick <= acc) { chosen = i; break; } }
                int obj = chosen >= 0 ? c.objects[chosen] : -1;
                float y = p.y + m.y[c.node];
                if (c.biome != Heightmap.Biome.None && (c.biome & LocBiome(p)) == 0) obj = -1;
                else if (y < c.minElev || y > c.maxElev) obj = -1;
                // SetSpawned
                for (int i = 0; i < c.objects.Length; i++)
                {
                    if (c.objects[i] < 0 || c.objects[i] == obj) continue;
                    active[c.objects[i]] = false;
                    foreach (int k in c.objectNetViews[i]) if (k >= 0) active[k] = false;
                }
                if (obj < 0) { active[c.node] = false; foreach (int k in c.ownNetViews) if (k >= 0) active[k] = false; }
                else if (!c.netview) active[c.node] = true;
            }
            int added = 0;
            foreach (var part in m.parts)
            {
                if (!active[part.node]) continue;   // SpawnLocation checks activeSelf only
                var root = Matrix4x4.TRS(p + rot * part.pos, rot * part.rot, part.scale);
                foreach (var sh in part.shapes)
                {
                    bool on = true;
                    foreach (int k in sh.path) if (!active[k]) { on = false; break; }
                    if (!on) continue;
                    blockers.AddShape(sh.def, root * sh.def.local);
                    added++;
                }
            }
            return added;
        }

        // ZoneSystem.PlaceLocations' slope rotation (GetTerrainDelta over the location's exterior radius,
        // the facing from the highest sample to the lowest, rounded to 22.5 degrees). The game draws the
        // samples from Unity's unseeded random state, so this samples many draws and returns the facing
        // that comes up most often.
        public static Quaternion LikelySlopeRotation(Vector3 p, float radius, Func<Vector3, float> groundHeight, URandom r, int seed)
        {
            var counts = new Dictionary<int, int>();
            r.InitState(seed);
            int best = 0, bestCount = -1;
            for (int s = 0; s < 48; s++)
            {
                float hi = -999999f, lo = 999999f;
                Vector3 vh = p, vl = p;
                for (int i = 0; i < 10; i++)
                {
                    Vector2 v = r.insideUnitCircle * radius;
                    Vector3 q = p + new Vector3(v.x, 0f, v.y);
                    float h = groundHeight(q);
                    if (h < lo) { lo = h; vl = q; }
                    if (h > hi) { hi = h; vh = q; }
                }
                Vector3 dir = Vector3.Normalize(vl - vh);
                Vector3 fwd = new Vector3(dir.x, 0f, dir.z).normalized;
                float yaw = fwd == Vector3.zero ? 0f : Quaternion.LookRotation(fwd).eulerAngles.y;
                int step = (int)Mathf.Round(yaw / 22.5f) % 16;
                counts.TryGetValue(step, out int n); counts[step] = ++n;
                if (n > bestCount) { bestCount = n; best = step; }
            }
            return Quaternion.Euler(0f, best * 22.5f, 0f);
        }
    }
}
