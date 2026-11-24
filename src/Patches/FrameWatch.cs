// patches: TASK-4 (TASK-4.6). Testing only (fork only): with MUNIN_FRAME_LOG=<seconds>, every that many
// seconds log the game server's frame rate, its longest frame and how many frames took over 50 ms, so a
// change's effect on the server can be read before, during and after it. Off unless the variable is set.
using System;
using System.Collections;
using UnityEngine;

namespace MuninMap.Patches
{
    internal static class FrameWatch
    {
        private static bool started;

        public static void Start()
        {
            if (started || !int.TryParse(Environment.GetEnvironmentVariable("MUNIN_FRAME_LOG"), out int secs) || secs <= 0) return;
            started = true;
            StaticCoroutine.Start(Watch(secs));
        }

        private static IEnumerator Watch(int secs)
        {
            var window = System.Diagnostics.Stopwatch.StartNew();
            var frame = System.Diagnostics.Stopwatch.StartNew();
            int frames = 0, slow = 0; double longest = 0;
            while (true)
            {
                yield return null;
                double ms = frame.Elapsed.TotalMilliseconds; frame.Restart();
                frames++;
                if (ms > longest) longest = ms;
                if (ms > 50) slow++;
                if (window.Elapsed.TotalSeconds >= secs)
                {
                    ZLog.Log($"MuninMap: frames: {frames / window.Elapsed.TotalSeconds:F1} fps, longest {longest:F1} ms, {slow} over 50 ms ({InPlaceOre.Status})");
                    window.Restart(); frames = 0; slow = 0; longest = 0;
                }
            }
        }
    }
}
