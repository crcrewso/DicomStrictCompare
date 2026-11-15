using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DSClibrary.Comparing
{
    /// <summary>
    /// Memory free for new work now: the OS's available memory (Linux MemAvailable, Windows ullAvailPhys), never more
    /// than the process may use (<see cref="GCMemoryInfo.TotalAvailableMemoryBytes"/>, which honours a container
    /// limit). <c>TotalAvailableMemoryBytes</c> alone is the machine's total, not what is free (crane, 2026-11-15:
    /// it sized 28 pairs at once of 1 mm grids on a 64 GB machine).
    /// </summary>
    public static class FreeMemory
    {
        public static long Bytes()
        {
            var gc = GC.GetGCMemoryInfo();
            long limit = gc.TotalAvailableMemoryBytes;
            long? os = null;
            try
            {
                if (OperatingSystem.IsLinux()) os = Linux();
                else if (OperatingSystem.IsWindows()) os = Windows();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or DllNotFoundException or EntryPointNotFoundException) { }
            // Elsewhere, or if the OS won't say: the limit less what the last GC saw in use.
            long free = os ?? Math.Max(0, limit - gc.MemoryLoadBytes);
            return Math.Max(0, Math.Min(free, limit));
        }

        private static long? Linux()
        {
            string? line = File.ReadLines("/proc/meminfo").FirstOrDefault(l => l.StartsWith("MemAvailable:", StringComparison.Ordinal));
            if (line is null) return null;
            string kb = line.Substring("MemAvailable:".Length).Trim().Split(' ')[0];
            return long.Parse(kb, System.Globalization.CultureInfo.InvariantCulture) * 1024;
        }

        private static long? Windows()
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            return GlobalMemoryStatusEx(ref status) ? (long)status.AvailPhys : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length, MemoryLoad;
            public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
    }
}
