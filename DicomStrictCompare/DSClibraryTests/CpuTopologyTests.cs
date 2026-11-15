using System;
using System.IO;
using System.Linq;
using System.Threading;
using DSClibrary.Comparing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>Physical cores without SMT siblings or low-power E-cores (crcrewso, 2026-11-15), from sysfs-shaped input and from this machine.</summary>
    [TestClass]
    public class CpuTopologyTests
    {
        [TestMethod]
        public void CpuListsParse()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 8, 10, 11 }, CpuTopology.ParseCpuList("0-3,8,10-11\n").ToArray());
            CollectionAssert.AreEqual(new[] { 5 }, CpuTopology.ParseCpuList("5").ToArray());
        }

        [TestMethod]
        public void SmtSiblingsMakeOneCore()
        {
            // A 16-core, 32-thread Ryzen (crane's 5900XT): cpu n and n + 16 share a core; every core has an L3.
            var cpus = Enumerable.Range(0, 32).Select(c => new CpuTopology.LogicalCpu(c, $"{c % 16},{c % 16 + 16}", true));
            var cores = CpuTopology.FromSysfs(cpus);
            Assert.AreEqual(16, cores.Count);
            Assert.IsTrue(cores.All(c => c.Logical.Count == 2 && c.Logical[1] == c.Logical[0] + 16));
            Assert.AreEqual(16, CpuTopology.Usable(cores).Count);
        }

        [TestMethod]
        public void LowPowerECoresAreLeftOut()
        {
            // jackdaw's Core Ultra X7 358H: 4 P-cores, 8 E-cores, then 4 low-power E-cores with no L3.
            var cpus = Enumerable.Range(0, 16).Select(c => new CpuTopology.LogicalCpu(c, $"{c}", c < 12,
                c < 4 ? CoreKind.Performance : CoreKind.Efficient));
            var cores = CpuTopology.FromSysfs(cpus);
            Assert.AreEqual(4, cores.Count(c => c.Kind == CoreKind.LowPowerEfficient));
            Assert.IsTrue(cores.Where(c => c.Kind == CoreKind.LowPowerEfficient).All(c => c.Logical[0] >= 12));
            var usable = CpuTopology.Usable(cores);
            Assert.AreEqual(12, usable.Count);
            Assert.AreEqual(4, usable.Count(c => c.Kind == CoreKind.Performance));
        }

        [TestMethod]
        public void NoCoreIsLeftOutWhenNoneHasAnL3()
        {
            var cores = CpuTopology.FromSysfs(Enumerable.Range(0, 4).Select(c => new CpuTopology.LogicalCpu(c, $"{c}", false)));
            Assert.AreEqual(4, CpuTopology.Usable(cores).Count);
            Assert.IsTrue(cores.All(c => c.Kind == CoreKind.Unknown));
        }

        [TestMethod]
        public void OnlyAllowedProcessorsCount()
        {
            var cpus = Enumerable.Range(0, 8).Select(c => new CpuTopology.LogicalCpu(c, $"{c % 4},{c % 4 + 4}", true));
            var cores = CpuTopology.FromSysfs(cpus, new System.Collections.Generic.HashSet<int> { 0, 1, 5 });
            Assert.AreEqual(2, cores.Count);   // cores {0} and {1,5}
            CollectionAssert.AreEqual(new[] { 1, 5 }, cores[1].Logical.ToArray());
        }

        [TestMethod]
        public void TheRecommendedCountFitsTheList()
        {
            // The front ends offer 1..MaxThreads and mark PhysicalCores "(recommended)".
            Assert.IsTrue(CpuTopology.PhysicalCores >= 1 && CpuTopology.PhysicalCores <= CpuTopology.MaxThreads);
            Assert.AreEqual(Environment.ProcessorCount, CpuTopology.MaxThreads);
            Assert.IsFalse(CpuTopology.PinByDefault);   // the OS schedules compute threads, on every platform
            Assert.AreEqual(CpuTopology.PinByDefault, new MultiThreadComparer().PinThreads);
        }

        [TestMethod]
        public void ThisMachineHasCoresAndPinningHolds()
        {
            Console.WriteLine(CpuTopology.Summary);
            foreach (var c in CpuTopology.All) Console.WriteLine($"  {c}");
            Assert.IsTrue(CpuTopology.Cores.Count >= 1);
            Assert.IsTrue(CpuTopology.Cores.All(CpuTopology.All.Contains));
            if (!OperatingSystem.IsLinux()) return;

            // A new thread pinned to the last usable core reports that one processor as its only allowed one.
            var core = CpuTopology.Cores[^1];
            string? allowed = null;
            bool pinned = false;
            var t = new Thread(() =>
            {
                pinned = CpuTopology.PinCurrentThread(core);
                allowed = File.ReadLines("/proc/thread-self/status").First(l => l.StartsWith("Cpus_allowed_list:")).Split(':')[1].Trim();
            });
            t.Start();
            t.Join();
            Assert.IsTrue(pinned);
            Assert.AreEqual(core.Logical[0].ToString(), allowed);
        }
    }
}
