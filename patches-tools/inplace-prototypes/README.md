# In-place ore prediction: prototypes kept for comparison (TASK-4.6)

Not built. Kept so a method can be compared again or brought back.

- `InPlaceOre.physics-b.cs.txt`: blocking prototype (b). Placement on the game thread with the real
  `UnityEngine.Random`, terrain from `HeightmapBuilder.Build` on a worker thread, and earlier objects
  as collider-only copies in a private physics scene, raycast with PhysX. Against the TASK-27
  snapshot (zones live had not generated, 0.5 m): copper 97.7%, buried scrap 98.8%, silver 92.7%,
  tin 97.3%, obsidian 95.0%, all 96.7%. Whole world in about 4.4 min, about 4 s of
  game-thread work in all. To build it, copy it back over `src/Patches/InPlaceOre.cs` (it needs the
  `UnityEngine.PhysicsModule` reference in `src/Directory.Build.targets`).
