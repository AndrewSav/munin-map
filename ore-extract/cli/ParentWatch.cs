// --parent-pid PID (TASK-4.5): the tool stops (and so do its servers) when that process is gone, for
// a program that runs the CLI and must not leave it behind.
using System.Diagnostics;
using OreExtract.Core;

namespace OreExtractCli;

internal static class ParentWatch
{
    public static void Start(int pid)
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                Thread.Sleep(2000);
                if (Alive(pid)) continue;
                Install.Say($"the process that started this run (pid {pid}) is gone; stopping (the run can be resumed)");
                Status.Set(("state", "stopped"), ("reason", $"the process that started it (pid {pid}) is gone"));
                Runner.Stop($"the process that started it (pid {pid}) is gone");
                Environment.Exit(4);   // ProcessExit stops the servers too; on Windows the job object ends them
            }
        }) { IsBackground = true, Name = "parent-watch" };
        t.Start();
    }

    private static bool Alive(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch { return false; }
    }
}
