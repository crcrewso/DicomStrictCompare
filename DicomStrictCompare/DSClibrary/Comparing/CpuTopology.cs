using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DSClibrary.Comparing
{
    /// <summary>The kind of a core. Low-power E-cores are Intel's SoC-tile cores (Meteor, Lunar, Panther Lake).</summary>
    public enum CoreKind { Unknown, Performance, Efficient, LowPowerEfficient }

    /// <summary>
    /// The cores compute threads use by default: one thread per physical core, SMT siblings ignored (Cody, 2026-11-15:
    /// "SMT threads should always be ignored"), and low-power E-cores left out ("limit yourself to not use low power e
    /// cores"). crane measured on a 5900XT that a sibling adds nothing to this double-precision work and can cost up to
    /// 40 %. A low-power core is one with no L3 cache when other cores have one; on AMD every CCD has its own L3, so none
    /// is left out. Linux reads sysfs, Windows asks the OS, macOS asks sysctl; anything else, or a failure, falls back to
    /// every logical processor. The L3 rule is a stand-in, kept for review: see design/core-selection.md.
    /// </summary>
    public static class CpuTopology
    {
        private static readonly Lazy<IReadOnlyList<Core>> _all = new(Detect);

        /// <summary>One core: its logical processors, lowest first; its Windows processor group; its kind; whether it has an L3.</summary>
        public sealed record Core(IReadOnlyList<int> Logical, int Group = 0, CoreKind Kind = CoreKind.Unknown, bool HasL3 = true)
        {
            public override string ToString() => $"{Kind} {string.Join(",", Logical)}{(Group != 0 ? $" (group {Group})" : "")}";
        }

        /// <summary>Every core this process may run on.</summary>
        public static IReadOnlyList<Core> All => _all.Value;

        /// <summary>The cores compute threads use: <see cref="All"/> without the low-power E-cores, unless that leaves none.</summary>
        public static IReadOnlyList<Core> Cores => Usable(All);

        /// <summary>
        /// Compute threads to use by default, the "(recommended)" count: one per usable core, never more than
        /// <see cref="MaxThreads"/>, which honours a process's CPU limit.
        /// </summary>
        public static int PhysicalCores => Math.Max(1, Math.Min(Cores.Count, MaxThreads));

        /// <summary>The most threads a front end offers: every logical processor, SMT siblings included.</summary>
        public static int MaxThreads => Math.Max(1, Environment.ProcessorCount);

        /// <summary>
        /// Whether compute threads are pinned to cores by default: Linux only. Windows schedules its own (Cody,
        /// 2026-11-15: "let windows figure out the scheduling"); macOS can't pin.
        /// </summary>
        public static bool PinByDefault => OperatingSystem.IsLinux();

        /// <summary>One line for a log: what was found and what is used.</summary>
        public static string Summary =>
            $"{All.Count} cores ({string.Join(", ", All.GroupBy(c => c.Kind).Select(g => $"{g.Count()} {g.Key}"))}), {Cores.Count} used for comparisons";

        public static IReadOnlyList<Core> Usable(IReadOnlyList<Core> all)
        {
            var usable = all.Where(c => c.Kind != CoreKind.LowPowerEfficient).ToArray();
            return usable.Length > 0 ? usable : all;
        }

        /// <summary>Marks cores with no L3 as low-power when other cores have one.</summary>
        public static IReadOnlyList<Core> Classify(IReadOnlyList<Core> cores) =>
            cores.Any(c => c.HasL3) ? cores.Select(c => c.HasL3 ? c : c with { Kind = CoreKind.LowPowerEfficient }).ToArray() : cores;

        /// <summary>
        /// Pins the calling thread to the first logical processor of a core. Returns false, leaving the thread as it
        /// was, where pinning isn't supported (macOS) or the OS refuses.
        /// </summary>
        public static bool PinCurrentThread(Core core)
        {
            try
            {
                if (OperatingSystem.IsLinux()) return PinLinux(core.Logical[0]);
                if (OperatingSystem.IsWindows()) return PinWindows(core.Group, core.Logical[0]);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
            return false;
        }

        /// <summary>A sysfs CPU list ("0-3,8,10-11") as numbers.</summary>
        public static IReadOnlyList<int> ParseCpuList(string list)
        {
            var cpus = new List<int>();
            foreach (var part in list.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int dash = part.IndexOf('-');
                if (dash < 0) cpus.Add(int.Parse(part));
                else for (int c = int.Parse(part[..dash]); c <= int.Parse(part[(dash + 1)..]); c++) cpus.Add(c);
            }
            return cpus;
        }

        /// <summary>What sysfs says about one logical processor.</summary>
        public readonly record struct LogicalCpu(int Cpu, string Siblings, bool HasL3, CoreKind Kind = CoreKind.Unknown);

        /// <summary>Cores from each logical processor's sibling list, keeping only processors the process may use, then classified.</summary>
        public static IReadOnlyList<Core> FromSysfs(IEnumerable<LogicalCpu> cpus, ISet<int>? allowed = null)
        {
            var byCpu = cpus.ToDictionary(c => c.Cpu);
            var cores = byCpu.Values
                .Where(c => allowed is null || allowed.Contains(c.Cpu))
                .Select(c => ParseCpuList(c.Siblings).Where(s => allowed is null || allowed.Contains(s)).OrderBy(s => s).ToArray())
                .Where(s => s.Length > 0)
                .DistinctBy(s => string.Join(",", s))
                .OrderBy(s => s[0])
                .Select(s => new Core(s, 0, byCpu.TryGetValue(s[0], out var first) ? first.Kind : CoreKind.Unknown,
                                      s.Any(l => byCpu.TryGetValue(l, out var c) && c.HasL3)))
                .ToArray();
            return Classify(cores);
        }

        private static IReadOnlyList<Core> Detect()
        {
            try
            {
                IReadOnlyList<Core>? cores = null;
                if (OperatingSystem.IsLinux()) cores = DetectLinux();
                else if (OperatingSystem.IsWindows()) cores = DetectWindows();
                else if (OperatingSystem.IsMacOS() && SysctlInt("hw.physicalcpu") is int n and > 0)
                    cores = Enumerable.Range(0, n).Select(i => new Core(new[] { i })).ToArray();
                if (cores is { Count: > 0 }) return cores;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException
                                           or DllNotFoundException or EntryPointNotFoundException) { }
            return Enumerable.Range(0, Environment.ProcessorCount).Select(i => new Core(new[] { i })).ToArray();
        }

        // ---- Linux ----

        private static IReadOnlyList<Core>? DetectLinux()
        {
            var root = new DirectoryInfo("/sys/devices/system/cpu");
            if (!root.Exists) return null;
            // Intel hybrid parts list their P-cores and E-cores (LP-E included) as two PMUs.
            static ISet<int> Listed(string path) => File.Exists(path) ? ParseCpuList(File.ReadAllText(path)).ToHashSet() : new HashSet<int>();
            var performance = Listed("/sys/devices/cpu_core/cpus");
            var efficient = Listed("/sys/devices/cpu_atom/cpus");
            var cpus = new List<LogicalCpu>();
            foreach (var dir in root.GetDirectories("cpu*"))
            {
                if (!int.TryParse(dir.Name.AsSpan(3), out int cpu)) continue;
                string file = Path.Combine(dir.FullName, "topology", "thread_siblings_list");
                if (!File.Exists(file)) continue;
                var caches = new DirectoryInfo(Path.Combine(dir.FullName, "cache"));
                bool l3 = caches.Exists && caches.GetDirectories("index*").Any(i =>
                    File.Exists(Path.Combine(i.FullName, "level")) && File.ReadAllText(Path.Combine(i.FullName, "level")).Trim() == "3");
                var kind = performance.Contains(cpu) ? CoreKind.Performance : efficient.Contains(cpu) ? CoreKind.Efficient : CoreKind.Unknown;
                cpus.Add(new LogicalCpu(cpu, File.ReadAllText(file), l3, kind));
            }
            return FromSysfs(cpus, AllowedLinux());
        }

        private const int CpuSetBytes = 128;   // glibc's cpu_set_t: 1024 processors

        private static ISet<int>? AllowedLinux()
        {
            var mask = new byte[CpuSetBytes];
            if (sched_getaffinity(0, (nuint)mask.Length, mask) != 0) return null;
            var set = new HashSet<int>();
            for (int i = 0; i < mask.Length * 8; i++) if ((mask[i / 8] & (1 << (i % 8))) != 0) set.Add(i);
            return set;
        }

        private static bool PinLinux(int cpu) => SetLinux(new[] { cpu });

        private static bool SetLinux(IEnumerable<int> cpus)
        {
            var mask = new byte[CpuSetBytes];
            foreach (int c in cpus) if (c < mask.Length * 8) mask[c / 8] |= (byte)(1 << (c % 8));
            return sched_setaffinity(0, (nuint)mask.Length, mask) == 0;   // 0: the calling thread
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int sched_getaffinity(int pid, nuint size, byte[] mask);

        [DllImport("libc", SetLastError = true)]
        private static extern int sched_setaffinity(int pid, nuint size, byte[] mask);

        // ---- Windows ----

        private const int RelationProcessorCore = 0, RelationCache = 2;

        /// <summary>Each entry of one GetLogicalProcessorInformationEx relationship, as a pointer to its body.</summary>
        private static void ForEachEntry(int relationship, Action<IntPtr> body)
        {
            uint length = 0;
            GetLogicalProcessorInformationEx(relationship, IntPtr.Zero, ref length);
            if (length == 0) return;
            IntPtr buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (!GetLogicalProcessorInformationEx(relationship, buffer, ref length)) return;
                for (long offset = 0; offset < length;)
                {
                    IntPtr entry = buffer + (int)offset;
                    body(entry + 8);
                    offset += Marshal.ReadInt32(entry, 4);
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private static IEnumerable<(int Group, int Cpu)> Masks(IntPtr affinity, int count)
        {
            for (int g = 0; g < Math.Max(1, count); g++)
            {
                ulong mask = (ulong)Marshal.ReadIntPtr(affinity, g * GroupAffinitySize).ToInt64();
                int group = Marshal.ReadInt16(affinity, g * GroupAffinitySize + IntPtr.Size);
                for (int b = 0; b < 64; b++) if ((mask & (1UL << b)) != 0) yield return (group, b);
            }
        }

        private static IReadOnlyList<Core>? DetectWindows()
        {
            // L3 caches: CACHE_RELATIONSHIP is Level, Associativity, LineSize, CacheSize, Type, Reserved[18], GroupCount, masks.
            var withL3 = new HashSet<(int, int)>();
            ForEachEntry(RelationCache, body =>
            {
                if (Marshal.ReadByte(body, 0) != 3) return;
                foreach (var p in Masks(body + 32, Marshal.ReadInt16(body, 30))) withL3.Add(p);
            });
            // Cores: PROCESSOR_RELATIONSHIP is Flags, EfficiencyClass, Reserved[20], GroupCount, masks.
            var found = new List<(Core Core, int Efficiency)>();
            ForEachEntry(RelationProcessorCore, body =>
            {
                int efficiency = Marshal.ReadByte(body, 1);
                var members = Masks(body + 24, Marshal.ReadInt16(body, 22)).ToArray();
                if (members.Length == 0) return;
                int group = members[0].Group;
                var logical = members.Where(m => m.Group == group).Select(m => m.Cpu).OrderBy(c => c).ToArray();
                found.Add((new Core(logical, group, CoreKind.Unknown, withL3.Count == 0 || members.Any(withL3.Contains)), efficiency));
            });
            // Several efficiency classes: the highest is the P-cores (Windows numbers more efficient cores lower).
            bool hybrid = found.Select(f => f.Efficiency).Distinct().Count() > 1;
            int top = found.Count > 0 ? found.Max(f => f.Efficiency) : 0;
            var cores = found.Select(f => hybrid ? f.Core with { Kind = f.Efficiency == top ? CoreKind.Performance : CoreKind.Efficient } : f.Core).ToArray();
            return Classify(cores);
        }

        private static readonly int GroupAffinitySize = IntPtr.Size + 8;   // KAFFINITY, WORD Group, WORD Reserved[3]

        [StructLayout(LayoutKind.Sequential)]
        private struct GroupAffinity
        {
            public UIntPtr Mask;
            public ushort Group;
            public ushort Reserved0, Reserved1, Reserved2;
        }

        private static bool PinWindows(int group, int cpu) => SetWindows(group, new[] { cpu });

        private static bool SetWindows(int group, IEnumerable<int> cpus)
        {
            ulong mask = 0;
            foreach (int c in cpus) if (c < 64) mask |= 1UL << c;
            var wanted = new GroupAffinity { Mask = (UIntPtr)mask, Group = (ushort)group };
            return SetThreadGroupAffinity(GetCurrentThread(), ref wanted, out _);
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(int relationship, IntPtr buffer, ref uint length);

        [DllImport("kernel32")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetThreadGroupAffinity(IntPtr thread, ref GroupAffinity affinity, out GroupAffinity previous);

        // ---- macOS ----

        private static int? SysctlInt(string name)
        {
            int value = 0;
            nint size = sizeof(int);
            return sysctlbyname(name, ref value, ref size, IntPtr.Zero, 0) == 0 ? value : null;
        }

        [DllImport("libc")]
        private static extern int sysctlbyname(string name, ref int value, ref nint size, IntPtr newValue, nint newSize);
    }
}
