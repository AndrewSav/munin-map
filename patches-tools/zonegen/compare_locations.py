"""Compare a location plan written by `zonegen run --locations FILE` with the plan stored in a world
save (TASK-4.2 accuracy check for the seed-only input).

Usage: python -I compare_locations.py LOCATIONS.csv WORLD_DIR [--tol METRES]

WORLD_DIR is a world folder of the current format; its newest complete save's .db2 holds the plan
(ZoneSystem.Save: generated zones, location version, global keys, locations-generated flag, then
per location instance: prefab hash, position, placed). A location matches when the other plan has
the same prefab within --tol metres (default 0.05).
"""
import csv
import glob
import gzip
import io
import math
import os
import struct
import sys
from collections import defaultdict


def stable_hash(s):
    """Valheim's string.GetStableHashCode (32-bit wrap-around)."""
    def i32(v):
        v &= 0xFFFFFFFF
        return v - (1 << 32) if v & 0x80000000 else v
    a = b = 5381
    i = 0
    while i < len(s) and s[i] != "\0":
        a = i32(((a << 5) + a) ^ ord(s[i]))
        if i == len(s) - 1 or s[i + 1] == "\0":
            break
        b = i32(((b << 5) + b) ^ ord(s[i + 1]))
        i += 2
    return i32(a + b * 1566083941)


def read_string(f):
    n, shift = 0, 0
    while True:
        byte = f.read(1)[0]
        n |= (byte & 0x7F) << shift
        if not byte & 0x80:
            break
        shift += 7
    return f.read(n).decode("utf-8")


def save_plan(world_dir):
    oks = [int(os.path.basename(p).split(".")[1]) for p in glob.glob(os.path.join(world_dir, "_main.*.ok"))]
    d = open(os.path.join(world_dir, f"_main.{max(oks)}.db2"), "rb").read()
    n = struct.unpack_from("<i", d, 12)[0]
    f = io.BytesIO(gzip.decompress(d[16:16 + n]))
    zones = struct.unpack("<i", f.read(4))[0]
    f.read(4 * zones)
    struct.unpack("<i", f.read(4))  # location version
    keys = struct.unpack("<i", f.read(4))[0]
    for _ in range(keys):
        read_string(f)
    f.read(1)  # locations generated
    count = struct.unpack("<i", f.read(4))[0]
    plan = []
    for _ in range(count):
        h, x, y, z, placed = struct.unpack("<ifff?", f.read(17))
        plan.append((h, x, z, placed))
    return plan


def main():
    args = sys.argv[1:]
    tol = 0.05
    if "--tol" in args:
        i = args.index("--tol")
        tol = float(args[i + 1])
        del args[i:i + 2]
    if len(args) != 2:
        print(__doc__)
        return 2
    run = []
    names = {}
    with open(args[0], newline="") as fh:
        for r in csv.DictReader(fh):
            h = stable_hash(r["prefab"])
            names[h] = r["prefab"]
            run.append((h, float(r["x"]), float(r["z"])))
    ref = save_plan(args[1])
    by = defaultdict(list)
    for r in ref:
        by[r[0]].append(r)
    used = set()
    stats = defaultdict(lambda: [0, 0, 0])
    for r in ref:
        stats[r[0]][2] += 1
    for h, x, z in run:
        stats[h][1] += 1
        best, bd = None, tol
        for c in by[h]:
            if id(c) in used:
                continue
            d = math.hypot(c[1] - x, c[2] - z)
            if d <= bd:
                best, bd = c, d
        if best is not None:
            used.add(id(best))
            stats[h][0] += 1
    tm = sum(s[0] for s in stats.values())
    tr = sum(s[1] for s in stats.values())
    tf = sum(s[2] for s in stats.values())
    diff = sorted(((max(s[1], s[2]) - s[0], names.get(h, str(h)), s) for h, s in stats.items()), reverse=True)
    print(f"locations: {tm} matched, {tr} in the run's plan, {tf} in the save's plan: {100.0 * tm / max(tr, tf, 1):.2f}%")
    print("most different types (matched / run / save):")
    for miss, name, s in diff[:12]:
        if miss == 0:
            break
        print(f"  {name:32} {s[0]:6} {s[1]:6} {s[2]:6}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
