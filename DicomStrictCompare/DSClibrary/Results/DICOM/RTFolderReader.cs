using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSClibrary.Results.DICOM
{
    /// <summary>
    /// A dose together with the plan and beam it came from, if they were found.
    /// <para>
    /// <see cref="Plan"/> and <see cref="Beam"/> are nullable because a dose may arrive without its
    /// plan - Cody's words were a "potentially accompanying" plan file - and a dose alone is still
    /// readable: it has its grid, its scaling and its units. What it lacks is most of what identifies
    /// the beam, which is why the pairing exists at all.
    /// </para>
    /// </summary>
    public sealed class RTDoseWithPlan
    {
        public RTDoseResult Dose { get; }
        public RTPlanResult? Plan { get; }
        public RTPlanBeam? Beam { get; }

        /// <summary>
        /// The monitor units of this dose's beam, from the plan's fraction group. Recorded and never
        /// applied to a dose (Cody, 2026-11-08).
        /// </summary>
        public double? MonitorUnits { get; }

        internal RTDoseWithPlan(RTDoseResult dose, RTPlanResult? plan, RTPlanBeam? beam, double? monitorUnits)
        {
            Dose = dose;
            Plan = plan;
            Beam = beam;
            MonitorUnits = monitorUnits;
        }

        public bool HasPlan => Plan is not null;

        /// <summary>True when the plan was found but the beam it names was not in it.</summary>
        public bool HasDanglingBeamReference => Plan is not null && Beam is null;

        public override string ToString()
        {
            string plan = Plan is null
                ? "no plan"
                : Beam is null
                    ? $"{Plan.RTPlanLabel}, beam {Dose.ReferencedBeamNumber} missing"
                    : $"{Plan.RTPlanLabel} / {Beam.BeamName}";
            return $"{Path.GetFileName(Dose.SourcePath)} <- {plan}";
        }
    }

    /// <summary>What a folder of RT files contained, and what could not be matched.</summary>
    public sealed class RTFolderContents
    {
        public string Directory { get; }

        /// <summary>Plans indexed by SOPInstanceUID, which is what a dose references.</summary>
        public IReadOnlyDictionary<string, RTPlanResult> PlansByUid { get; }

        /// <summary>Every dose read, each with its plan and beam where they were found.</summary>
        public IReadOnlyList<RTDoseWithPlan> Doses { get; }

        /// <summary>Files that could not be read at all, with the reason. Reported, never swallowed.</summary>
        public IReadOnlyList<(string Path, string Reason)> Unreadable { get; }

        internal RTFolderContents(
            string directory,
            IReadOnlyDictionary<string, RTPlanResult> plansByUid,
            IReadOnlyList<RTDoseWithPlan> doses,
            IReadOnlyList<(string, string)> unreadable)
        {
            Directory = directory;
            PlansByUid = plansByUid;
            Doses = doses;
            Unreadable = unreadable;
        }

        public IEnumerable<RTPlanResult> Plans => PlansByUid.Values;

        /// <summary>Doses whose plan was not in the folder. Readable, but short of beam identity.</summary>
        public IEnumerable<RTDoseWithPlan> DosesWithoutPlan => Doses.Where(d => !d.HasPlan);

        public override string ToString() =>
            $"{Path.GetFileName(Directory)}: {PlansByUid.Count} plan(s), {Doses.Count} dose(s), "
            + $"{DosesWithoutPlan.Count()} unpaired, {Unreadable.Count} unreadable";
    }

    /// <summary>
    /// Reads a folder of RT Dose and RT Plan files in two passes, plans first.
    /// <para>
    /// Cody's design, 2026-11-08: <i>"reading dose files will be done with an automated handler
    /// finding the plan files first, reading all of their information, then reading the dose files and
    /// matching missing information for each dosevolume"</i>. The measurements support it - the 9
    /// plans in <c>resources/</c> serve all 26 doses, so plans-first reads each plan once instead of
    /// up to nine times.
    /// </para>
    /// <para>
    /// <b>Matching is by SOPInstanceUID, not by filename.</b> Measured: all 26 doses carry a
    /// ReferencedRTPlanSequence and all 26 resolve, none unmatched. PyMPAT matches by filename
    /// convention, which works until somebody renames a file and then fails silently; a UID cannot do
    /// that.
    /// </para>
    /// </summary>
    public static class RTFolderReader
    {
        /// <summary>
        /// Reads every DICOM file in a folder. Files that are neither RT Dose nor RT Plan are skipped
        /// without complaint; files that look like they should parse and do not are reported in
        /// <see cref="RTFolderContents.Unreadable"/> rather than throwing, so one bad file in a folder
        /// of twenty-six does not lose the other twenty-five.
        /// </summary>
        public static RTFolderContents Read(string directory, string searchPattern = "*.dcm", bool recursive = false)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A directory is required.", nameof(directory));
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);

            string[] paths = Directory
                .GetFiles(directory, searchPattern,
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();

            var unreadable = new List<(string, string)>();

            // Pass one: the plans, indexed by the UID a dose will reference.
            var plansByUid = new Dictionary<string, RTPlanResult>(StringComparer.Ordinal);
            var doseFiles = new List<DicomFile>();

            foreach (string path in paths)
            {
                DicomFile file;
                try
                {
                    file = DicomFileReader.ReadFile(path);
                }
                catch (Exception ex)
                {
                    unreadable.Add((path, $"{ex.GetType().Name}: {ex.Message}"));
                    continue;
                }

                if (file.IsRTPlan)
                {
                    var plan = new RTPlanResult(file);
                    if (plan.SOPInstanceUID is { Length: > 0 } uid)
                        plansByUid[uid] = plan;
                    else
                        unreadable.Add((path, "RT Plan has no SOPInstanceUID, so no dose can reference it"));
                }
                else if (file.IsRTDose)
                {
                    doseFiles.Add(file);
                }
                // Anything else in the folder is not ours and is left alone.
            }

            // Pass two: the doses, each matched to a plan and a beam.
            var doses = new List<RTDoseWithPlan>();
            foreach (DicomFile file in doseFiles)
            {
                var dose = new RTDoseResult(file);

                RTPlanResult? plan = dose.ReferencedRTPlanSOPInstanceUID is { Length: > 0 } uid
                                     && plansByUid.TryGetValue(uid, out var found)
                    ? found
                    : null;

                RTPlanBeam? beam = plan is not null && dose.ReferencedBeamNumber is { } number
                    ? plan.Beam(number)
                    : null;

                double? monitorUnits = plan is not null && dose.ReferencedBeamNumber is { } n
                    ? plan.MonitorUnitsFor(n)
                    : null;

                doses.Add(new RTDoseWithPlan(dose, plan, beam, monitorUnits));
            }

            return new RTFolderContents(directory, plansByUid, doses, unreadable);
        }
    }
}
