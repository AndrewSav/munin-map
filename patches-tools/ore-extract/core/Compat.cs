// What the shared code needs that .NET Framework 4.8 (Munin, on the game's Mono) lacks or does
// differently from .NET 8 (the CLI): one way to write each, used by both.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OreExtract.Core;

internal static class Compat
{
    // Environment.OSVersion rather than RuntimeInformation: every runtime the code meets has it
    public static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;
    public static bool IsLinux => Environment.OSVersion.Platform == PlatformID.Unix && Directory.Exists("/proc/self");
    public static int ProcessId => Process.GetCurrentProcess().Id;

    public static void MoveOver(string from, string to)
    {
        if (File.Exists(to)) File.Delete(to);
        File.Move(from, to);
    }

    public static string Hex(byte[] b) => BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();

    public static string Sha256Hex(byte[] data)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Hex(sha.ComputeHash(data));
    }

    public static string Get(this Dictionary<string, string> d, string key, string def = null) => d != null && d.TryGetValue(key, out var v) ? v : def;

    public static IEnumerable<T> Last<T>(this List<T> l, int n) => l.Skip(Math.Max(0, l.Count - n));

    // one command line from arguments, quoted as both Windows (CommandLineToArgvW) and Mono/.NET on
    // Linux split it
    public static string Join(IEnumerable<string> args)
    {
        var sb = new StringBuilder();
        foreach (var a in args)
        {
            if (sb.Length > 0) sb.Append(' ');
            if (a.Length > 0 && a.IndexOfAny([' ', '\t', '"', '\\']) < 0) { sb.Append(a); continue; }
            sb.Append('"');
            int slashes = 0;
            foreach (char c in a)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { sb.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
                sb.Append('\\', slashes).Append(c); slashes = 0;
            }
            sb.Append('\\', slashes * 2).Append('"');
        }
        return sb.ToString();
    }
}

// CPU priority and affinity of a child process, through the OS directly: .NET's Process properties
// for these aren't implemented on Mono for Linux
internal static class ProcCtl
{
    // lowest: idle class (Windows), nice 19 (Linux); else below normal / nice 10
    public static string SetPriority(Process p, bool lowest)
    {
        if (Compat.IsWindows)
            return SetPriorityClass(p.Handle, lowest ? 0x40u : 0x4000u) ? (lowest ? "lowest priority" : "below normal priority") : "priority NOT set";
        return setpriority(0 /* PRIO_PROCESS */, (uint)p.Id, lowest ? 19 : 10) == 0 ? (lowest ? "lowest priority" : "below normal priority") : "priority NOT set";
    }

    // CPU time a child has used (all its threads); on Linux from /proc: Mono doesn't give another
    // process's CPU time there (utime + stime, in clock ticks of 1/100 s)
    public static double CpuSeconds(Process p)
    {
        if (Compat.IsLinux)
        {
            try
            {
                string stat = File.ReadAllText($"/proc/{p.Id}/stat");
                var f = stat.Substring(stat.LastIndexOf(')') + 2).Split(' ');
                return (long.Parse(f[11]) + long.Parse(f[12])) / 100.0;
            }
            catch { }
        }
        return p.TotalProcessorTime.TotalSeconds;
    }

    // the CPUs this process may use, as a bit mask
    public static ulong CurrentAffinity()
    {
        try
        {
            if (Compat.IsWindows)
                return GetProcessAffinityMask(Process.GetCurrentProcess().Handle, out var pm, out _) ? (ulong)pm : AllCpus();
            var set = new ulong[16];
            if (sched_getaffinity(0, (IntPtr)(set.Length * 8), set) == 0) return set[0] != 0 ? set[0] : AllCpus();
        }
        catch { }
        return AllCpus();
    }

    private static ulong AllCpus() => Environment.ProcessorCount >= 64 ? ulong.MaxValue : (1UL << Environment.ProcessorCount) - 1;

    public static bool SetAffinity(Process p, ulong mask)
    {
        if (Compat.IsWindows) return SetProcessAffinityMask(p.Handle, (UIntPtr)mask);
        var set = new ulong[16];
        set[0] = mask;
        return sched_setaffinity(p.Id, (IntPtr)(set.Length * 8), set) == 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetPriorityClass(IntPtr h, uint cls);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessAffinityMask(IntPtr h, out UIntPtr process, out UIntPtr system);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetProcessAffinityMask(IntPtr h, UIntPtr mask);
    [DllImport("libc", SetLastError = true)] private static extern int setpriority(int which, uint who, int prio);
    [DllImport("libc", SetLastError = true)] private static extern int sched_getaffinity(int pid, IntPtr size, ulong[] mask);
    [DllImport("libc", SetLastError = true)] private static extern int sched_setaffinity(int pid, IntPtr size, ulong[] mask);
}
