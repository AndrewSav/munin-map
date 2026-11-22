"""Compare a ore-extract deposit CSV with a reference export (TASK-4.2 accuracy check).

Usage: python -I compare.py RUN.csv REFERENCE.csv [--base-save WORLD_DIR] [--tol METRES]

With --base-save (the save the run started from, current format), every zone that save had not
generated is compared, so deposits the run missed count. Without it, only zones where the run has
a deposit are compared. A deposit matches when the reference has the same prefab within --tol metres in the
same zone (default 0.05: the CSVs round to centimetres, and the same generation gives the same
position). Lines starting with '#' are skipped.
"""
import csv
import glob
import gzip
import math
import os
import struct
import sys
from collections import defaultdict


def load(path):
    rows = []
    with open(path, newline="") as f:
        lines = (l for l in f if not l.startswith("#"))
        for r in csv.DictReader(lines):
            rows.append((r["prefab"], float(r["x"]), float(r["z"]), int(r["zx"]), int(r["zy"])))
    return rows


def generated_zones(world_dir):
    oks = [int(os.path.basename(f).split(".")[1]) for f in glob.glob(os.path.join(world_dir, "_main.*.ok"))]
    d = open(os.path.join(world_dir, f"_main.{max(oks)}.db2"), "rb").read()
    n = struct.unpack_from("<i", d, 12)[0]
    p = gzip.decompress(d[16:16 + n])
    count = struct.unpack_from("<i", p, 0)[0]
    return {struct.unpack_from("<hh", p, 4 + 4 * i) for i in range(count)}


def main():
    args = sys.argv[1:]
    tol = 0.05
    if "--tol" in args:
        i = args.index("--tol")
        tol = float(args[i + 1])
        del args[i:i + 2]
    base = None
    if "--base-save" in args:
        i = args.index("--base-save")
        base = generated_zones(args[i + 1])
        del args[i:i + 2]
    if len(args) != 2:
        print(__doc__)
        return 2
    run, ref = load(args[0]), load(args[1])
    if base is not None:
        run = [r for r in run if (r[3], r[4]) not in base]
        ref = [r for r in ref if (r[3], r[4]) not in base]
        zones_note = f"every zone outside the base save's {len(base)} generated zones"
    else:
        zones = {(r[3], r[4]) for r in run}
        ref = [r for r in ref if (r[3], r[4]) in zones]
        zones_note = f"{len(zones)} zones (those with a deposit in the run)"
    by = defaultdict(list)
    for r in ref:
        by[(r[0], r[3], r[4])].append(r)
    used = set()
    stats = defaultdict(lambda: [0, 0, 0])  # prefab: matched, run total, ref total
    for r in ref:
        stats[r[0]][2] += 1
    for r in run:
        stats[r[0]][1] += 1
        best, bd = None, tol
        for c in by[(r[0], r[3], r[4])]:
            if id(c) in used:
                continue
            d = math.hypot(c[1] - r[1], c[2] - r[2])
            if d <= bd:
                best, bd = c, d
        if best is not None:
            used.add(id(best))
            stats[r[0]][0] += 1
    tm = tr = tf = 0
    print(f"{'prefab':20} {'matched':>8} {'run':>8} {'ref':>8} {'match %':>8}")
    for p in sorted(stats):
        m, n, f = stats[p]
        tm += m; tr += n; tf += f
        print(f"{p:20} {m:8} {n:8} {f:8} {100.0 * m / max(n, f, 1):8.1f}")
    print(f"{'all':20} {tm:8} {tr:8} {tf:8} {100.0 * tm / max(tr, tf, 1):8.1f}")
    print("zones compared: " + zones_note)
    return 0


if __name__ == "__main__":
    sys.exit(main())
