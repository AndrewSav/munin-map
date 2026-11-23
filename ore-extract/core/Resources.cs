// CPU, memory and port helpers, and the protections that make the tool, not a game server next to
// it, the one that gives way when memory runs short.
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OreExtract.Core;

public static class Mem
{
    public const double GB = 1024.0 * 1024 * 1024;

    // memory this tool can still use: the system's available memory and, in a container or other
    // control group with a limit, that limit minus what the group uses (without reclaimable file
    // cache, as Docker counts it). The smaller wins.
    public static long Available(out string where)
    {
        if (Compat.IsWindows)
        {
            var s = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            where = "system";
            return GlobalMemoryStatusEx(ref s) ? (long)s.ullAvailPhys : 0;
        }
        long avail = MemInfo("MemAvailable");
        where = "system";
        long cg = CgroupHeadroom();
        if (cg >= 0 && cg < avail) { avail = cg; where = "container limit"; }
        return avail;
    }

    public static long Total()
    {
        if (Compat.IsWindows)
        {
            var s = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref s) ? (long)s.ullTotalPhys : 0;
        }
        return MemInfo("MemTotal");
    }

    private static long MemInfo(string key)
    {
        foreach (var line in File.ReadLines("/proc/meminfo"))
            if (line.StartsWith(key + ":"))
                return long.Parse(line.Split([' '], StringSplitOptions.RemoveEmptyEntries)[1]) * 1024;
        return 0;
    }

    // -1 = no limit found
    private static long CgroupHeadroom()
    {
        try
        {
            // cgroup v2: "0::/path"; check that group and its parents up to the root
            var v2 = File.ReadLines("/proc/self/cgroup").FirstOrDefault(l => l.StartsWith("0::"));
            if (v2 != null && File.Exists("/sys/fs/cgroup/cgroup.controllers"))
            {
                long best = -1;
                string rel = v2.Substring(3).Trim();
                while (true)
                {
                    string dir = Path.Combine("/sys/fs/cgroup", rel.TrimStart('/'));
                    string max = Path.Combine(dir, "memory.max");
                    if (File.Exists(max))
                    {
                        string t = File.ReadAllText(max).Trim();
                        if (t != "max" && long.TryParse(t, out long limit))
                        {
                            long used = ReadLong(Path.Combine(dir, "memory.current")) - Stat(Path.Combine(dir, "memory.stat"), "inactive_file");
                            long h = limit - used;
                            if (best < 0 || h < best) best = h;
                        }
                    }
                    if (rel == "/" || rel.Length == 0) break;
                    rel = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "/";
                }
                return best;
            }
            // cgroup v1
            string v1 = "/sys/fs/cgroup/memory";
            if (File.Exists(Path.Combine(v1, "memory.limit_in_bytes")))
            {
                long limit = ReadLong(Path.Combine(v1, "memory.limit_in_bytes"));
                if (limit <= 0 || limit > (1L << 60)) return -1;
                long used = ReadLong(Path.Combine(v1, "memory.usage_in_bytes")) - Stat(Path.Combine(v1, "memory.stat"), "total_inactive_file");
                return limit - used;
            }
        }
        catch { }
        return -1;
    }

    private static long ReadLong(string f) => long.TryParse(File.ReadAllText(f).Trim(), out long v) ? v : 0;

    private static long Stat(string f, string key)
    {
        if (!File.Exists(f)) return 0;
        foreach (var line in File.ReadLines(f))
        {
            var p = line.Split(' ');
            if (p.Length == 2 && p[0] == key && long.TryParse(p[1], out long v)) return v;
        }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

// the kernel's choice when memory runs out (Linux) or a hard cap (Windows): the generator goes first
public sealed class Protection : IDisposable
{
    private IntPtr job;

    public Protection(long perProcessLimitBytes)
    {
        if (!Compat.IsWindows) return;
        job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return;
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        info.ProcessMemoryLimit = (UIntPtr)(ulong)perProcessLimitBytes;
        int size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        IntPtr p = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, p, false);
            if (!SetInformationJobObject(job, 9 /* JobObjectExtendedLimitInformation */, p, (uint)size))
                throw new ToolError("could not set the memory limit (Windows job object): error " + Marshal.GetLastWin32Error());
        }
        finally { Marshal.FreeHGlobal(p); }
    }

    // Linux: oom_score_adj 1000 makes this process the first the kernel kills when its container
    // or the machine runs out of memory; raising one's own child's score needs no privileges.
    // Windows: the job caps the process's committed memory, and closing the job (this tool
    // exiting) ends it.
    public string Apply(Process p)
    {
        if (Compat.IsWindows)
        {
            if (job == IntPtr.Zero || !AssignProcessToJobObject(job, p.Handle)) return "memory cap NOT set (job object failed)";
            return "memory cap set (job object)";
        }
        try { File.WriteAllText($"/proc/{p.Id}/oom_score_adj", "1000"); return "oom_score_adj 1000"; }
        catch (Exception e) { return "oom_score_adj NOT set: " + e.Message; }
    }

    public void Dispose()
    {
        if (job != IntPtr.Zero) { CloseHandle(job); job = IntPtr.Zero; }
    }

    private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x100, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr a, string name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
}

public static class Ports
{
    private static readonly HashSet<int> taken = new();

    // the server listens on UDP port P and P+1: find a pair nothing else holds. Read from the system's
    // list of UDP listeners rather than by opening a socket: a test socket on all interfaces makes
    // Windows Firewall ask whether to allow the tool, which never needs incoming connections.
    // `taken` covers servers this run started that may not be listening yet.
    public static int FreePair(int from = 24560)
    {
        var used = new HashSet<int>();
        try { foreach (var ep in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()) used.Add(ep.Port); }
        catch (Exception e) { Install.Say($"could not list the UDP ports in use ({e.Message}); taking the first pair this run hasn't"); }
        for (int p = from; p < 65000; p += 2)
        {
            if (taken.Contains(p) || used.Contains(p) || used.Contains(p + 1)) continue;
            taken.Add(p);
            return p;
        }
        throw new ToolError("no free UDP port pair found");
    }
}

public static class Cpu
{
    // logical CPUs this process may use (.NET counts affinity and container CPU limits)
    public static int Logical => Environment.ProcessorCount;

    // the highest-numbered n CPUs of those this process may use
    public static ulong AffinityMask(int n)
    {
        ulong mask = ProcCtl.CurrentAffinity();
        var bits = Enumerable.Range(0, 64).Where(b => (mask & (1UL << b)) != 0).ToList();
        ulong m = 0;
        foreach (var b in bits.Skip(Math.Max(0, bits.Count - n))) m |= 1UL << b;
        return m;
    }
}
